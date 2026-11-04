using System.Collections.Generic;
using System.Text;

namespace PlayerModels.Core.Script;

/// <summary>
/// Writes a script in the one canonical style (like gofmt), the way the code editor opens it: one statement a line,
/// tabs for blocks, a blank line between paragraphs, spaces around operators and inside call and index brackets
/// (<c>Math.Floor( time / 100 )</c>, <c>palette[ i ]</c>), none inside grouping parentheses (<c>(a + b) * c</c>), the
/// house style of the addon's C#. Comments, literals and names are kept exactly as written.
/// </summary>
public static class Formatter
{
	/// <summary><paramref name="source"/> formatted; null when it doesn't parse (an editor keeps the text as it is).</summary>
	public static string Format( string source )
	{
		if ( source is null || source.Length > ScriptLimits.MaxSourceLength )
			return null;

		var tree = Parser.Parse( source );
		return tree.Error is null ? Format( tree ) : null;
	}

	/// <summary>A parsed script formatted.</summary>
	public static string Format( SyntaxTree tree )
	{
		var text = new StringBuilder( tree.Source.Length + tree.Source.Length / 4 );
		var brackets = new List<TokenKind>();
		Token previous = default;
		var hasPrevious = false;
		var previousUnary = false;
		foreach ( var placed in TokenLayout.Of( tree ) )
		{
			var token = tree.Tokens[ placed.Index ];
			if ( placed.NewLine )
			{
				if ( text.Length > 0 )
					text.Append( placed.Blank ? "\n\n" : "\n" );

				text.Append( '\t', placed.Depth );
			}
			else if ( hasPrevious && (token.Kind == TokenKind.Comment || Spaced( previous, token, brackets, previousUnary ) || Minifier.Merges( tree.Source, previous, token )) )
				text.Append( ' ' );

			text.Append( tree.Source, token.Start, token.Length );
			if ( token.Kind == TokenKind.Comment )
				continue;

			previousUnary = token.Kind == TokenKind.Minus && !(hasPrevious && EndsOperand( previous.Kind ));
			Track( brackets, previous, hasPrevious, token, tree.Breaks[ placed.Index ] >= 0 );
			previous = token;
			hasPrevious = true;
		}

		return text.Append( '\n' ).ToString();
	}

	// What each open bracket is: a call's (or a function's parameters), a grouping, an index, an attribute, a list
	private const TokenKind Call = TokenKind.LeftParen, Group = TokenKind.Or, Index = TokenKind.LeftBracket, Attribute = TokenKind.And, List = TokenKind.LeftBrace;

	// A [ starting a statement is an attribute's; any other after a value, an index
	private static void Track( List<TokenKind> brackets, Token previous, bool hasPrevious, Token token, bool startsStatement )
	{
		switch ( token.Kind )
		{
			case TokenKind.LeftParen:
				brackets.Add( hasPrevious && (EndsOperand( previous.Kind ) || previous.Kind == TokenKind.Function) ? Call : Group );
				break;
			case TokenKind.LeftBracket:
				brackets.Add( hasPrevious && EndsOperand( previous.Kind ) && !startsStatement ? Index : Attribute );
				break;
			case TokenKind.LeftBrace:
				brackets.Add( List );
				break;
			case TokenKind.RightParen or TokenKind.RightBracket or TokenKind.RightBrace when brackets.Count > 0:
				brackets.RemoveAt( brackets.Count - 1 );
				break;
		}
	}

	/// <summary>Whether a token of <paramref name="kind"/> ends a value, so what follows is a call, an index or a binary operator.</summary>
	public static bool EndsOperand( TokenKind kind ) => kind is TokenKind.Name or TokenKind.Number or TokenKind.String or TokenKind.True
		or TokenKind.False or TokenKind.RightParen or TokenKind.RightBracket or TokenKind.RightBrace or TokenKind.End;

	private static bool Spaced( Token previous, Token token, List<TokenKind> brackets, bool previousUnary )
	{
		var open = brackets.Count > 0 ? brackets[ ^1 ] : TokenKind.EndOfFile;
		var (a, b) = (previous.Kind, token.Kind);
		switch ( b )
		{
			case TokenKind.Comma or TokenKind.Dot or TokenKind.Colon: return false;
			case TokenKind.RightParen: return a != TokenKind.LeftParen && open == Call;
			case TokenKind.RightBracket: return a != TokenKind.LeftBracket && open == Index;
			case TokenKind.RightBrace: return a != TokenKind.LeftBrace;
		}

		switch ( a )
		{
			case TokenKind.LeftParen: return open == Call;
			case TokenKind.LeftBracket: return open == Index;
			case TokenKind.LeftBrace: return true;
			case TokenKind.Dot: return false;
		}

		if ( previousUnary )
			return false;

		return b switch
		{
			TokenKind.LeftParen => !(EndsOperand( a ) || a == TokenKind.Function),
			TokenKind.LeftBracket => !EndsOperand( a ),
			_ => true,
		};
	}
}
