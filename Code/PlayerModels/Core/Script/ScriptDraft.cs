using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// A script left unfinished in the code editor ("Keep as draft"), kept on this machine only to come back to: its name,
/// its text exactly as it was left (even when it doesn't compile yet), the effect it was for (by id; another is made when
/// that one is gone) and when it was last edited. Drafts are never worn, sent or published.
/// </summary>
public sealed class ScriptDraft
{
	/// <summary>The most drafts kept, and the longest a draft's text is (the longest script the editor opens).</summary>
	public const int MaxDrafts = 50;
	public const int MaxLength = ScriptLimits.MaxSourceLength;

	public string Name { get; set; } = "";
	public string Script { get; set; } = "";
	public string EffectId { get; set; }

	/// <summary>When it was last kept, in Unix seconds.</summary>
	public long Edited { get; set; }

	/// <summary>A copy within bounds, or null for one without a name or a text too long to be a script.</summary>
	public ScriptDraft Sanitized()
	{
		var name = Name?.Trim() ?? "";
		if ( name.Length == 0 || Script is null || Script.Length > MaxLength )
			return null;

		return new ScriptDraft
		{
			Name = name.Length <= Effect.MaxNameLength ? name : name[ ..Effect.MaxNameLength ],
			Script = Script,
			EffectId = EffectId is { Length: > 0 and <= Effect.MaxIdLength } ? EffectId : null,
			Edited = Math.Max( 0, Edited ),
		};
	}

	/// <summary>
	/// <paramref name="drafts"/> with <paramref name="draft"/> kept (in place of one of its name), newest first, within
	/// <see cref="MaxDrafts"/>: <paramref name="dropped"/> names the oldest let go to make room, or null. A draft that
	/// isn't one changes nothing.
	/// </summary>
	public static List<ScriptDraft> Keep( IEnumerable<ScriptDraft> drafts, ScriptDraft draft, out string dropped )
	{
		dropped = null;
		var kept = Within( drafts );
		if ( draft?.Sanitized() is not { } saved )
			return kept;

		kept.RemoveAll( x => x.Name == saved.Name );
		kept.Insert( 0, saved );
		if ( kept.Count > MaxDrafts )
		{
			dropped = kept[ ^1 ].Name;
			kept.RemoveAt( kept.Count - 1 );
		}

		return kept;
	}

	/// <summary><paramref name="drafts"/> within bounds: each one that reads, a name once, newest first, at most <see cref="MaxDrafts"/>.</summary>
	public static List<ScriptDraft> Within( IEnumerable<ScriptDraft> drafts )
	{
		var kept = new List<ScriptDraft>();
		foreach ( var draft in drafts ?? Array.Empty<ScriptDraft>() )
		{
			if ( draft?.Sanitized() is { } saved && !kept.Exists( x => x.Name == saved.Name ) )
				kept.Add( saved );
		}

		kept.Sort( ( a, b ) => b.Edited.CompareTo( a.Edited ) );
		if ( kept.Count > MaxDrafts )
			kept.RemoveRange( MaxDrafts, kept.Count - MaxDrafts );

		return kept;
	}

	/// <summary>How long ago it was edited, in plain words: "edited just now", "edited 5 min ago", "edited 3 days ago".</summary>
	public static string Age( long edited, long now )
	{
		var seconds = Math.Max( 0, now - edited );
		return seconds switch
		{
			< 60 => "edited just now",
			< 3600 => $"edited {seconds / 60} min ago",
			< 86400 => $"edited {seconds / 3600} h ago",
			_ => $"edited {seconds / 86400} day{(seconds / 86400 == 1 ? "" : "s")} ago",
		};
	}
}
