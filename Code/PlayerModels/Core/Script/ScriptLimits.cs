using System.Collections.Generic;
using System.Globalization;

namespace PlayerModels.Core.Script;

/// <summary>
/// Every cap a script is held to. Scripts come from other players (looks, the Workshop), so each cap is checked before
/// the work it guards: lengths before any parsing, counts while parsing and compiling, instructions while running. A
/// script over any of them is refused whole, never run in part.
/// </summary>
public static class ScriptLimits
{
	/// <summary>The most UTF-8 bytes one script has, measured on its minified (stored) form.</summary>
	public const int MaxScriptBytes = 16 * 1024;

	/// <summary>The most UTF-8 bytes all the scripts of one look have together, minified.</summary>
	public const int MaxLookBytes = 32 * 1024;

	/// <summary>
	/// The longest text the compiler looks at, formatted or not: refused on its length alone, before anything else, so a
	/// million lines cost as much as one. Formatting adds indentation, so it is above <see cref="MaxScriptBytes"/>.
	/// </summary>
	public const int MaxSourceLength = 64 * 1024;

	/// <summary>
	/// The most tokens and syntax nodes (one per stored byte, so any script within <see cref="MaxScriptBytes"/> fits),
	/// and how deep blocks, brackets and expressions nest.
	/// </summary>
	public const int MaxTokens = 16 * 1024;
	public const int MaxNodes = 16 * 1024;
	public const int MaxDepth = 48;

	/// <summary>The most functions, value slots for one function's locals, and for the script's own variables.</summary>
	public const int MaxFunctions = 64;
	public const int MaxLocals = 128;
	public const int MaxGlobals = 256;

	/// <summary>The most constant values, list slots, and instructions a script compiles to.</summary>
	public const int MaxConstants = 4096;
	public const int MaxListSlots = 4096;
	public const int MaxCode = 32 * 1024;

	/// <summary>The longest string literal, number literal and line, in characters.</summary>
	public const int MaxStringLength = 256;
	public const int MaxNumberLength = 32;
	public const int MaxLineLength = 4096;

	/// <summary>The most items a list literal has.</summary>
	public const int MaxListLength = 256;

	/// <summary>
	/// The most list slots a script's lists hold together once it is built: its written lists, those List.Create made and
	/// the palettes it read while building.
	/// </summary>
	public const int MaxArenaSlots = 16 * 1024;

	/// <summary>The largest number a literal or a result can be; past it a result is held there.</summary>
	public const double MaxNumber = 1e15;

	/// <summary>How deep calls go while running.</summary>
	public const int MaxCallDepth = 32;

	/// <summary>
	/// How many instructions building an effect or a shape may take (once, when the look arrives), and one run of a
	/// function, event or element (each frame) by default; a host can only lower the second.
	/// </summary>
	public const int BuildInstructions = 200_000;
	public const int RunInstructions = 10_000;

	/// <summary>The most errors one compile reports: past them the rest are noise.</summary>
	public const int MaxErrors = 32;

	/// <summary>Whether <paramref name="script"/> (stored, minified) is within <see cref="MaxScriptBytes"/>: by its length first.</summary>
	public static bool Fits( string script ) => script is not null && script.Length <= MaxScriptBytes && Utf8Bytes( script ) <= MaxScriptBytes;

	/// <summary>The UTF-8 size of <paramref name="text"/>, counted without encoding it.</summary>
	public static int Utf8Bytes( string text ) => text is null ? 0 : Utf8Bytes( text, 0, text.Length );

	/// <summary>The UTF-8 size of <paramref name="length"/> characters of <paramref name="text"/> from <paramref name="start"/>.</summary>
	public static int Utf8Bytes( string text, int start, int length )
	{
		var bytes = 0;
		var end = start + length;
		for ( var i = start; i < end; i++ )
		{
			var c = text[ i ];
			if ( c < 0x80 )
				bytes++;
			else if ( c < 0x800 )
				bytes += 2;
			else if ( char.IsHighSurrogate( c ) && i + 1 < end && char.IsLowSurrogate( text[ i + 1 ] ) )
			{
				bytes += 4;
				i++;
			}
			else
				bytes += 3;
		}

		return bytes;
	}

	/// <summary>Whether the scripts of one look (each stored, minified) are within <see cref="MaxLookBytes"/> together.</summary>
	public static bool LookFits( IEnumerable<string> scripts )
	{
		var total = 0;
		foreach ( var script in scripts )
		{
			if ( script is null )
				continue;

			if ( script.Length > MaxLookBytes )
				return false;

			total += Utf8Bytes( script );
			if ( total > MaxLookBytes )
				return false;
		}

		return true;
	}

	/// <summary>A size for people: "12.1 KB of 16 KB".</summary>
	public static string Describe( int bytes, int cap ) => (bytes / 1024.0).ToString( "0.0", CultureInfo.InvariantCulture ) + " KB of " + cap / 1024 + " KB";
}
