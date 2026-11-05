using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// Where a parsed script's lines break, the one structure the formatter and the minifier both write from: each token
/// (comments included, <c>;</c> left out) with whether it starts a line, whether a blank line (a paragraph break, a
/// <c>;</c> in minified text) comes first, and its indent. Statements and block ends start lines; an own-line comment keeps its line, indented with what
/// it comments; a line after a comment in the middle of a statement is indented one more. Because both writers follow
/// this, formatting a minified script and minifying a formatted one give each other back.
/// </summary>
public static class TokenLayout
{
	/// <summary>One token as it is laid out.</summary>
	public readonly record struct Placed( int Index, bool NewLine, bool Blank, int Depth );

	/// <summary>
	/// The layout of <paramref name="tree"/>, which must have parsed. A line keeps a blank line (or a <c>;</c>) before it,
	/// except the first line of a block and a block's end.
	/// </summary>
	public static IEnumerable<Placed> Of( SyntaxTree tree )
	{
		var tokens = tree.Tokens;
		var breaks = tree.Breaks;
		var any = false;
		var lineDepth = 0;
		var lastLineDepth = -1;
		var newLineNext = false;
		for ( var k = 0; k < tokens.Count; k++ )
		{
			var token = tokens[ k ];
			if ( token.Kind is TokenKind.EndOfFile or TokenKind.Semicolon )
				continue;

			var isComment = token.Kind == TokenKind.Comment;
			var ownLine = isComment && (token.NewlineBefore || !any);
			if ( ownLine || !isComment && breaks[ k ] >= 0 )
			{
				var depth = isComment ? CommentDepth( tokens, breaks, NextSignificant( tokens, k ), lineDepth ) : breaks[ k ] & SyntaxTree.DepthMask;
				var continues = !isComment && (breaks[ k ] & SyntaxTree.Continues) != 0;
				var closes = !isComment && token.Kind is TokenKind.End or TokenKind.Else or TokenKind.ElseIf;
				var blank = BlankBefore( tokens, k ) && !closes && !continues && lastLineDepth >= 0 && depth >= lastLineDepth;
				yield return new Placed( k, true, blank, depth );
				lastLineDepth = depth;
				if ( !isComment )
					lineDepth = depth;
			}
			else if ( isComment )
				yield return new Placed( k, false, false, lineDepth );
			else
			{
				yield return new Placed( k, newLineNext, false, newLineNext ? lineDepth + 1 : lineDepth );
				if ( newLineNext )
					lastLineDepth = lineDepth + 1;
			}

			newLineNext = isComment && (ownLine || IsLineComment( tree.Source, token ));
			any = true;
		}
	}

	// Whether a blank line, or a ; (a paragraph break in minified text), comes right before the token at k
	private static bool BlankBefore( List<Token> tokens, int k )
	{
		return tokens[ k ].BlankLineBefore || k > 0 && tokens[ k - 1 ].Kind == TokenKind.Semicolon;
	}

	/// <summary>Whether <paramref name="token"/> (a comment) runs to the end of its line, rather than being a <c>--[[ ]]</c> block.</summary>
	public static bool IsLineComment( string source, Token token ) =>
		token.Kind == TokenKind.Comment && !(token.Length >= 4 && source[ token.Start + 2 ] == '[' && source[ token.Start + 3 ] == '[');

	private static int NextSignificant( List<Token> tokens, int k )
	{
		var next = k + 1;
		while ( next < tokens.Count && tokens[ next ].Kind is TokenKind.Comment or TokenKind.Semicolon )
			next++;

		return next;
	}

	// An own-line comment is indented like the line it comes before; one before a block's end, like the block's inside
	private static int CommentDepth( List<Token> tokens, int[] breaks, int next, int lineDepth )
	{
		if ( next >= tokens.Count || tokens[ next ].Kind == TokenKind.EndOfFile )
			return 0;

		if ( breaks[ next ] < 0 )
			return lineDepth + 1;

		return (breaks[ next ] & SyntaxTree.DepthMask) + (tokens[ next ].Kind is TokenKind.End or TokenKind.Else or TokenKind.ElseIf ? 1 : 0);
	}
}
