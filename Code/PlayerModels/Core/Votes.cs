using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>
/// How a rating is counted. Votes are records players write themselves, so a modified client can write as many as it
/// likes: only a record under the one key a voter has for an item counts, once per voter, and never its author's own.
/// </summary>
public static class Votes
{
	/// <summary>The key of <paramref name="voter"/>'s vote on <paramref name="item"/>: keys are unique, so they have just this one.</summary>
	public static string KeyOf( long item, long voter ) => $"{item}:{voter}";

	/// <summary>How many players <paramref name="records"/> (each vote's owner and key) count for on <paramref name="item"/>, by <paramref name="author"/>.</summary>
	public static int Count( IEnumerable<(long Owner, string Key)> records, long item, long author )
	{
		var voters = new HashSet<long>();
		foreach ( var (owner, key) in records )
		{
			if ( owner != author && key == KeyOf( item, owner ) )
				voters.Add( owner );
		}

		return voters.Count;
	}
}
