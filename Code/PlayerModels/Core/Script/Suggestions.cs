using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>"Did you mean ...?": the known name closest to a mistyped one, by edit distance, ignoring case.</summary>
public static class Suggestions
{
	/// <summary>The closest of <paramref name="candidates"/> to <paramref name="name"/>, for "did you mean"; null when none is close.</summary>
	public static string Closest( string name, IEnumerable<string> candidates )
	{
		// A name of one or two letters is only matched by case: anything is a letter or two away from it
		string best = null;
		var bestDistance = (name.Length <= 2 ? 0 : Math.Max( 2, name.Length / 3 )) + 1;
		foreach ( var candidate in candidates )
		{
			var distance = string.Equals( candidate, name, StringComparison.OrdinalIgnoreCase ) ? 0 : Distance( name, candidate );
			if ( distance < bestDistance )
				(best, bestDistance) = (candidate, distance);
		}

		return best;
	}

	// Levenshtein distance, short strings only (names)
	private static int Distance( string a, string b )
	{
		if ( Math.Abs( a.Length - b.Length ) > 3 )
			return int.MaxValue;

		var previous = new int[ b.Length + 1 ];
		var current = new int[ b.Length + 1 ];
		for ( var j = 0; j <= b.Length; j++ )
			previous[ j ] = j;

		for ( var i = 1; i <= a.Length; i++ )
		{
			current[ 0 ] = i;
			for ( var j = 1; j <= b.Length; j++ )
			{
				var cost = char.ToLowerInvariant( a[ i - 1 ] ) == char.ToLowerInvariant( b[ j - 1 ] ) ? 0 : 1;
				current[ j ] = Math.Min( Math.Min( current[ j - 1 ] + 1, previous[ j ] + 1 ), previous[ j - 1 ] + cost );
			}

			(previous, current) = (current, previous);
		}

		return previous[ b.Length ];
	}
}
