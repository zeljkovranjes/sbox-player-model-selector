using System;
using System.Collections.Generic;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The player's drafts (see <see cref="Draft"/>), kept on this machine in the game's own data folder rather than its
/// cookies, which every start reads whole: a kind's file is read the first time its drafts are asked for, and written
/// whenever one is saved, renamed or deleted. Each kind keeps at most <see cref="Draft.MaxPerKind"/>, the newest first.
/// </summary>
internal static class Drafts
{
	private const string Folder = "player_model_selector";

	private static readonly Dictionary<Draft.Of, List<Draft>> Kept = new();
	private static int _made;

	/// <summary>Counts up whenever a draft is saved, renamed or deleted: what lists them asks again when it changes.</summary>
	internal static int Revision { get; private set; }

	/// <summary>Now, as drafts keep the time: seconds since 1970 (UTC).</summary>
	internal static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

	/// <summary>The drafts of <paramref name="kind"/>, the newest first, each within bounds.</summary>
	internal static IReadOnlyList<Draft> Of( Draft.Of kind ) => Read( kind );

	/// <summary>The draft of <paramref name="kind"/> with id <paramref name="id"/>; null for none.</summary>
	internal static Draft Find( Draft.Of kind, string id ) => id is null ? null : Read( kind ).Find( x => x.Id == id );

	/// <summary>
	/// A draft of <paramref name="kind"/> named <paramref name="name"/> holding <paramref name="item"/> and the editor's
	/// <paramref name="state"/>, edited now: the one with <paramref name="id"/> saved again, or a new one for null.
	/// </summary>
	internal static Draft Make( Draft.Of kind, string id, string name, string item, string state ) => new()
	{
		Id = id ?? Draft.NewId( Now, _made++ ), Kind = kind.ToString(), Name = name, Edited = Now, Item = item, State = state,
	};

	/// <summary>
	/// Keeps <paramref name="draft"/>, the newest of its kind. False, with nothing kept, when it isn't within bounds (too
	/// big), or when it is new and its kind is full: then, with <paramref name="dropOldest"/>, the oldest is let go of
	/// for it and named in <paramref name="dropped"/>.
	/// </summary>
	internal static bool Save( Draft draft, bool dropOldest, out Draft dropped )
	{
		dropped = null;
		if ( draft?.Sanitized() is not { } kept || kept.KindIs is not { } kind )
			return false;

		var drafts = Draft.Put( Read( kind ), kept, dropOldest, out dropped );
		if ( drafts is null )
			return false;

		Write( kind, drafts );
		return true;
	}

	/// <summary>The oldest draft of <paramref name="kind"/>, the one a full kind would let go of; null for none.</summary>
	internal static Draft Oldest( Draft.Of kind ) => Draft.Oldest( Read( kind ) );

	/// <summary>Renames the draft of <paramref name="kind"/> with id <paramref name="id"/>.</summary>
	internal static void Rename( Draft.Of kind, string id, string name )
	{
		var drafts = Read( kind );
		var at = drafts.FindIndex( x => x.Id == id );
		if ( at < 0 )
			return;

		var renamed = new Draft { Id = id, Kind = drafts[ at ].Kind, Name = name, Edited = drafts[ at ].Edited, Item = drafts[ at ].Item, State = drafts[ at ].State }.Sanitized();
		if ( renamed is null )
			return;

		drafts[ at ] = renamed;
		Write( kind, drafts );
	}

	/// <summary>Deletes the draft of <paramref name="kind"/> with id <paramref name="id"/>.</summary>
	internal static void Delete( Draft.Of kind, string id )
	{
		var drafts = Read( kind );
		if ( drafts.RemoveAll( x => x.Id == id ) > 0 )
			Write( kind, drafts );
	}

	private static string FileOf( Draft.Of kind ) => $"{Folder}/drafts_{kind.ToString().ToLowerInvariant()}.json";

	private static List<Draft> Read( Draft.Of kind )
	{
		if ( Kept.TryGetValue( kind, out var drafts ) )
			return drafts;

		drafts = new List<Draft>();
		foreach ( var draft in FileSystem.Data.ReadJsonOrDefault( FileOf( kind ), new List<Draft>() ) ?? new List<Draft>() )
		{
			if ( drafts.Count < Draft.MaxPerKind && draft?.Sanitized() is { } kept && kept.KindIs == kind && !drafts.Exists( x => x.Id == kept.Id ) )
				drafts.Add( kept );
		}

		drafts.Sort( ( a, b ) => b.Edited.CompareTo( a.Edited ) );
		Kept[ kind ] = drafts;
		return drafts;
	}

	private static void Write( Draft.Of kind, List<Draft> drafts )
	{
		Kept[ kind ] = drafts;
		FileSystem.Data.CreateDirectory( Folder );
		FileSystem.Data.WriteJson( FileOf( kind ), drafts );
		Revision++;
	}
}
