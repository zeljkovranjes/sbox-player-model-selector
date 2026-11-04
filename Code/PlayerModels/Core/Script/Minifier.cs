using System.Text;

namespace PlayerModels.Core.Script;

/// <summary>
/// Writes a script as it is stored, sent and published: no indentation, the shortest separators within a line, one
/// line break between statements, and <c>;</c> for a blank line between paragraphs. Names are never changed and
/// comments are kept (readers of a fork need them); <see cref="Formatter"/> gives the script back as written.
/// </summary>
public static class Minifier
{
	/// <summary>
	/// <paramref name="source"/> minified; when it doesn't parse, just trimmed line by line (so a script mid-edit is
	/// still kept), or null when it is too long to read.
	/// </summary>
	public static string Minify( string source )
	{
		if ( source is null || source.Length > ScriptLimits.MaxSourceLength )
			return null;

		var tree = Parser.Parse( source );
		if ( tree.Error is null )
			return Minify( tree );

		var lines = source.Split( '\n' );
		for ( var i = 0; i < lines.Length; i++ )
			lines[ i ] = lines[ i ].Trim();

		return string.Join( "\n", lines ).Trim();
	}

	/// <summary>A parsed script minified.</summary>
	public static string Minify( SyntaxTree tree )
	{
		var text = new StringBuilder( tree.Source.Length );
		Write( tree, text );
		return text.ToString();
	}

	/// <summary>How many UTF-8 bytes a parsed script takes minified, worked out without writing it.</summary>
	public static int StoredBytes( SyntaxTree tree ) => Write( tree, null );

	// Writes the minified script into text (or, with none, only counts it), giving its UTF-8 size
	private static int Write( SyntaxTree tree, StringBuilder text )
	{
		var source = tree.Source;
		var bytes = 0;
		var last = '\0';
		Token previous = default;
		var hasPrevious = false;
		var lineCommentOpen = false;
		foreach ( var placed in TokenLayout.Of( tree ) )
		{
			var token = tree.Tokens[ placed.Index ];
			if ( lineCommentOpen )
				Put( text, '\n', ref bytes, ref last );

			if ( placed.NewLine && bytes > 0 )
			{
				if ( placed.Blank )
					Put( text, ';', ref bytes, ref last );

				if ( (!placed.Blank || token.Kind == TokenKind.Comment) && last != '\n' )
					Put( text, '\n', ref bytes, ref last );
			}
			else if ( hasPrevious && !placed.NewLine && Merges( source, previous, token ) )
				Put( text, ' ', ref bytes, ref last );

			text?.Append( source, token.Start, token.Length );
			bytes += ScriptLimits.Utf8Bytes( source, token.Start, token.Length );
			last = source[ token.End - 1 ];
			lineCommentOpen = TokenLayout.IsLineComment( source, token );
			previous = token;
			hasPrevious = true;
		}

		return bytes;
	}

	private static void Put( StringBuilder text, char c, ref int bytes, ref char last )
	{
		text?.Append( c );
		bytes++;
		last = c;
	}

	/// <summary>Whether tokens <paramref name="a"/> then <paramref name="b"/> written together would read as something else.</summary>
	internal static bool Merges( string source, Token a, Token b )
	{
		if ( Token.IsWordLike( a.Kind ) && Token.IsWordLike( b.Kind ) )
			return true;

		var last = source[ a.End - 1 ];
		var first = source[ b.Start ];
		return last == '-' && first is '-' or '>'
			|| last is '<' or '>' or '=' or '~' && first == '='
			|| (a.Kind == TokenKind.Number || last == '.') && first is '.' or >= '0' and <= '9' && a.Kind != TokenKind.Name;
	}
}
