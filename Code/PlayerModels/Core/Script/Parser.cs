using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// Reads a script into a <see cref="SyntaxTree"/>: Lua's statements (<c>local</c>, <c>function ... end</c>,
/// <c>if/elseif/else/end</c>, numeric <c>for</c>, <c>while</c>, <c>return</c>, <c>break</c>), Luau's type annotations
/// (<c>x: number</c>) and if-expressions, and C#-like calls on dotted names. Pulls tokens from the <see cref="Lexer"/>
/// only as it needs them and stops at the first error, so text that nests too deep or runs past a cap is refused after
/// reading just that far. Records for the formatter which tokens begin a line and at what depth.
/// </summary>
public sealed partial class Parser
{
	private readonly string _source;
	private readonly LineMap _lines;
	private readonly Lexer _lexer;
	private readonly List<Token> _tokens = new();
	private readonly List<int> _breaks = new();
	private int _at = -1;
	private int _blockDepth;
	private int _depth;
	private int _nodes;
	private ScriptError _error;

	private Parser( string source, LineMap lines )
	{
		_source = source;
		_lines = lines;
		_lexer = new Lexer( source, lines );
	}

	/// <summary>Parses <paramref name="source"/>; the tree's <see cref="SyntaxTree.Error"/> says why when it can't.</summary>
	public static SyntaxTree Parse( string source, LineMap lines = null )
	{
		source ??= "";
		lines ??= new LineMap( source );
		var parser = new Parser( source, lines );
		Block root = null;
		if ( parser.Advance() )
		{
			root = parser.ParseBlock();
			if ( parser._error is null && parser.Current.Kind != TokenKind.EndOfFile )
				parser.Fail( parser.Current.Kind == TokenKind.End ? "This 'end' closes nothing" : $"Unexpected {Describe( parser.Current )}", parser.Current );
		}

		return new SyntaxTree
		{
			Source = source,
			Lines = lines,
			Tokens = parser._tokens,
			Root = parser._error is null ? root : null,
			Breaks = parser._breaks.ToArray(),
			Error = parser._error ?? parser._lexer.Error,
		};
	}

	private Token Current => _tokens[ _at ];

	private bool Is( TokenKind kind ) => _error is null && Current.Kind == kind;

	// Moves to the next token that isn't a comment, reading it in when needed
	private bool Advance()
	{
		if ( _error is not null )
			return false;

		if ( _at >= 0 && Current.Kind == TokenKind.EndOfFile )
			return true;

		while ( true )
		{
			_at++;
			if ( _at >= _tokens.Count )
			{
				if ( !_lexer.Next( out var token ) )
				{
					_error = _lexer.Error;
					_at--;
					return false;
				}

				_tokens.Add( token );
				_breaks.Add( -1 );
			}

			if ( Current.Kind != TokenKind.Comment )
				return true;
		}
	}

	private bool Expect( TokenKind kind, string what )
	{
		if ( _error is not null )
			return false;

		if ( Current.Kind != kind )
			return Fail( $"Expected {what}, found {Describe( Current )}", Current );

		return Advance();
	}

	private bool Fail( string message, Token at ) => Fail( message, at.Start, at.Length );

	private bool Fail( string message, int start, int length )
	{
		_error ??= _lines.Error( message, start, length );
		return false;
	}

	private T Made<T>( T node, int start ) where T : Node
	{
		node.Start = start;
		node.End = _at > 0 ? PreviousEnd() : start;
		if ( ++_nodes > ScriptLimits.MaxNodes )
			Fail( $"The script is too big to compile (more than {ScriptLimits.MaxNodes} parts)", start, 1 );

		return node;
	}

	// The end of the last token that isn't a comment before the current one
	private int PreviousEnd()
	{
		for ( var i = _at - 1; i >= 0; i-- )
		{
			if ( _tokens[ i ].Kind != TokenKind.Comment )
				return _tokens[ i ].End;
		}

		return 0;
	}

	private bool Enter()
	{
		if ( ++_depth <= ScriptLimits.MaxDepth )
			return true;

		return Fail( $"The script nests too deep (more than {ScriptLimits.MaxDepth} levels)", Current );
	}

	private void Leave() => _depth--;

	// Marks the current token as starting a line at depth
	private void Break( int depth )
	{
		if ( _error is null )
			_breaks[ _at ] = depth;
	}

	// Marks the current token as starting a line at depth that goes on the statement before it
	private void Continue( int depth )
	{
		if ( _error is null )
			_breaks[ _at ] = depth | SyntaxTree.Continues;
	}

	private static string Describe( Token token ) => token.Kind switch
	{
		TokenKind.EndOfFile => "the end of the script",
		TokenKind.Name => "a name",
		TokenKind.Number => "a number",
		TokenKind.String => "text",
		_ => $"'{Spelling( token.Kind )}'",
	};

	/// <summary>How a token of <paramref name="kind"/> is written (a keyword or a symbol).</summary>
	public static string Spelling( TokenKind kind ) => kind switch
	{
		TokenKind.ElseIf => "elseif",
		TokenKind.Plus => "+", TokenKind.Minus => "-", TokenKind.Star => "*", TokenKind.Slash => "/", TokenKind.Percent => "%",
		TokenKind.Equal => "==", TokenKind.NotEqual => "~=", TokenKind.Less => "<", TokenKind.LessEqual => "<=",
		TokenKind.Greater => ">", TokenKind.GreaterEqual => ">=", TokenKind.Assign => "=", TokenKind.LeftParen => "(",
		TokenKind.RightParen => ")", TokenKind.LeftBrace => "{", TokenKind.RightBrace => "}", TokenKind.LeftBracket => "[",
		TokenKind.RightBracket => "]", TokenKind.Semicolon => ";", TokenKind.Colon => ":", TokenKind.Comma => ",",
		TokenKind.Dot => ".", TokenKind.Arrow => "->",
		_ => kind.ToString().ToLowerInvariant(),
	};
}
