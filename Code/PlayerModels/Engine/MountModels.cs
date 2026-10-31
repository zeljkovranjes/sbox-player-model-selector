using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Mounting;

namespace PlayerModels.Engine;

/// <summary>
/// Character models in the games mounted on this machine (Half-Life 2, Counter-Strike and the like), and which of them
/// turned out wearable. A mount holds thousands of props, so only models in character folders are offered, plus any
/// folder a player added; checking one loads it (<see cref="ModelCheck"/>), so each verdict is kept on this machine.
/// </summary>
internal static class MountModels
{
	/// <summary>A character model in a mounted game: its mount path, a title from its file name, and the game.</summary>
	internal sealed record Found( string Path, string Title, string Game );

	/// <summary>
	/// A folder of a mounted game that holds models: its mount path (<c>mount://deadlock/models/heroes_staging</c>), its
	/// path in the game, how many models are under it, and whether characters are looked for in it anyway.
	/// </summary>
	internal sealed record Folder( string Path, string Name, int Models, bool Searched );

	/// <summary>A mounted game's model folders, by path.</summary>
	internal sealed record GameFolders( string Game, List<Folder> Folders );

	// Renamed whenever what passes changes, so models checked under older rules are checked again
	private static readonly VerdictCache Verdicts = new( "player_model_selector/mount_body_verdicts.json" );

	// Folders games keep their characters in: Source and GoldSrc's, the heroes, agents and units of later games, the
	// actors of Skyrim and STALKER and the peds of GTA
	private static readonly string[] CharacterFolders =
	{
		"/player/", "/players/", "/humans/", "/characters/", "/character/", "/npc", "/heroes", "/hero/", "/agents/",
		"/survivors/", "/infected/", "/combine", "/police", "/zombie", "/actors/", "/actor/", "/ped", "/people/",
		"/chars/", "/pawns/", "/bodies/", "/skins/",
	};

	// Words in the file names of characters, for a game whose folders say nothing: those come first in its group
	private static readonly string[] CharacterWords =
		{ "ped", "player", "npc", "char", "human", "man", "woman", "male", "female", "body", "hero", "soldier", "citizen" };

	// Folders are listed this many levels into a game (models/heroes_staging): deeper ones are each hero's own
	private const int FolderDepth = 2;

	// Files in those folders that hold only animations or shared parts, never a body
	private static readonly string[] NotBodies = { "gesture", "anim", "posture", "shared", "_gib", "ragdoll_" };

	// File names of standalone first-person arms (c_arms_citizen, v_hands, arms_combine...)
	private static readonly string[] ArmsWords = { "arms", "hands" };

	/// <summary>
	/// Whether <paramref name="model"/> is a path to a model in a mounted game: <c>mount://game/folders/name.vmdl</c>,
	/// never leaving the game's folder. Picks come from clients, so nothing else counts as one.
	/// </summary>
	internal static bool IsMountPath( string model )
	{
		return !string.IsNullOrEmpty( model ) && MountUtility.IsMountPath( model )
			&& model.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) && !model.Contains( ".." ) && !model.Contains( '\\' );
	}

	/// <summary>
	/// The character models of every mounted game, by game: the ones in <paramref name="addedFolders"/> (mount paths of
	/// folders, see <see cref="Folder"/>) first, then the ones in character folders. A game whose folders hold no
	/// characters by any name known offers every model it has instead, characters by name first, so any mount works
	/// without setting anything up: each is checked as it is shown, and what isn't a body leaves the grid.
	/// </summary>
	internal static List<Found> Candidates( IReadOnlyCollection<string> addedFolders )
	{
		var found = new List<Found>();
		foreach ( var info in Sandbox.Mounting.Directory.GetAll().Where( x => x.Mounted ).OrderBy( x => x.Title ) )
		{
			var mount = Sandbox.Mounting.Directory.Get( info.Ident );
			if ( mount is null )
				continue;

			var models = mount.GetAll( ResourceType.Model );
			var paths = models.Select( x => x.Path ).ToHashSet( StringComparer.OrdinalIgnoreCase );
			var bodies = models
				.Where( x => !x.Flags.Contains( ResourceFlags.DeveloperOnly ) && !IsBreakPiece( x.Path, paths ) && IsBody( x.Path ) )
				.Select( x => x.Path )
				.ToList();

			var characters = bodies.Where( x => CharacterFolders.Any( f => x.Contains( f, StringComparison.OrdinalIgnoreCase ) ) ).ToList();
			var listed = characters.Count > 0 ? characters : bodies;

			var ordered = listed
				.Union( bodies.Where( x => InFolders( x, addedFolders ) ) )
				.OrderBy( x => InFolders( x, addedFolders ) ? 0 : characters.Count > 0 || LooksLikeCharacter( x ) ? 1 : 2 )
				.ThenBy( x => x, StringComparer.Ordinal );

			found.AddRange( ordered.Select( x => new Found( x, TitleOf( x ), info.Title ) ) );
		}

		return found;
	}

	// Whether a model's file name reads like a character's: for ordering a game whose folders say nothing
	private static bool LooksLikeCharacter( string path )
	{
		var name = path[ (path.LastIndexOf( '/' ) + 1).. ];
		return CharacterWords.Any( x => name.Contains( x, StringComparison.OrdinalIgnoreCase ) );
	}

	/// <summary>
	/// The folders of every mounted game that hold models, <see cref="FolderDepth"/> levels in, with how many models are
	/// under each. Games are in title order and folders in path order.
	/// </summary>
	internal static List<GameFolders> Folders()
	{
		var games = new List<GameFolders>();
		foreach ( var info in Sandbox.Mounting.Directory.GetAll().Where( x => x.Mounted ).OrderBy( x => x.Title ) )
		{
			var mount = Sandbox.Mounting.Directory.Get( info.Ident );
			if ( mount is null )
				continue;

			var root = $"mount://{info.Ident}/".ToLowerInvariant();
			var counts = new Dictionary<string, int>();
			foreach ( var resource in mount.GetAll( ResourceType.Model ) )
			{
				if ( resource.Flags.Contains( ResourceFlags.DeveloperOnly ) || FolderOf( resource.Path, root ) is not { } folder )
					continue;

				counts[ folder ] = counts.GetValueOrDefault( folder ) + 1;
			}

			if ( counts.Count == 0 )
				continue;

			var folders = counts
				.OrderBy( x => x.Key, StringComparer.Ordinal )
				.Select( x => new Folder( x.Key, x.Key[ root.Length.. ], x.Value, IsCharacter( x.Key + "/" ) ) )
				.ToList();

			games.Add( new GameFolders( info.Title, folders ) );
		}

		return games;
	}

	/// <summary>
	/// Standalone first-person arms in every mounted game, by game then path. Weapon view models (<c>v_*</c>) are left
	/// out: they come with their weapon built in.
	/// </summary>
	internal static List<Found> ArmsCandidates()
	{
		var found = new List<Found>();
		foreach ( var info in Sandbox.Mounting.Directory.GetAll().Where( x => x.Mounted ).OrderBy( x => x.Title ) )
		{
			var mount = Sandbox.Mounting.Directory.Get( info.Ident );
			if ( mount is null )
				continue;

			foreach ( var resource in mount.GetAll( ResourceType.Model ).OrderBy( x => x.Path ) )
			{
				var name = resource.Path[ (resource.Path.LastIndexOf( '/' ) + 1).. ];
				if ( !resource.Flags.Contains( ResourceFlags.DeveloperOnly ) && ArmsWords.Any( x => name.Contains( x, StringComparison.OrdinalIgnoreCase ) ) )
					found.Add( new Found( resource.Path, TitleOf( resource.Path ), info.Title ) );
			}
		}

		return found;
	}

	/// <summary>The game a mounted model is from, by mount ident ("deadlock"); null for any other path.</summary>
	internal static string GameOf( string model )
	{
		if ( !IsMountPath( model ) )
			return null;

		const int start = 8; // "mount://"
		var end = model.IndexOf( '/', start );
		return end > start ? model[ start..end ].ToLowerInvariant() : null;
	}

	/// <summary>Whether <paramref name="ident"/> reads as a mount ident: a short lowercase name, as mounts give them.</summary>
	internal static bool IsGameIdent( string ident )
	{
		return !string.IsNullOrEmpty( ident ) && ident.Length <= 32 && ident.All( x => x is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' );
	}

	/// <summary>The games mounted now, by ident and title.</summary>
	internal static List<(string Ident, string Title)> Mounted()
	{
		return Sandbox.Mounting.Directory.GetAll().Where( x => x.Mounted ).Select( x => (x.Ident, x.Title) ).OrderBy( x => x.Ident ).ToList();
	}

	/// <summary>The Steam logo of the mounted game titled <paramref name="title"/>; null when its mount names no Steam app.</summary>
	internal static string LogoOf( string title )
	{
		var ident = Sandbox.Mounting.Directory.GetAll().FirstOrDefault( x => x.Title == title ).Ident;
		return ident is null ? null : Sandbox.Mounting.Directory.Get( ident )?.LogoUrl;
	}

	/// <summary>The titles of the games mounted now.</summary>
	internal static List<string> MountedGames()
	{
		return Sandbox.Mounting.Directory.GetAll().Where( x => x.Mounted ).Select( x => x.Title ).OrderBy( x => x ).ToList();
	}

	/// <summary>What an earlier check said about this model: true wearable, false not, null never checked.</summary>
	internal static bool? Known( string path )
	{
		return Verdicts.Known( path );
	}

	/// <summary>Checks <paramref name="path"/> (loading it) and remembers the verdict. True when it can be worn.</summary>
	internal static async Task<bool> CheckAsync( string path )
	{
		var why = await ModelCheck.WhyNotWearableAsync( path );
		Verdicts.Remember( path, why );

		return why is null;
	}

	private static bool IsCharacter( string path )
	{
		return CharacterFolders.Any( x => path.Contains( x, StringComparison.OrdinalIgnoreCase ) ) && IsBody( path );
	}

	private static bool IsBody( string path )
	{
		var name = path[ (path.LastIndexOf( '/' ) + 1).. ];
		return !NotBodies.Any( x => name.Contains( x, StringComparison.OrdinalIgnoreCase ) );
	}

	// Source 2 keeps a model's break pieces in a folder named after it (barrel/barrel.vmdl, barrel/barrel/piece_01.vmdl)
	private static bool IsBreakPiece( string path, HashSet<string> models )
	{
		var slash = path.LastIndexOf( '/' );
		return slash > 0 && models.Contains( path[ ..slash ] + ".vmdl" );
	}

	private static bool InFolders( string path, IReadOnlyCollection<string> folders )
	{
		foreach ( var folder in folders )
		{
			if ( path.Length > folder.Length && path[ folder.Length ] == '/' && path.StartsWith( folder, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	// "mount://deadlock/models/heroes_staging/haze/haze.vmdl" -> "mount://deadlock/models/heroes_staging"; a model in
	// the game's root folder has none
	private static string FolderOf( string path, string root )
	{
		if ( !path.StartsWith( root, StringComparison.Ordinal ) )
			return null;

		var end = root.Length;
		for ( var level = 0; level < FolderDepth; level++ )
		{
			var slash = path.IndexOf( '/', end == root.Length ? end : end + 1 );
			if ( slash < 0 )
				break;

			end = slash;
		}

		return end == root.Length ? null : path[ ..end ];
	}

	// "mount://hl2/models/humans/group01/male_01.vmdl" -> "male 01"
	private static string TitleOf( string path )
	{
		var start = path.LastIndexOf( '/' ) + 1;
		var end = path.IndexOf( '.', start );
		var name = end < 0 ? path[ start.. ] : path[ start..end ];

		return name.Replace( '_', ' ' );
	}
}
