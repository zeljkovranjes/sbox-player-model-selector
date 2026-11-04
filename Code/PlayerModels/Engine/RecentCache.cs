using System.Collections.Generic;

namespace PlayerModels.Engine;

/// <summary>
/// What was made for weapons (materials, models, meshes), kept by a key up to <see cref="Most"/> of them: when it is full
/// the one used longest ago goes, so what the weapons near you wear now stays made. Not for every frame: a lookup counts
/// as a use, and making room looks through them all.
/// </summary>
internal sealed class RecentCache<TKey, TValue>
{
	private readonly Dictionary<TKey, (TValue Value, long Used)> _kept = new();
	private long _uses;

	/// <summary>The most kept at once.</summary>
	internal int Most { get; }

	internal RecentCache( int most ) => Most = most;

	internal int Count => _kept.Count;

	/// <summary>The value kept for <paramref name="key"/>, counted as used now; false when none is.</summary>
	internal bool TryGet( TKey key, out TValue value )
	{
		if ( !_kept.TryGetValue( key, out var kept ) )
		{
			value = default;
			return false;
		}

		_kept[ key ] = (kept.Value, ++_uses);
		value = kept.Value;
		return true;
	}

	/// <summary>Keeps <paramref name="value"/> for <paramref name="key"/>, letting the one used longest ago go when full.</summary>
	internal void Set( TKey key, TValue value )
	{
		if ( !_kept.ContainsKey( key ) && _kept.Count >= Most )
			LetGoOldest();

		_kept[ key ] = (value, ++_uses);
	}

	internal void Clear() => _kept.Clear();

	private void LetGoOldest()
	{
		var oldest = default( TKey );
		var found = false;
		var at = long.MaxValue;
		foreach ( var (key, kept) in _kept )
		{
			if ( kept.Used < at )
			{
				(oldest, at, found) = (key, kept.Used, true);
			}
		}

		if ( found )
			_kept.Remove( oldest );
	}
}
