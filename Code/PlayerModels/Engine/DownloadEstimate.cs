using System;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// How far along a package download probably is. The engine tells addons nothing of a download in progress, so this
/// goes by the package's size and the speed downloads went at before, and is only ever an estimate: it creeps towards
/// the end as time passes and is full when the download is done.
/// </summary>
internal sealed class DownloadEstimate
{
	// The speed assumed before any download is timed, in bytes a second
	private const float AssumedSpeed = 4f * 1024 * 1024;

	// The share of the bar the estimate reaches at the expected time; past it, the bar only creeps on
	private const float ExpectedShare = 0.85f;

	// Downloads shorter than this say little about the speed (most of it was already on the machine)
	private const float ShortestTimed = 1f;

	private float _speed = AssumedSpeed;
	private float _bytes;
	private float _expected;
	private RealTimeSince _since;

	/// <summary>The model being downloaded; null when none is.</summary>
	internal string Model { get; private set; }

	/// <summary>Starts estimating the download of <paramref name="model"/>, <paramref name="bytes"/> long (0 when not known).</summary>
	internal void Start( string model, long bytes )
	{
		Model = model;
		_bytes = Math.Max( 0, bytes );
		_expected = Math.Max( ShortestTimed, _bytes / _speed );
		_since = 0;
	}

	/// <summary>Ends the download; one long enough to time sets the speed later estimates assume.</summary>
	internal void Finish()
	{
		if ( Model is not null && _since >= ShortestTimed && _bytes > 0 )
			_speed = Math.Max( 64f * 1024, _bytes / _since );

		Model = null;
	}

	/// <summary>How far along the download probably is, 0 to 1; 0 when there is none.</summary>
	internal float Fraction
	{
		get
		{
			if ( Model is null )
				return 0f;

			var progress = _since / _expected;
			if ( progress <= 1f )
				return progress * ExpectedShare;

			// Past the expected time: on towards the end, ever slower, never quite there
			return ExpectedShare + (1f - ExpectedShare) * (1f - MathF.Exp( 1f - progress ));
		}
	}
}
