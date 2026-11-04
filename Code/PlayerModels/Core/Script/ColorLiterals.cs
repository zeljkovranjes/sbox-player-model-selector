using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlayerModels.Core.Script;

/// <summary>How a colour literal is written: hex text, red-green-blue numbers, hue-saturation-value, or a name.</summary>
public enum ColorForm { Hex, Rgb, Hsv, Named }

/// <summary>A colour written in a script: where (from <c>Color</c> to its closing bracket), its colour, and its form.</summary>
public readonly record struct ColorLiteral( int Start, int Length, double R, double G, double B, double A, ColorForm Form );

/// <summary>
/// The colours written in a script, for the code editor's swatches: <c>Color.Parse( "#..." )</c>, <c>Color( r, g, b[, a] )</c>,
/// <c>Color.FromHsv( h, s, v[, a] )</c> and the named ones (<c>Color.Red</c>), palette entries included. Read from the
/// tokens alone, so swatches show while the rest of the script is still being typed. Picking a colour rewrites just
/// that literal, keeping its form.
/// </summary>
public static class ColorLiterals
{
	/// <summary>Every colour literal in <paramref name="source"/>, in order (up to the first thing that doesn't read).</summary>
	public static List<ColorLiteral> Find( string source )
	{
		var found = new List<ColorLiteral>();
		if ( source is null || source.Length > ScriptLimits.MaxSourceLength )
			return found;

		var tokens = Significant( Lexer.All( source, new LineMap( source ), out _ ) );
		for ( var k = 0; k < tokens.Count; k++ )
		{
			if ( !IsName( source, tokens[ k ], "Color" ) || k > 0 && tokens[ k - 1 ].Kind == TokenKind.Dot )
				continue;

			if ( TryRead( source, tokens, k, out var literal ) )
				found.Add( literal );
		}

		return found;
	}

	/// <summary>The colour literal covering <paramref name="offset"/>, if there is one.</summary>
	public static ColorLiteral? At( string source, int offset )
	{
		foreach ( var literal in Find( source ) )
		{
			if ( offset >= literal.Start && offset < literal.Start + literal.Length )
				return literal;
		}

		return null;
	}

	/// <summary><paramref name="source"/> with <paramref name="literal"/> rewritten as the colour r, g, b, a (0 to 1), in its own form.</summary>
	public static string Replace( string source, ColorLiteral literal, double r, double g, double b, double a ) =>
		source[ ..literal.Start ] + Write( source.Substring( literal.Start, literal.Length ), literal.Form, r, g, b, a ) + source[ (literal.Start + literal.Length).. ];

	/// <summary>The colour r, g, b, a as a literal in <paramref name="form"/> (a name can't be kept: it becomes hex), like <paramref name="old"/>.</summary>
	public static string Write( string old, ColorForm form, double r, double g, double b, double a )
	{
		var hasAlpha = a < 1 || CountArguments( old ) == 4;
		switch ( form )
		{
			case ColorForm.Rgb:
				return hasAlpha ? $"Color( {N( r )}, {N( g )}, {N( b )}, {N( a )} )" : $"Color( {N( r )}, {N( g )}, {N( b )} )";
			case ColorForm.Hsv:
			{
				var (h, s, v) = ToHsv( r, g, b );
				var hue = Math.Round( h, 1 ).ToString( "0.#", CultureInfo.InvariantCulture );
				return hasAlpha ? $"Color.FromHsv( {hue}, {N( s )}, {N( v )}, {N( a )} )" : $"Color.FromHsv( {hue}, {N( s )}, {N( v )} )";
			}

			default:
			{
				var hex = ScriptValue.ColorText( r, g, b, a );
				if ( form == ColorForm.Hex && HasUpper( old ) )
					hex = hex.ToUpperInvariant();

				return $"Color.Parse( \"{hex}\" )";
			}
		}
	}

	private static string N( double value ) => Math.Round( Math.Clamp( value, 0, 1 ), 3 ).ToString( "0.###", CultureInfo.InvariantCulture );

	private static bool HasUpper( string text )
	{
		var quote = text.IndexOf( '"' );
		for ( var i = Math.Max( quote, 0 ); i < text.Length; i++ )
		{
			if ( text[ i ] is >= 'A' and <= 'F' )
				return true;
		}

		return false;
	}

	private static int CountArguments( string text )
	{
		var commas = 0;
		foreach ( var c in text )
			commas += c == ',' ? 1 : 0;

		return commas + 1;
	}

	/// <summary>Hue (degrees), saturation and value of r, g, b.</summary>
	public static (double H, double S, double V) ToHsv( double r, double g, double b )
	{
		var max = Math.Max( r, Math.Max( g, b ) );
		var min = Math.Min( r, Math.Min( g, b ) );
		var delta = max - min;
		var h = delta == 0 ? 0 : max == r ? 60 * ((g - b) / delta % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
		return (h < 0 ? h + 360 : h, max == 0 ? 0 : delta / max, max);
	}

	private static List<Token> Significant( List<Token> tokens )
	{
		var significant = new List<Token>( tokens.Count );
		foreach ( var token in tokens )
		{
			if ( token.Kind != TokenKind.Comment )
				significant.Add( token );
		}

		return significant;
	}

	private static bool IsName( string source, Token token, string name ) =>
		token.Kind == TokenKind.Name && token.Length == name.Length && string.CompareOrdinal( source, token.Start, name, 0, name.Length ) == 0;

	private static bool Is( List<Token> tokens, int k, TokenKind kind ) => k < tokens.Count && tokens[ k ].Kind == kind;

	private static bool TryRead( string source, List<Token> tokens, int k, out ColorLiteral literal )
	{
		literal = default;
		var start = tokens[ k ].Start;
		if ( Is( tokens, k + 1, TokenKind.Dot ) && Is( tokens, k + 2, TokenKind.Name ) )
		{
			var member = source.Substring( tokens[ k + 2 ].Start, tokens[ k + 2 ].Length );
			if ( member == "Parse" && Is( tokens, k + 3, TokenKind.LeftParen ) && Is( tokens, k + 4, TokenKind.String ) && Is( tokens, k + 5, TokenKind.RightParen )
				&& ScriptMath.TryParseColor( Lexer.TextOf( source, tokens[ k + 4 ] ), out var r, out var g, out var b, out var a ) )
			{
				literal = new ColorLiteral( start, tokens[ k + 5 ].End - start, r, g, b, a, ColorForm.Hex );
				return true;
			}

			if ( member == "FromHsv" && Is( tokens, k + 3, TokenKind.LeftParen ) && Numbers( source, tokens, k + 4, out var hsv, out var end ) )
			{
				var (red, green, blue) = ScriptMath.FromHsv( hsv[ 0 ], hsv[ 1 ], hsv[ 2 ] );
				literal = new ColorLiteral( start, end - start, red, green, blue, hsv[ 3 ], ColorForm.Hsv );
				return true;
			}

			foreach ( var (name, nr, ng, nb, na) in ScriptLibrary.NamedColors )
			{
				if ( name == member )
				{
					literal = new ColorLiteral( start, tokens[ k + 2 ].End - start, nr, ng, nb, na, ColorForm.Named );
					return true;
				}
			}

			return false;
		}

		if ( Is( tokens, k + 1, TokenKind.LeftParen ) && Numbers( source, tokens, k + 2, out var rgba, out var close ) )
		{
			literal = new ColorLiteral( start, close - start, rgba[ 0 ], rgba[ 1 ], rgba[ 2 ], rgba[ 3 ], ColorForm.Rgb );
			return true;
		}

		return false;
	}

	// Three or four numbers separated by commas, then ')': their values (alpha 1 when left out) and where ')' ends
	private static bool Numbers( string source, List<Token> tokens, int k, out double[] values, out int end )
	{
		values = new[] { 0.0, 0, 0, 1 };
		end = 0;
		for ( var i = 0; i < 4; i++ )
		{
			if ( !Is( tokens, k, TokenKind.Number ) )
				return false;

			values[ i ] = Lexer.NumberOf( source, tokens[ k ] );
			if ( Is( tokens, k + 1, TokenKind.RightParen ) && i >= 2 )
			{
				end = tokens[ k + 1 ].End;
				return true;
			}

			if ( !Is( tokens, k + 1, TokenKind.Comma ) )
				return false;

			k += 2;
		}

		return false;
	}
}
