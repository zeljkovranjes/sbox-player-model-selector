using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>
/// One player's downloads in a sliding window: at most a set number in any stretch of time that long. Times are in
/// seconds, on whatever clock the caller passes.
/// </summary>
public sealed class DownloadQuota
{
	private readonly List<double> _taken = new();

	/// <summary>
	/// Counts a download at <paramref name="now"/> if fewer than <paramref name="limit"/> were counted in the
	/// <paramref name="window"/> seconds before it. False, counting nothing, when the quota is used up.
	/// </summary>
	public bool TryTake( double now, int limit, double window )
	{
		Forget( now, window );
		if ( _taken.Count >= limit )
			return false;

		_taken.Add( now );
		return true;
	}

	/// <summary>Seconds from <paramref name="now"/> until another download fits; 0 when one does now.</summary>
	public double SecondsUntilFree( double now, int limit, double window )
	{
		Forget( now, window );
		if ( _taken.Count < limit )
			return 0;

		// The download that has to fall out of the window first, with the limit maybe lowered since they were taken
		var oldest = _taken[ _taken.Count - limit < 0 ? 0 : _taken.Count - limit ];
		return oldest + window - now;
	}

	private void Forget( double now, double window )
	{
		_taken.RemoveAll( x => now - x >= window );
	}
}
