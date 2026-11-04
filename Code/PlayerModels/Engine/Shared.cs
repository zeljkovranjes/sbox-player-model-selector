using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayerModels.Core;
using Sandbox;
using Sandbox.Services;

namespace PlayerModels.Engine;

/// <summary>
/// What players share with each other through the selector: armatures and looks for each model, effects and shapes for
/// everyone, each in its own cloud tables.
/// </summary>
internal static class Shared
{
	// shortcut: a group is read to 300 records, so a flood past that can still hide what is behind it; raise MaxPages, at a
	// request a page, if anyone ever does
	private const int MaxPages = 3;
	private const int PageSize = 100;

	/// <summary>
	/// The model effects and shapes are shared for: none in particular, so everyone's are in one group (see
	/// <see cref="SharedLibrary{T}.ListAsync"/>).
	/// </summary>
	internal const string Everyone = "everyone";

	// shortcut: one group for everyone lists the newest 50 shares, a few dozen each from the most active players; group
	// by tag or month if the Workshop ever outgrows that
	private const int PerPlayerForEveryone = 20;

	// Writes are spaced this far apart, in seconds, so a burst of them (votes down a list, a test run) stays inside the
	// service's limit of about 40 requests in a couple of minutes
	private const float WriteGap = 1.5f;
	private static float _nextWrite;

	/// <summary>Armatures: the bones picked by hand for a model's missing body parts.</summary>
	internal static readonly SharedLibrary<SharedArmature> Armatures = new( "armature", "armatures", "armature_votes",
		SharedArmature.Parse, x => x.Serialize(), x => x.Model, ( a, b ) => a.SameAs( b ) );

	/// <summary>Looks: how a model is customised (colours, skin, body groups, morphs, body shape), without its armature.</summary>
	internal static readonly SharedLibrary<PlayerLook> Looks = new( "look", "looks", "look_votes",
		PlayerLook.ParseShared, x => x.Customisation().Serialize(), x => x.Model, ( a, b ) => a.SameCustomisationAs( b ) );

	/// <summary>Effects (or single layers of one), with the drawn shapes they use, for anyone to wear on any model.</summary>
	internal static readonly SharedLibrary<Effect> Effects = new( ShareOrigin.EffectKind, "effects", "effect_votes",
		Effect.ParseShared, x => x.SharedForm(), _ => Everyone, ( a, b ) => a.SameAs( b ), PerPlayerForEveryone );

	/// <summary>Shapes and stickers made in the shape creator, for anyone to use in their effects.</summary>
	internal static readonly SharedLibrary<DrawnShape> Shapes = new( ShareOrigin.ShapeKind, "shapes", "shape_votes",
		DrawnShape.ParseShared, x => x.SharedForm(), _ => Everyone, ( a, b ) => a.Content == b.Content, PerPlayerForEveryone );

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
		var records = new List<Database.Record>();
		var cursor = 0L;

		for ( var page = 0; page < MaxPages; page++ )
		{
			var found = await table.List( group, cursor, PageSize );
			records.AddRange( found.Records );

			if ( found.NextCursor is not { } next )
				break;

			cursor = next;
		}

		return records;
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
}
