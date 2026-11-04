using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// Something wrong with a script, in plain words, and where: the characters it is about (<see cref="Start"/>,
/// <see cref="Length"/>) and their line and column, both counted from 1, for the editor's squiggle and error list.
/// </summary>
public sealed class ScriptError
{
	public string Message { get; }
	public int Start { get; }
	public int Length { get; }
	public int Line { get; }
	public int Column { get; }

	public ScriptError( string message, int start, int length, int line, int column )
	{
		Message = message;
		Start = start;
		Length = length;
		Line = line;
		Column = column;
	}

	/// <summary>"line:col: message", as the error list shows it.</summary>
	public override string ToString() => $"{Line}:{Column}: {Message}";
}

/// <summary>Where each line of a source starts, to turn a character offset into a line and column.</summary>
public sealed class LineMap
{
	private readonly List<int> _starts = new() { 0 };

	public LineMap( string source )
	{
		for ( var i = 0; i < source.Length; i++ )
		{
			if ( source[ i ] == '\n' )
				_starts.Add( i + 1 );
		}
	}

	/// <summary>How many lines there are.</summary>
	public int Count => _starts.Count;

	/// <summary>The line (from 1) and column (from 1) of <paramref name="offset"/>.</summary>
	public (int Line, int Column) Position( int offset )
	{
		var index = _starts.BinarySearch( Math.Max( offset, 0 ) );
		if ( index < 0 )
			index = ~index - 1;

		return (index + 1, offset - _starts[ index ] + 1);
	}

	/// <summary>Where line <paramref name="line"/> (from 1) starts.</summary>
	public int StartOf( int line ) => _starts[ Math.Clamp( line - 1, 0, _starts.Count - 1 ) ];

	/// <summary>An error at <paramref name="start"/> for <paramref name="length"/> characters.</summary>
	public ScriptError Error( string message, int start, int length )
	{
		var (line, column) = Position( start );
		return new ScriptError( message, start, Math.Max( length, 1 ), line, column );
	}
}
