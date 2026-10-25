using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Mounting;

namespace PlayerModels.Engine;

/// <summary>
/// Character models in the games mounted on this machine (Half-Life 2, Counter-Strike and the like), and which of them
/// turned out wearable. A mount holds thousands of props, so only models in character folders are offered; checking one
/// loads it (<see cref="ModelCheck"/>), so each verdict is kept on this machine.
/// </summary>
internal static class MountModels
{
	/// <summary>A character model in a mounted game: its mount path, a title from its file name, and the game.</summary>
	internal sealed record Found( string Path, string Title, string Game );

	private static readonly VerdictCache Verdicts = new( "player_model_selector/mount_verdicts.json" );

	// Folders Source and GoldSrc games keep their characters in
	private static readonly string[] CharacterFolders =
		{ "/player/", "/players/", "/humans/", "/characters/", "/character/", "/npc/", "/combine", "/police", "/zombie" };

	// Files in those folders that hold only animations or shared parts, never a body
	private static readonly string[] NotBodies = { "gesture", "anim", "posture", "shared", "_gib", "ragdoll_" };

	// File names of standalone first-person arms (c_arms_citizen, v_hands, arms_combine...)
	private static readonly string[] ArmsWords = { "arms", "hands" };

	/// <summary>Whether <paramref name="model"/> is a path into a mounted game.</summary>
	internal static bool IsMountPath( string model )
	{
		return !string.IsNullOrEmpty( model ) && MountUtility.IsMountPath( model );
	}

	/// <summary>The character models of every mounted game, by game then path.</summary>
	internal static List<Found> Candidates()
	{
		var found = new List<Found>();
		foreach ( var info in Sandbox.Mounting.Directory.GetAll().Where( x => x.Mounted ).OrderBy( x => x.Title ) )
		{
			var mount = Sandbox.Mounting.Directory.Get( info.Ident );
			if ( mount is null )
				continue;

			foreach ( var resource in mount.GetAll( ResourceType.Model ).OrderBy( x => x.Path ) )
			{
				if ( resource.Flags.Contains( ResourceFlags.DeveloperOnly ) || !IsCharacter( resource.Path ) )
					continue;

				found.Add( new Found( resource.Path, TitleOf( resource.Path ), info.Title ) );
			}
		}

		return found;
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
		var name = path[ (path.LastIndexOf( '/' ) + 1).. ];
		return CharacterFolders.Any( x => path.Contains( x, StringComparison.OrdinalIgnoreCase ) )
			&& !NotBodies.Any( x => name.Contains( x, StringComparison.OrdinalIgnoreCase ) );
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
