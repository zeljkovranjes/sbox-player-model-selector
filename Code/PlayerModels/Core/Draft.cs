using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using PlayerModels.Core.Script;

namespace PlayerModels.Core;

/// <summary>
/// Unfinished work kept to come back to: an effect, a shape (a sticker or an emblem too), a weapon design or a script as
/// its editor had it (<see cref="Item"/>, as text, whether or not it is finished or compiles), with how the editor was
/// left (<see cref="State"/>: the layer selected, the tool picked; for a script, the effect it was for), its name and when
/// it was last edited. Kept on this machine
/// only: never worn, sent or published. A kind keeps at most <see cref="MaxPerKind"/>, the newest first (see
/// <see cref="Put"/>).
/// </summary>
public sealed class Draft
{
	/// <summary>What a draft is of.</summary>
	public enum Of { Effect, Shape, Weapon, Script }

	/// <summary>The most drafts of one kind kept.</summary>
	public const int MaxPerKind = 50;

	/// <summary>
	/// The longest a draft's item may be, as text: as long as the longest item of its kind shared (an effect, an emblem or a
	/// weapon design, each 32 KB at most; see <see cref="MaxItemLengthOf"/> for a script), and its editor's state.
	/// </summary>
	public const int MaxItemLength = 32 * 1024;
	public const int MaxStateLength = 4 * 1024;

	/// <summary>The longest a draft of <paramref name="kind"/> may be: a script as long as the code editor opens.</summary>
	public static int MaxItemLengthOf( Of kind ) => kind == Of.Script ? ScriptLimits.MaxSourceLength : MaxItemLength;

	/// <summary>The longest name.</summary>
	public const int MaxNameLength = 32;

	// An id is the time it was first kept and a count, in hex: unique on this machine, which is all a draft needs
	private const int MaxIdLength = 32;

	/// <summary>Which draft it is, the same across saves of it.</summary>
	[JsonPropertyName( "id" )]
	public string Id { get; set; } = "";

	/// <summary>What it is of, an <see cref="Of"/> by name.</summary>
	[JsonPropertyName( "k" )]
	public string Kind { get; set; } = nameof( Of.Effect );

	/// <summary>What it is called.</summary>
	[JsonPropertyName( "n" )]
	public string Name { get; set; } = "";

	/// <summary>When it was last saved, in seconds since 1970 (UTC).</summary>
	[JsonPropertyName( "t" )]
	public long Edited { get; set; }

	/// <summary>The work itself, as its editor writes it (an effect, a shape or a design as JSON; a script as its text).</summary>
	[JsonPropertyName( "i" )]
	public string Item { get; set; } = "";

	/// <summary>How its editor was left, as the editor writes it; null for nothing to restore.</summary>
	[JsonPropertyName( "s" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string State { get; set; }

	/// <summary>What it is of; null for a kind this version doesn't know.</summary>
	[JsonIgnore]
	public Of? KindIs => Enum.TryParse<Of>( Kind, false, out var kind ) && Enum.IsDefined( kind ) ? kind : null;

	/// <summary>
	/// A copy within bounds, or null for one of a kind this version doesn't know, with no id or work, or whose work or
	/// state is longer than a draft may be (checked by length before anything is read).
	/// </summary>
	public Draft Sanitized()
	{
		if ( KindIs is not { } kind || string.IsNullOrWhiteSpace( Id ) || Id.Length > MaxIdLength || string.IsNullOrEmpty( Item )
			|| Item.Length > MaxItemLengthOf( kind ) || State is { Length: > MaxStateLength } )
			return null;

		var name = SharePost.Tidy( Name, MaxNameLength );
		return new Draft { Id = Id, Kind = kind.ToString(), Name = name == "" ? "Untitled" : name, Edited = Math.Max( 0, Edited ), Item = Item, State = State };
	}

	/// <summary>A new id for a draft first kept at <paramref name="now"/> (seconds), the <paramref name="count"/>th this session.</summary>
	public static string NewId( long now, int count ) => $"{now:x}{count:x}";

	/// <summary>
	/// <paramref name="drafts"/> (of one kind, the newest first) with <paramref name="draft"/> kept: in place of the one with
	/// its id, moved to the front, or added at the front. Null when it is new and the kind is full; with
	/// <paramref name="dropOldest"/> the oldest is let go of instead, and named in <paramref name="dropped"/>.
	/// </summary>
	public static List<Draft> Put( List<Draft> drafts, Draft draft, bool dropOldest, out Draft dropped )
	{
		dropped = null;
		var kept = new List<Draft>( drafts );
		var at = kept.FindIndex( x => x.Id == draft.Id );
		if ( at >= 0 )
			kept.RemoveAt( at );
		else if ( kept.Count >= MaxPerKind )
		{
			if ( !dropOldest )
				return null;

			dropped = Oldest( kept );
			kept.Remove( dropped );
		}

		kept.Insert( 0, draft );
		return kept;
	}

	/// <summary>The draft of <paramref name="drafts"/> edited longest ago; null for none.</summary>
	public static Draft Oldest( List<Draft> drafts )
	{
		Draft oldest = null;
		foreach ( var draft in drafts )
		{
			if ( oldest is null || draft.Edited < oldest.Edited )
				oldest = draft;
		}

		return oldest;
	}

	/// <summary>When it was last edited, in plain words, at <paramref name="now"/> (seconds): "edited 5 min ago".</summary>
	public string EditedText( long now )
	{
		var seconds = Math.Max( 0, now - Edited );
		return seconds switch
		{
			< 60 => "edited just now",
			< 3600 => $"edited {seconds / 60} min ago",
			< 86400 => $"edited {seconds / 3600} h ago",
			_ => $"edited {seconds / 86400} {(seconds / 86400 == 1 ? "day" : "days")} ago",
		};
	}
}
