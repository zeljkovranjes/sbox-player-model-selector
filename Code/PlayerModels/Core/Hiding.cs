using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlayerModels.Core;

/// <summary>
/// What of a model may be hidden: an accessory or a piece of clothing may, the body itself never, and never everything,
/// so no player can make themselves invisible to the others. Checked on every client.
/// </summary>
public static class Hiding
{
	// Words in the names of a body's own groups and materials: what stays, whatever a look says
	private static readonly HashSet<string> BodyWords = new( StringComparer.OrdinalIgnoreCase )
	{
		"head", "chest", "torso", "body", "leg", "legs", "arm", "arms", "hand", "hands", "foot", "feet", "default", "base",
		"skin",
	};

	private static readonly Regex NameWords = new( "[^a-zA-Z]+" );

	/// <summary>Whether what is called <paramref name="name"/> (a body group, a part) may be hidden, told by the words of its name.</summary>
	public static bool CanHide( string name )
	{
		return !string.IsNullOrEmpty( name ) && !NameWords.Split( name ).Any( BodyWords.Contains );
	}

	/// <summary>
	/// Which of <paramref name="parts"/> (a model's materials) really are hidden by <paramref name="finishes"/>: those given
	/// <see cref="MaterialFinish.Hidden"/> that may be, except that the first part stays if that would hide them all.
	/// </summary>
	public static HashSet<string> HiddenParts( IReadOnlyList<string> parts, IReadOnlyDictionary<string, string> finishes )
	{
		var hidden = new HashSet<string>();
		foreach ( var part in parts )
		{
			if ( finishes.TryGetValue( part, out var finish ) && finish == MaterialFinish.Hidden && CanHide( part ) )
				hidden.Add( part );
		}

		if ( parts.Count > 0 && parts.All( hidden.Contains ) )
			hidden.Remove( parts[ 0 ] );

		return hidden;
	}
}
