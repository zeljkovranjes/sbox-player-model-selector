using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Answers from sbox.game kept in a file on this machine for a while, so the same question isn't asked again on every
/// start or join: an entry is used until it is older than the lifetime, then asked again. Holds at most a set number of
/// entries, dropping the oldest.
/// </summary>
internal sealed class TimedCache<T>
{
	internal sealed class Entry
	{
		public T Value { get; set; }
		public long Saved { get; set; }
	}

	private readonly string _file;
	private readonly TimeSpan _lifetime;
	private readonly int _maxEntries;
	private Dictionary<string, Entry> _entries;

	internal TimedCache( string file, TimeSpan lifetime, int maxEntries )
	{
		_file = file;
		_lifetime = lifetime;
		_maxEntries = maxEntries;
	}

	/// <summary>The value kept for <paramref name="key"/>, when there is one and it isn't too old.</summary>
	internal bool TryGet( string key, out T value )
	{
		value = default;
		if ( !Entries().TryGetValue( key, out var entry ) || Now() - entry.Saved > (long)_lifetime.TotalSeconds )
			return false;

		value = entry.Value;
		return true;
	}

	/// <summary>Keeps <paramref name="value"/> for <paramref name="key"/>, from now.</summary>
	internal void Set( string key, T value )
	{
		var entries = Entries();
		entries[ key ] = new Entry { Value = value, Saved = Now() };

		// Named methods, not lambdas: a generic type's lambda can't be carried over a hot reload
		foreach ( var oldest in entries.OrderBy( SavedAt ).Take( entries.Count - _maxEntries ).Select( KeyOf ).ToArray() )
			entries.Remove( oldest );

		FileSystem.Data.WriteJson( _file, entries );
	}

	private static long SavedAt( KeyValuePair<string, Entry> entry ) => entry.Value.Saved;

	private static string KeyOf( KeyValuePair<string, Entry> entry ) => entry.Key;

	private Dictionary<string, Entry> Entries()
	{
		return _entries ??= FileSystem.Data.ReadJsonOrDefault( _file, new Dictionary<string, Entry>() ) ?? new();
	}

	private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
