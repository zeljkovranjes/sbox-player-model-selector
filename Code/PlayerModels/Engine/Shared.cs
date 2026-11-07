using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayerModels.Core;
using Sandbox;
using Sandbox.Services;

namespace PlayerModels.Engine;

/// <summary>
/// What players share with each other through the selector: armatures and looks for each model, effects and shapes for
/// everyone, designs for each weapon, each in its own cloud tables.
/// </summary>
internal static class Shared
{
	// A group is read 300 records at a time (a request a page), oldest first as the service gives them; paging past them
	// reads on from where the last read stopped
	private const int MaxPages = 3;
	private const int PageSize = 100;

	/// <summary>
	/// The model effects and shapes are shared for: none in particular, so everyone's are in one group (see
	/// <see cref="SharedLibrary{T}.ListAsync"/>).
	/// </summary>
	internal const string Everyone = "everyone";

	/// <summary>
	/// The addon's author, whose shares seed the Workshops: all of them are listed, past the few each player has listed
	/// (see <see cref="SharedLibrary{T}.PerPlayer"/>).
	/// </summary>
	internal const long Curator = 76561199542962865;

	// shortcut: one group for everyone lists the newest 50 shares, a few dozen each from the most active players; group
	// by tag or month if the Workshop ever outgrows that
	private const int PerPlayerForEveryone = 20;

	// Writes are spaced this far apart, in seconds, so a burst of them (votes down a list, a test run) stays inside the
	// service's limit of about 40 requests in a couple of minutes
	private const float WriteGap = 1.5f;
	private static float _nextWrite;

	// The libraries are made afresh on a hot reload ([SkipHotload]): the lambdas they are made with are the static
	// constructor's, which a hot reload can't match once a method is added to this class

	/// <summary>Armatures: the bones picked by hand for a model's missing body parts.</summary>
	[SkipHotload]
	internal static readonly SharedLibrary<SharedArmature> Armatures = new( "armature", "armatures", "armature_votes",
		SharedArmature.Parse, x => x.Serialize(), x => x.Model, ( a, b ) => a.SameAs( b ) );

	/// <summary>Looks: how a model is customised (colours, skin, body groups, morphs, body shape), without its armature.</summary>
	[SkipHotload]
	internal static readonly SharedLibrary<PlayerLook> Looks = new( "look", "looks", "look_votes",
		PlayerLook.ParseShared, x => x.Customisation().Serialize(), x => x.Model, ( a, b ) => a.SameCustomisationAs( b ) );

	/// <summary>Effects (or single layers of one), with the drawn shapes they use, for anyone to wear on any model.</summary>
	[SkipHotload]
	internal static readonly SharedLibrary<Effect> Effects = new( ShareOrigin.EffectKind, "effects", "effect_votes",
		Effect.ParseShared, x => x.SharedForm(), _ => Everyone, ( a, b ) => a.SameAs( b ), PerPlayerForEveryone );

	/// <summary>Shapes and stickers made in the shape creator, for anyone to use in their effects.</summary>
	[SkipHotload]
	internal static readonly SharedLibrary<DrawnShape> Shapes = new( ShareOrigin.ShapeKind, "shapes", "shape_votes",
		DrawnShape.ParseShared, x => x.SharedForm(), _ => Everyone, ( a, b ) => a.Content == b.Content, PerPlayerForEveryone );

	/// <summary>
	/// Weapon designs, grouped by the weapon's world model: a design shows on the same weapon in any game, so it is shared
	/// for that weapon, with the drawn shapes its stickers use.
	/// </summary>
	[SkipHotload]
	internal static readonly SharedLibrary<WeaponDesign> Weapons = new( ShareOrigin.WeaponKind, "weapon_designs", "weapon_votes",
		WeaponDesign.ParseShared, x => x.SharedForm(), x => x.Weapon, ( a, b ) => a.SameAs( b ) );

	/// <summary>
	/// Waits until this player's next write to the Workshop's tables may go, each a little after the last, so they stay
	/// within the service's rate limit however many come at once.
	/// </summary>
	internal static async Task PaceAsync()
	{
		var now = RealTime.Now;
		var at = Math.Max( now, _nextWrite );
		_nextWrite = at + WriteGap;
		if ( at > now )
			await GameTask.Delay( (int)((at - now) * 1000f) );
	}

	/// <summary>
	/// The public records of <paramref name="group"/>, past the first page: anyone can write to any group, so what counts
	/// could be behind junk. Fails with the backend's own exception.
	/// </summary>
	internal static async Task<List<Database.Record>> ReadAsync( Database.Table table, string group )
	{
		return (await ReadFromAsync( table, group, 0 )).Records;
	}

	/// <summary>
	/// Whether <paramref name="e"/> means Facepunch's service isn't answering, as opposed to refusing this player something
	/// (a 4xx, which <see cref="Why"/> words).
	/// </summary>
	internal static bool IsUnresponsive( Exception e )
	{
		return e is not (DatabaseException { StatusCode: >= 400 and < 500 } or ArgumentException);
	}

	/// <summary>What to tell the player when sharing, listing or rating failed.</summary>
	internal static string Why( Exception e )
	{
		return e switch
		{
			ArgumentException => e.Message,
			DatabaseException { StatusCode: 401 } => "Sign in to Steam to share and rate.",
			DatabaseException { StatusCode: 403 } => "Sharing isn't open to you here.",
			DatabaseException { StatusCode: 429 } => "Too many changes at once. Try again in a moment.",
			_ => "Shared designs can't be reached right now.",
		};
	}

	/// <summary>
	/// The next public records of <paramref name="group"/> after <paramref name="cursor"/> (0 for the first, else a
	/// record's id: records come in the order they were made), as many pages as one read takes, and where reading goes on
	/// from: null when it reached the end. Fails with the backend's own exception.
	/// </summary>
	internal static async Task<(List<Database.Record> Records, long? Next)> ReadFromAsync( Database.Table table, string group, long cursor )
	{
		var records = new List<Database.Record>();
		for ( var page = 0; page < MaxPages; page++ )
		{
			var found = await table.List( group, cursor, PageSize );
			records.AddRange( found.Records );

			if ( found.NextCursor is not { } next )
				return (records, null);

			cursor = next;
		}

		return (records, cursor);
	}
}
