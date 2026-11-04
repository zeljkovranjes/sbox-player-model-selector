using System.Collections.Generic;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// What was made for weapons (materials, models, meshes), kept by a key up to <see cref="Most"/> of them, each made for a
/// group (a design, a weapon, a prop) that bodies count as they are seen (see <see cref="Count"/>): when it is full what
/// the furthest players use goes first, then what was used longest ago, so what the weapons near you wear stays made. A
/// group nobody counted lately counts as furthest of all. Not for every frame: a lookup counts as a use, and making room
/// looks through them all.
/// </summary>
internal sealed class RecentCache<TKey, TValue>
{
	// A body's count holds this long: every scene showing bodies counts a few times a second, so one going quiet means
	// nobody shows that group any more
	private const float CountSeconds = 2f;

	private readonly Dictionary<TKey, (TValue Value, long Used, string Group)> _kept = new();
	private readonly Dictionary<string, (float Distance, float At)> _nearest = new();
	private long _uses;

	/// <summary>The most kept at once.</summary>
	internal int Most { get; }

	internal RecentCache( int most ) => Most = most;

	internal int Kept => _kept.Count;

	/// <summary>The value kept for <paramref name="key"/>, counted as used now; false when none is.</summary>
	internal bool TryGet( TKey key, out TValue value )
	{
		if ( !_kept.TryGetValue( key, out var kept ) )
		{
			value = default;
			return false;
		}

		_kept[ key ] = (kept.Value, ++_uses, kept.Group);
		value = kept.Value;
		return true;
	}

	/// <summary>
	/// Keeps <paramref name="value"/> for <paramref name="key"/>, made for <paramref name="group"/> (null for none), letting
	/// one go when full: the furthest players', then the one used longest ago.
	/// </summary>
	internal void Set( TKey key, TValue value, string group = null )
	{
		if ( !_kept.ContainsKey( key ) && _kept.Count >= Most )
			LetGoFurthest();

		_kept[ key ] = (value, ++_uses, group);
	}

	/// <summary>
	/// Counts <paramref name="group"/> as used by a body <paramref name="distance"/> from the camera, now. A few times a
	/// second for every body: no allocations once the group was counted before.
	/// </summary>
	internal void Count( string group, float distance )
	{
		if ( group is null )
			return;

		var now = RealTime.Now;
		if ( !_nearest.TryGetValue( group, out var counted ) || now - counted.At > CountSeconds / 2f || distance < counted.Distance )
			_nearest[ group ] = (distance, now);
	}

	internal void Clear()
	{
		_kept.Clear();
		_nearest.Clear();
	}

	// How far the nearest body using group is, as counted lately; as far as can be when none counted it
	private float DistanceOf( string group, float now ) =>
		group is not null && _nearest.TryGetValue( group, out var counted ) && now - counted.At <= CountSeconds ? counted.Distance : float.MaxValue;

	private void LetGoFurthest()
	{
		var now = RealTime.Now;
		var furthest = default( TKey );
		var found = false;
		var (distance, used) = (float.MinValue, long.MaxValue);
		foreach ( var (key, kept) in _kept )
		{
			var away = DistanceOf( kept.Group, now );
			if ( away > distance || away == distance && kept.Used < used )
				(furthest, distance, used, found) = (key, away, kept.Used, true);
		}

		if ( found )
			_kept.Remove( furthest );

		// Counts of groups nothing kept uses any more go too, so they never pile up
		if ( _nearest.Count > Most * 4 )
			_nearest.Clear();
	}
}
