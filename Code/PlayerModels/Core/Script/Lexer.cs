using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlayerModels.Core.Script;

/// <summary>
/// Reads a script's text into tokens, one at a time, holding it to <see cref="ScriptLimits"/> as it goes: a line, a
/// string, a number, a name or the bracket nesting that is too long or too deep stops it right there, so hostile text
/// costs no more than the little read before it. Lua's spelling (names, <c>--</c> comments, <c>--[[ ]]</c> blocks,
/// <c>~=</c>), and for what Lua has but this language leaves out, an error saying what to write instead.
/// </summary>
public sealed class Lexer
{
	/// <summary>The longest name.</summary>
	public const int MaxNameLength = 64;

	[Sandbox.SkipHotload]
	private static readonly (string Word, TokenKind Kind)[] Keywords =
	{
		("and", TokenKind.And), ("break", TokenKind.Break), ("do", TokenKind.Do), ("else", TokenKind.Else), ("elseif", TokenKind.ElseIf),
		("end", TokenKind.End), ("false", TokenKind.False), ("for", TokenKind.For), ("function", TokenKind.Function), ("if", TokenKind.If),
		("in", TokenKind.In), ("local", TokenKind.Local), ("not", TokenKind.Not), ("or", TokenKind.Or), ("return", TokenKind.Return),
		("then", TokenKind.Then), ("true", TokenKind.True), ("while", TokenKind.While), ("nil", TokenKind.Nil), ("repeat", TokenKind.Repeat),
		("until", TokenKind.Until), ("goto", TokenKind.Goto),
	};

	private readonly string _source;
	private readonly LineMap _lines;
	private int _at;
	private int _lineStart;
	private int _tokens;
	private int _depth;
	private bool _finished;

	/// <summary>The first error met; null while there is none.</summary>
	public ScriptError Error { get; private set; }

	public Lexer( string source, LineMap lines )
	{
		_source = source ?? "";
		_lines = lines;
	}

	/// <summary>Every token of <paramref name="source"/>, comments included, up to the end or the first error.</summary>
	public static List<Token> All( string source, LineMap lines, out ScriptError error )
	{
		var lexer = new Lexer( source, lines );
		var tokens = new List<Token>();
		while ( lexer.Next( out var token ) )
		{
			tokens.Add( token );
			if ( token.Kind == TokenKind.EndOfFile )
				break;
		}

		error = lexer.Error;
		return tokens;
	}

	/// <summary>The next token (the end of the text is one, then stays one); false once there is an error.</summary>
	public bool Next( out Token token )
	{
		token = default;
		if ( Error is not null )
			return false;

		var newlines = SkipSpace();
		if ( Error is not null )
			return false;

		var newline = newlines > 0 || _at == 0;
		var blank = newlines > 1;
		if ( _at >= _source.Length )
		{
			_finished = true;
			token = new Token( TokenKind.EndOfFile, _source.Length, 0, newline, blank );
			return true;
		}

		if ( ++_tokens > ScriptLimits.MaxTokens )
			return Fail( $"The script has too many words and symbols (more than {ScriptLimits.MaxTokens})", _at, 1 );

		var start = _at;
		var c = _source[ _at ];
		var ok = c switch
		{
			'-' when Peek( 1 ) == '-' => Comment( out token, newline, blank ),
			'"' or '\'' => String( out token, newline, blank ),
			_ when IsDigit( c ) || c == '.' && IsDigit( Peek( 1 ) ) => Number( out token, newline, blank ),
			_ when IsNameStart( c ) => Name( out token, newline, blank ),
			_ => Symbol( out token, newline, blank ),
		};

		if ( !ok )
			return false;

		if ( _at - _lineStart > ScriptLimits.MaxLineLength )
			return Fail( $"This line is too long (more than {ScriptLimits.MaxLineLength} characters)", start, 1 );

		return true;
	}

	/// <summary>Whether it has read to the end of the text.</summary>
	public bool AtEnd => _finished;

	// Skips spaces and line breaks, counting the breaks
	private int SkipSpace()
	{
		var newlines = 0;
		while ( _at < _source.Length )
		{
			var c = _source[ _at ];
			if ( c == '\n' )
			{
				newlines++;
				_at++;
				_lineStart = _at;
			}
			else if ( c is ' ' or '\t' or '\r' or '\f' or '\v' )
			{
				_at++;
				if ( _at - _lineStart > ScriptLimits.MaxLineLength )
				{
					Fail( $"This line is too long (more than {ScriptLimits.MaxLineLength} characters)", _at - 1, 1 );
					return newlines;
				}
			}
			else
				break;
		}

		return newlines;
	}

	private bool Comment( out Token token, bool newline, bool blank )
	{
		var start = _at;
		_at += 2;
		if ( Peek( 0 ) == '[' && Peek( 1 ) == '[' )
		{
			var close = _source.IndexOf( "]]", _at + 2, System.StringComparison.Ordinal );
			if ( close < 0 )
			{
				token = default;
				return Fail( "This comment is never closed: end it with ]]", start, 4 );
			}

			for ( var i = _at; i < close; i++ )
			{
				if ( _source[ i ] == '\n' )
					_lineStart = i + 1;
			}

			_at = close + 2;
		}
		else
		{
			while ( _at < _source.Length && _source[ _at ] != '\n' )
				_at++;
		}

		token = new Token( TokenKind.Comment, start, _at - start, newline, blank );
		return true;
	}

	private bool String( out Token token, bool newline, bool blank )
	{
		token = default;
		var start = _at;
		var quote = _source[ _at++ ];
		var length = 0;
		while ( true )
		{
			if ( _at >= _source.Length || _source[ _at ] == '\n' )
				return Fail( $"This text is never closed: end it with {quote}", start, 1 );

			var c = _source[ _at++ ];
			if ( c == quote )
				break;

			if ( c == '\\' )
			{
				if ( Escaped( _at < _source.Length ? _source[ _at++ ] : '\0' ) == '\0' )
					return Fail( "Unknown escape: use \\n, \\t, \\\\, \\\" or \\'", _at - 2, 2 );
			}
			else if ( char.IsControl( c ) && c != '\t' )
				return Fail( "Text can't hold control characters", _at - 1, 1 );

			if ( ++length > ScriptLimits.MaxStringLength )
				return Fail( $"This text is too long (more than {ScriptLimits.MaxStringLength} characters)", start, 1 );
		}

		token = new Token( TokenKind.String, start, _at - start, newline, blank );
		return true;
	}

	// What an escape (the character after a backslash) stands for; \0 for none
	private static char Escaped( char c ) => c switch { 'n' => '\n', 't' => '\t', '\\' => '\\', '"' => '"', '\'' => '\'', _ => '\0' };

	/// <summary>The value of a string token (read by this lexer from <paramref name="source"/>), its escapes worked out.</summary>
	public static string TextOf( string source, Token token )
	{
		var text = new System.Text.StringBuilder( token.Length );
		for ( var i = token.Start + 1; i < token.End - 1; i++ )
			text.Append( source[ i ] == '\\' ? Escaped( source[ ++i ] ) : source[ i ] );

		return text.ToString();
	}

	/// <summary>The value of a number token (read by this lexer from <paramref name="source"/>).</summary>
	public static double NumberOf( string source, Token token ) =>
		double.Parse( source.AsSpan( token.Start, token.Length ), NumberStyles.Float, CultureInfo.InvariantCulture );

	private bool Number( out Token token, bool newline, bool blank )
	{
		token = default;
		var start = _at;
		Digits();
		if ( Peek( 0 ) == '.' && Peek( 1 ) != '.' )
		{
			_at++;
			Digits();
		}

		if ( Peek( 0 ) is 'e' or 'E' )
		{
			_at++;
			if ( Peek( 0 ) is '+' or '-' )
				_at++;

			if ( !IsDigit( Peek( 0 ) ) )
				return Fail( "This number is malformed", start, _at - start );

			Digits();
		}

		var length = _at - start;
		if ( length > ScriptLimits.MaxNumberLength )
			return Fail( $"This number is too long (more than {ScriptLimits.MaxNumberLength} characters)", start, length );

		if ( IsNameStart( Peek( 0 ) ) || IsDigit( Peek( 0 ) ) )
			return Fail( "This number is malformed", start, _at - start + 1 );

		if ( !double.TryParse( _source.AsSpan( start, length ), NumberStyles.Float, CultureInfo.InvariantCulture, out var value )
			|| !double.IsFinite( value ) || value > ScriptLimits.MaxNumber )
			return Fail( $"This number is too big (the largest is {ScriptLimits.MaxNumber:0e0})", start, length );

		token = new Token( TokenKind.Number, start, length, newline, blank );
		return true;
	}

	private void Digits()
	{
		// Read one past the number cap at most: a longer run is refused right after, however long it goes on
		for ( var read = 0; IsDigit( Peek( 0 ) ) && read <= ScriptLimits.MaxNumberLength; read++ )
			_at++;
	}

	private bool Name( out Token token, bool newline, bool blank )
	{
		token = default;
		var start = _at;
		while ( _at < _source.Length && IsNamePart( _source[ _at ] ) )
		{
			if ( ++_at - start > MaxNameLength )
				return Fail( $"This name is too long (more than {MaxNameLength} characters)", start, MaxNameLength );
		}

		token = new Token( KeywordAt( start, _at - start ), start, _at - start, newline, blank );
		return true;
	}

	// The keyword the name at start is, or Name: compared in place, so reading a name allocates nothing
	private TokenKind KeywordAt( int start, int length )
	{
		foreach ( var (word, kind) in Keywords )
		{
			if ( word.Length == length && string.CompareOrdinal( _source, start, word, 0, length ) == 0 )
				return kind;
		}

		return TokenKind.Name;
	}

	private bool Symbol( out Token token, bool newline, bool blank )
	{
		token = default;
		var start = _at;
		var c = _source[ _at ];
		var next = Peek( 1 );
		var (kind, length, hint) = c switch
		{
			'+' => (TokenKind.Plus, 1, null),
			'-' when next == '>' => (TokenKind.Arrow, 2, null),
			'-' => (TokenKind.Minus, 1, null),
			'*' => (TokenKind.Star, 1, null),
			'/' when next == '/' => (TokenKind.EndOfFile, 2, "There is no // operator: use Math.Floor( a / b )"),
			'/' => (TokenKind.Slash, 1, null),
			'%' => (TokenKind.Percent, 1, null),
			'=' when next == '=' => (TokenKind.Equal, 2, null),
			'=' => (TokenKind.Assign, 1, null),
			'~' when next == '=' => (TokenKind.NotEqual, 2, null),
			'!' when next == '=' => (TokenKind.EndOfFile, 2, "Write 'not equal' as ~="),
			'!' => (TokenKind.EndOfFile, 1, "Write 'not' instead of !"),
			'&' when next == '&' => (TokenKind.EndOfFile, 2, "Write 'and' instead of &&"),
			'|' when next == '|' => (TokenKind.EndOfFile, 2, "Write 'or' instead of ||"),
			'<' when next == '=' => (TokenKind.LessEqual, 2, null),
			'<' => (TokenKind.Less, 1, null),
			'>' when next == '=' => (TokenKind.GreaterEqual, 2, null),
			'>' => (TokenKind.Greater, 1, null),
			'(' => (TokenKind.LeftParen, 1, null),
			')' => (TokenKind.RightParen, 1, null),
			'{' => (TokenKind.LeftBrace, 1, null),
			'}' => (TokenKind.RightBrace, 1, null),
			'[' => (TokenKind.LeftBracket, 1, null),
			']' => (TokenKind.RightBracket, 1, null),
			';' => (TokenKind.Semicolon, 1, null),
			':' => (TokenKind.Colon, 1, null),
			',' => (TokenKind.Comma, 1, null),
			'.' when next == '.' => (TokenKind.EndOfFile, 2, "Joining text with .. isn't supported: text never changes while a script runs"),
			'.' => (TokenKind.Dot, 1, null),
			'^' => (TokenKind.EndOfFile, 1, "There is no ^ operator: use Math.Pow( x, y )"),
			'#' => (TokenKind.EndOfFile, 1, "There is no # operator: use list.Count"),
			_ => (TokenKind.EndOfFile, 1, char.IsControl( c ) ? "This script holds a control character" : $"'{c}' isn't part of the language"),
		};

		if ( hint is not null )
			return Fail( hint, start, length );

		if ( kind is TokenKind.LeftParen or TokenKind.LeftBrace or TokenKind.LeftBracket && ++_depth > ScriptLimits.MaxDepth )
			return Fail( $"Brackets nest too deep (more than {ScriptLimits.MaxDepth})", start, 1 );

		if ( kind is TokenKind.RightParen or TokenKind.RightBrace or TokenKind.RightBracket )
			_depth--;

		_at += length;
		token = new Token( kind, start, length, newline, blank );
		return true;
	}

	private char Peek( int ahead ) => _at + ahead < _source.Length ? _source[ _at + ahead ] : '\0';

	private bool Fail( string message, int start, int length )
	{
		Error ??= _lines.Error( message, start, length );
		return false;
	}

	private static bool IsDigit( char c ) => c is >= '0' and <= '9';

	private static bool IsNameStart( char c ) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_';

	private static bool IsNamePart( char c ) => IsNameStart( c ) || IsDigit( c );
}
