using System;
using System.Collections.Generic;
using System.Text;

namespace PlayerModels.Core.Script;

/// <summary>
/// Scripts compiled once and kept by their text, so a look worn again, or the same script on a hundred players, costs one
/// compile: keyed by the source, its kind and the names of the player's library it may use (those decide whether it
/// compiles). Failures are kept too, so garbage isn't compiled again each time a look arrives. Only what running needs
/// is kept (the program or the errors, not the syntax tree), and at most <see cref="MostKept"/> scripts, the least
/// recently used let go first.
/// </summary>
public sealed class ScriptCache
{
	/// <summary>The most compiled scripts kept: a full server's worth of different ones.</summary>
	public const int MostKept = 128;

	private sealed class Entry
	{
		public CompileResult Result;
		public long Used;
	}

	private readonly Dictionary<string, Entry> _entries = new();
	private long _clock;

	/// <summary>How many compiled scripts it keeps now.</summary>
	public int Count => _entries.Count;

	/// <summary>Whether <paramref name="source"/> is compiled already (asking costs no compile).</summary>
	public bool Has( string source, ScriptKind kind = ScriptKind.Effect, IPlayerLibrary library = null ) =>
		source is not null && _entries.ContainsKey( Key( source, kind, library ) );

	/// <summary>
	/// <paramref name="source"/> compiled as a script of <paramref name="kind"/> naming <paramref name="library"/>: kept
	/// from before when it was compiled already, else compiled now and kept. Never throws.
	/// </summary>
	public CompileResult Compile( string source, ScriptKind kind = ScriptKind.Effect, IPlayerLibrary library = null )
	{
		if ( source is null )
			return ScriptCompiler.Compile( null );

		var key = Key( source, kind, library );
		if ( _entries.TryGetValue( key, out var entry ) )
		{
			entry.Used = ++_clock;
			return entry.Result;
		}

		var compiled = ScriptCompiler.Compile( source, kind, library );
		var kept = new CompileResult { Program = compiled.Program, Errors = compiled.Errors, StoredBytes = compiled.StoredBytes };
		if ( _entries.Count >= MostKept )
			LetGoOfOldest();

		_entries[ key ] = new Entry { Result = kept, Used = ++_clock };
		return kept;
	}

	/// <summary>Lets go of every compiled script.</summary>
	public void Clear() => _entries.Clear();

	private void LetGoOfOldest()
	{
		string oldest = null;
		var used = long.MaxValue;
		foreach ( var (key, entry) in _entries )
		{
			if ( entry.Used < used )
				(oldest, used) = (key, entry.Used);
		}

		if ( oldest is not null )
			_entries.Remove( oldest );
	}

	// The kind, then the library's names by kind (they decide what compiles), then the source
	private static string Key( string source, ScriptKind kind, IPlayerLibrary library )
	{
		var key = new StringBuilder( source.Length + 64 );
		key.Append( (int)kind ).Append( '\n' );
		foreach ( var item in library?.Items ?? Array.Empty<LibraryItem>() )
			key.Append( (int)item.Kind ).Append( item.Name ).Append( '\u0001' );

		return key.Append( '\n' ).Append( source ).ToString();
	}
}
