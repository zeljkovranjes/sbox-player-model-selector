using System;
using System.Collections.Generic;
using System.Text;

namespace PlayerModels.Core.Script;

/// <summary>What an edit in the code editor makes: the new text and the selection after (the caret where both meet).</summary>
public readonly record struct TextChange( string Text, int SelectionStart, int SelectionEnd );

/// <summary>What a piece of a script is to the code editor's colours (VS Code's Dark+ and Light+ name the same kinds).</summary>
public enum CodeClass { Plain, Keyword, Control, Number, String, Comment, Function, Type, Variable, Operator }

/// <summary>A piece of a script to colour: where it is and what it is.</summary>
public readonly record struct CodeSpan( int Start, int Length, CodeClass Class );

/// <summary>
/// The code editor's text work, on plain strings so it is tested without the UI: lines and columns, auto-indent on
/// Enter, brackets and quotes closed as they are typed, indenting and commenting lines, words, find and replace, the
/// bracket or block (<c>function ... end</c>, <c>then ... end</c>, <c>do ... end</c>) matching the caret, and what to
/// colour each piece. Nothing here renders anything: the editor shows text only as plain text.
/// </summary>
public static class CodeText
{
	/// <summary>One step of indentation, as the formatter writes it.</summary>
	public const string Indent = "\t";

	/// <summary>The comment a line starts with when it is commented out.</summary>
	public const string LineComment = "-- ";

	/// <summary>The most matches find counts; past it the count reads as "1000+".</summary>
	public const int MostMatches = 1000;

	[Sandbox.SkipHotload]
	private static readonly (char Open, char Close)[] Pairs = { ('(', ')'), ('[', ']'), ('{', '}'), ('"', '"'), ('\'', '\'') };

	/// <summary>Where the line holding <paramref name="at"/> starts.</summary>
	public static int LineStart( string text, int at ) => at <= 0 ? 0 : text.LastIndexOf( '\n', Math.Min( at, text.Length ) - 1 ) + 1;

	/// <summary>Where the line holding <paramref name="at"/> ends (its line break, or the end of the text).</summary>
	public static int LineEnd( string text, int at )
	{
		var end = text.IndexOf( '\n', Math.Clamp( at, 0, text.Length ) );
		return end < 0 ? text.Length : end;
	}

	/// <summary>The line and column of <paramref name="at"/>, both from 1.</summary>
	public static (int Line, int Column) Position( string text, int at )
	{
		at = Math.Clamp( at, 0, text.Length );
		var line = 1;
		for ( var i = 0; i < at; i++ )
		{
			if ( text[ i ] == '\n' )
				line++;
		}

		return (line, at - LineStart( text, at ) + 1);
	}

	/// <summary>How many lines <paramref name="text"/> has.</summary>
	public static int LineCount( string text )
	{
		var lines = 1;
		foreach ( var c in text )
		{
			if ( c == '\n' )
				lines++;
		}

		return lines;
	}

	/// <summary>Where line <paramref name="line"/> (from 1) starts; the last line's start past the end, the first before it (go to line).</summary>
	public static int OffsetOfLine( string text, int line )
	{
		var at = 0;
		for ( var current = 1; current < line; current++ )
		{
			var next = text.IndexOf( '\n', at );
			if ( next < 0 )
				break;

			at = next + 1;
		}

		return at;
	}

	/// <summary>The indentation (tabs and spaces) the line holding <paramref name="at"/> starts with.</summary>
	public static string IndentationAt( string text, int at )
	{
		var start = LineStart( text, at );
		var end = start;
		while ( end < text.Length && text[ end ] is ' ' or '\t' )
			end++;

		return text[ start..end ];
	}

	/// <summary>
	/// Enter at the selection: a new line indented like the one before, one step more after a line that opens a block
	/// (<c>then</c>, <c>do</c>, <c>else</c>, <c>function ...( )</c>, an open bracket); between a bracket pair the closing one
	/// goes on a line of its own below.
	/// </summary>
	public static TextChange NewLine( string text, int start, int end )
	{
		(start, end) = Ordered( text, start, end );
		var indent = IndentationAt( text, start );
		var before = text[ LineStart( text, start )..start ].TrimEnd();
		var deeper = Opens( before ) ? indent + Indent : indent;
		var closes = start > 0 && end < text.Length && IsPair( text[ start - 1 ], text[ end ] ) && text[ start - 1 ] is '(' or '[' or '{';
		var inserted = closes ? "\n" + deeper + "\n" + indent : "\n" + deeper;
		var caret = start + 1 + deeper.Length;
		return new TextChange( text[ ..start ] + inserted + text[ end.. ], caret, caret );
	}

	/// <summary>
	/// <paramref name="typed"/> at the selection: an opening bracket or quote closes itself (around the selection, when
	/// there is one), typing a closing one where it already is steps over it, and <c>end</c>/<c>else</c>/<c>elseif</c>
	/// finished on a line of their own steps back out one indent. Anything else is just typed.
	/// </summary>
	public static TextChange Type( string text, int start, int end, char typed )
	{
		(start, end) = Ordered( text, start, end );
		foreach ( var (open, close) in Pairs )
		{
			if ( typed == close && start == end && start < text.Length && text[ start ] == close && (open != close || !InWord( text, start )) )
				return new TextChange( text, start + 1, start + 1 );

			if ( typed != open )
				continue;

			if ( start != end )
				return new TextChange( text[ ..start ] + open + text[ start..end ] + close + text[ end.. ], start + 1, end + 1 );

			// A quote right after a word (it's an apostrophe) or before one closes nothing
			var next = start < text.Length ? text[ start ] : '\n';
			var closes = open == close ? !InWord( text, start - 1 ) && !char.IsLetterOrDigit( next ) : !char.IsLetterOrDigit( next ) && next != '_';
			if ( closes )
				return new TextChange( text[ ..start ] + open + close + text[ end.. ], start + 1, start + 1 );
		}

		var typedText = text[ ..start ] + typed + text[ end.. ];
		var caret = start + 1;
		return Dedented( typedText, caret ) ?? new TextChange( typedText, caret, caret );
	}

	/// <summary>Backspace at the caret between an empty pair (<c>(|)</c>, <c>"|"</c>) removes both; null when it's an ordinary backspace.</summary>
	public static TextChange? DeletePair( string text, int start, int end )
	{
		if ( start != end || start <= 0 || start >= text.Length || !IsPair( text[ start - 1 ], text[ start ] ) )
			return null;

		return new TextChange( text[ ..(start - 1) ] + text[ (start + 1).. ], start - 1, start - 1 );
	}

	/// <summary>Tab or Shift+Tab over the selected lines: each indented one step, or one step less.</summary>
	public static TextChange IndentLines( string text, int start, int end, bool outdent )
	{
		(start, end) = Ordered( text, start, end );
		var first = LineStart( text, start );
		var last = end > start && end > 0 && text[ end - 1 ] == '\n' ? end - 1 : end;
		var result = new StringBuilder( text.Length + 64 ).Append( text, 0, first );
		var (newStart, newEnd) = (start, end);
		var at = first;
		while ( true )
		{
			var lineEnd = LineEnd( text, at );
			var line = text[ at..lineEnd ];
			var removed = 0;
			if ( outdent )
			{
				removed = line.StartsWith( '\t' ) ? 1 : CountSpaces( line, 4 );
				line = line[ removed.. ];
			}
			else
				line = Indent + line;

			var shift = outdent ? -removed : Indent.Length;
			if ( at <= start )
				newStart = Math.Max( at, start + shift );

			newEnd += shift;
			result.Append( line );
			if ( lineEnd >= last || lineEnd >= text.Length )
			{
				result.Append( text, lineEnd, text.Length - lineEnd );
				break;
			}

			result.Append( '\n' );
			at = lineEnd + 1;
		}

		return new TextChange( result.ToString(), Math.Max( 0, newStart ), Math.Max( 0, newEnd ) );
	}

	/// <summary>Ctrl+/ over the selected lines: all commented out with <c>-- </c>, or, when they all are, uncommented.</summary>
	public static TextChange ToggleComment( string text, int start, int end )
	{
		(start, end) = Ordered( text, start, end );
		var first = LineStart( text, start );
		var last = end > start && text[ end - 1 ] == '\n' ? end - 1 : end;
		var lines = new List<(int Start, int End)>();
		for ( var at = first; ; )
		{
			var lineEnd = LineEnd( text, at );
			lines.Add( (at, lineEnd) );
			if ( lineEnd >= last || lineEnd >= text.Length )
				break;

			at = lineEnd + 1;
		}

		var commented = lines.TrueForAll( x => text[ x.Start..x.End ].Trim().Length == 0 || text[ x.Start..x.End ].TrimStart().StartsWith( "--" ) );
		var indent = int.MaxValue;
		foreach ( var (lineStart, lineEnd) in lines )
		{
			var line = text[ lineStart..lineEnd ];
			if ( line.Trim().Length > 0 )
				indent = Math.Min( indent, line.Length - line.TrimStart().Length );
		}

		indent = indent == int.MaxValue ? 0 : indent;
		var result = new StringBuilder( text.Length + lines.Count * 3 ).Append( text, 0, first );
		var (newStart, newEnd) = (start, end);
		for ( var i = 0; i < lines.Count; i++ )
		{
			var (lineStart, lineEnd) = lines[ i ];
			var line = text[ lineStart..lineEnd ];
			var shift = 0;
			if ( line.Trim().Length > 0 )
			{
				if ( commented )
				{
					var at = line.IndexOf( "--", StringComparison.Ordinal );
					var length = at + 2 < line.Length && line[ at + 2 ] == ' ' ? 3 : 2;
					line = line[ ..at ] + line[ (at + length).. ];
					shift = -length;
				}
				else
				{
					line = line[ ..indent ] + LineComment + line[ indent.. ];
					shift = LineComment.Length;
				}
			}

			if ( lineStart <= start && start <= lineEnd )
				newStart = Math.Max( lineStart, start + shift );

			newEnd += shift;
			result.Append( line );
			if ( i < lines.Count - 1 )
				result.Append( '\n' );
		}

		result.Append( text, lines[ ^1 ].End, text.Length - lines[ ^1 ].End );
		return new TextChange( result.ToString(), Math.Max( 0, newStart ), Math.Max( 0, newEnd ) );
	}

	/// <summary>The word (letters, digits, <c>_</c>) around <paramref name="at"/> (double-click); empty where there is none.</summary>
	public static (int Start, int End) WordAt( string text, int at )
	{
		at = Math.Clamp( at, 0, text.Length );
		var start = at;
		var end = at;
		while ( start > 0 && IsWord( text[ start - 1 ] ) )
			start--;

		while ( end < text.Length && IsWord( text[ end ] ) )
			end++;

		return (start, end);
	}

	/// <summary>
	/// Where <paramref name="query"/> is in <paramref name="text"/>, in order: matching case or not, and as a whole word or
	/// anywhere; at most <see cref="MostMatches"/>.
	/// </summary>
	public static List<int> Find( string text, string query, bool matchCase, bool wholeWord )
	{
		var found = new List<int>();
		if ( string.IsNullOrEmpty( query ) || string.IsNullOrEmpty( text ) )
			return found;

		var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
		for ( var at = text.IndexOf( query, comparison ); at >= 0 && found.Count < MostMatches; at = text.IndexOf( query, at + Math.Max( 1, query.Length ), comparison ) )
		{
			if ( !wholeWord || IsWholeWord( text, at, query.Length ) )
				found.Add( at );

			if ( at + query.Length >= text.Length )
				break;
		}

		return found;
	}

	/// <summary>Every match of <paramref name="query"/> replaced with <paramref name="replacement"/>; <paramref name="count"/> says how many.</summary>
	public static string ReplaceAll( string text, string query, string replacement, bool matchCase, bool wholeWord, out int count )
	{
		var found = Find( text, query, matchCase, wholeWord );
		count = found.Count;
		if ( count == 0 )
			return text;

		var result = new StringBuilder( text.Length + count * Math.Max( 0, (replacement?.Length ?? 0) - query.Length ) );
		var at = 0;
		foreach ( var match in found )
		{
			result.Append( text, at, match - at ).Append( replacement );
			at = match + query.Length;
		}

		return result.Append( text, at, text.Length - at ).ToString();
	}

	/// <summary>
	/// The bracket or block matching the one at or just before <paramref name="caret"/>: a bracket's partner, or for a
	/// block keyword (<c>function</c>, <c>if</c>, <c>for</c>, <c>while</c>, <c>do</c>, <c>end</c>) the keyword opening
	/// it and its <c>end</c>. Both as (start, length); null when the caret isn't on one or it has no partner.
	/// </summary>
	public static ((int Start, int Length) First, (int Start, int Length) Second)? Matching( string text, int caret )
	{
		var tokens = Tokens( text );
		var at = TokenAt( tokens, caret );
		if ( at < 0 )
			return null;

		var token = tokens[ at ];
		var partner = token.Kind switch
		{
			TokenKind.LeftParen or TokenKind.LeftBracket or TokenKind.LeftBrace => Forward( tokens, at, token.Kind, Closer( token.Kind ) ),
			TokenKind.RightParen or TokenKind.RightBracket or TokenKind.RightBrace => Backward( tokens, at, Opener( token.Kind ), token.Kind ),
			TokenKind.Function or TokenKind.If or TokenKind.For or TokenKind.While or TokenKind.Do => BlockEnd( tokens, at ),
			TokenKind.End => BlockStart( tokens, at ),
			_ => -1,
		};

		return partner < 0 ? null : ((token.Start, token.Length), (tokens[ partner ].Start, tokens[ partner ].Length));
	}

	/// <summary>
	/// What to colour each piece of <paramref name="text"/> as, in order, comments and strings included. Text the lexer
	/// can't read (a string left open while typing) is plain to the end of its line, and colouring goes on after it.
	/// </summary>
	public static List<CodeSpan> Highlight( string text )
	{
		var spans = new List<CodeSpan>();
		var offset = 0;
		while ( offset < text.Length )
		{
			var part = offset == 0 ? text : text[ offset.. ];
			var tokens = Lexer.All( part, new LineMap( part ), out var error );
			for ( var i = 0; i < tokens.Count; i++ )
			{
				var token = tokens[ i ];
				if ( token.Kind == TokenKind.EndOfFile )
					break;

				var kind = ClassOf( part, tokens, i );
				if ( kind != CodeClass.Plain )
					spans.Add( new CodeSpan( offset + token.Start, token.Length, kind ) );
			}

			if ( error is null )
				break;

			// Past what the lexer couldn't read, to the next line
			var stop = offset + Math.Clamp( error.Start, 0, part.Length );
			var next = text.IndexOf( '\n', stop );
			if ( next < 0 )
				break;

			offset = next + 1;
		}

		return spans;
	}

	private static CodeClass ClassOf( string text, List<Token> tokens, int i )
	{
		var token = tokens[ i ];
		switch ( token.Kind )
		{
			case TokenKind.Comment:
				return CodeClass.Comment;
			case TokenKind.String:
				return CodeClass.String;
			case TokenKind.Number:
				return CodeClass.Number;
			case TokenKind.If or TokenKind.Then or TokenKind.Else or TokenKind.ElseIf or TokenKind.End or TokenKind.For or TokenKind.While or TokenKind.Do
				or TokenKind.Return or TokenKind.Break or TokenKind.In:
				return CodeClass.Control;
			case TokenKind.Name:
				break;
			default:
				return Token.IsKeyword( token.Kind ) ? CodeClass.Keyword : CodeClass.Operator;
		}

		var name = text.Substring( token.Start, token.Length );
		var afterDot = i > 0 && tokens[ i - 1 ].Kind == TokenKind.Dot;
		var called = i + 1 < tokens.Count && tokens[ i + 1 ].Kind == TokenKind.LeftParen;
		var declared = i > 0 && tokens[ i - 1 ].Kind == TokenKind.Function;
		if ( called || declared || ScriptLibrary.Event( name ) is not null )
			return CodeClass.Function;

		if ( !afterDot && (ScriptLibrary.Namespace( name ) is not null || ScriptLibrary.Type( name ) is not null) )
			return CodeClass.Type;

		return CodeClass.Variable;
	}

	/// <summary>Every token of <paramref name="text"/> the lexer reads, up to the end or its first error.</summary>
	public static List<Token> Tokens( string text ) => Lexer.All( text ?? "", new LineMap( text ?? "" ), out _ );

	// The token at the caret, or the one just before it (a caret after ")" matches it)
	private static int TokenAt( List<Token> tokens, int caret )
	{
		var before = -1;
		for ( var i = 0; i < tokens.Count; i++ )
		{
			var token = tokens[ i ];
			if ( token.Kind is TokenKind.EndOfFile or TokenKind.Comment or TokenKind.String )
				continue;

			if ( caret >= token.Start && caret < token.End )
				return Matchable( token.Kind ) ? i : before >= 0 && tokens[ before ].End == caret ? before : -1;

			if ( token.End == caret && Matchable( token.Kind ) )
				before = i;

			if ( token.Start > caret )
				break;
		}

		return before;
	}

	private static bool Matchable( TokenKind kind ) => kind is TokenKind.LeftParen or TokenKind.RightParen or TokenKind.LeftBracket or TokenKind.RightBracket
		or TokenKind.LeftBrace or TokenKind.RightBrace or TokenKind.Function or TokenKind.If or TokenKind.For or TokenKind.While or TokenKind.Do or TokenKind.End;

	private static TokenKind Closer( TokenKind open ) => open switch { TokenKind.LeftParen => TokenKind.RightParen, TokenKind.LeftBracket => TokenKind.RightBracket, _ => TokenKind.RightBrace };

	private static TokenKind Opener( TokenKind close ) => close switch { TokenKind.RightParen => TokenKind.LeftParen, TokenKind.RightBracket => TokenKind.LeftBracket, _ => TokenKind.LeftBrace };

	private static int Forward( List<Token> tokens, int at, TokenKind open, TokenKind close )
	{
		var depth = 0;
		for ( var i = at; i < tokens.Count; i++ )
		{
			if ( tokens[ i ].Kind == open )
				depth++;
			else if ( tokens[ i ].Kind == close && --depth == 0 )
				return i;
		}

		return -1;
	}

	private static int Backward( List<Token> tokens, int at, TokenKind open, TokenKind close )
	{
		var depth = 0;
		for ( var i = at; i >= 0; i-- )
		{
			if ( tokens[ i ].Kind == close )
				depth++;
			else if ( tokens[ i ].Kind == open && --depth == 0 )
				return i;
		}

		return -1;
	}

	// A block keyword's end: for and while open their block at their do, which then isn't one of its own
	private static int BlockEnd( List<Token> tokens, int at )
	{
		var depth = 0;
		var loopHeader = false;
		for ( var i = at; i < tokens.Count; i++ )
		{
			var kind = tokens[ i ].Kind;
			if ( kind is TokenKind.Function or TokenKind.If )
				depth++;
			else if ( kind is TokenKind.For or TokenKind.While )
			{
				depth++;
				loopHeader = true;
			}
			else if ( kind == TokenKind.Do )
			{
				if ( loopHeader )
					loopHeader = false;
				else
					depth++;
			}
			else if ( kind == TokenKind.End && --depth == 0 )
				return i;
		}

		return -1;
	}

	private static int BlockStart( List<Token> tokens, int at )
	{
		// Walk forward from the top, keeping the opener of each block open at each point
		var open = new List<int>();
		var loopHeader = false;
		for ( var i = 0; i <= at && i < tokens.Count; i++ )
		{
			var kind = tokens[ i ].Kind;
			if ( kind is TokenKind.Function or TokenKind.If )
				open.Add( i );
			else if ( kind is TokenKind.For or TokenKind.While )
			{
				open.Add( i );
				loopHeader = true;
			}
			else if ( kind == TokenKind.Do )
			{
				if ( loopHeader )
					loopHeader = false;
				else
					open.Add( i );
			}
			else if ( kind == TokenKind.End && open.Count > 0 )
			{
				if ( i == at )
					return open[ ^1 ];

				open.RemoveAt( open.Count - 1 );
			}
		}

		return -1;
	}

	// Whether a line opens a block, so the next goes in a step
	private static bool Opens( string line )
	{
		if ( line.Length == 0 || line.StartsWith( "--" ) )
			return false;

		var last = line[ ^1 ];
		if ( last is '(' or '[' or '{' )
			return true;

		var word = LastWord( line );
		if ( word is "then" or "do" or "else" or "repeat" )
			return true;

		var trimmed = line.TrimStart();
		return last == ')' && (trimmed.StartsWith( "function " ) || trimmed.StartsWith( "local function " ) || line.Contains( "function(" ) || line.Contains( "function (" ));
	}

	// The line just typed, "end", "else" or "elseif" alone on it: indented as the line opening its block
	private static TextChange? Dedented( string text, int caret )
	{
		var start = LineStart( text, caret );
		var line = text[ start..caret ];
		var word = line.TrimStart();
		if ( word is not ("end" or "else" or "elseif") || text[ caret..LineEnd( text, caret ) ].Trim().Length > 0 )
			return null;

		var tokens = Tokens( text[ ..start ] );
		var open = new List<int>();
		var loopHeader = false;
		foreach ( var token in tokens )
		{
			switch ( token.Kind )
			{
				case TokenKind.Function or TokenKind.If:
					open.Add( token.Start );
					break;
				case TokenKind.For or TokenKind.While:
					open.Add( token.Start );
					loopHeader = true;
					break;
				case TokenKind.Do when loopHeader:
					loopHeader = false;
					break;
				case TokenKind.Do:
					open.Add( token.Start );
					break;
				case TokenKind.End when open.Count > 0:
					open.RemoveAt( open.Count - 1 );
					break;
			}
		}

		if ( open.Count == 0 )
			return null;

		var indent = IndentationAt( text, open[ ^1 ] );
		if ( line.Length - word.Length == indent.Length && line.StartsWith( indent ) )
			return null;

		var replaced = text[ ..start ] + indent + word + text[ caret.. ];
		var newCaret = start + indent.Length + word.Length;
		return new TextChange( replaced, newCaret, newCaret );
	}

	private static string LastWord( string line )
	{
		var end = line.Length;
		var start = end;
		while ( start > 0 && IsWord( line[ start - 1 ] ) )
			start--;

		return line[ start..end ];
	}

	private static bool IsPair( char open, char close )
	{
		foreach ( var pair in Pairs )
		{
			if ( pair.Open == open && pair.Close == close )
				return true;
		}

		return false;
	}

	private static bool IsWord( char c ) => char.IsLetterOrDigit( c ) || c == '_';

	private static bool InWord( string text, int at ) => at >= 0 && at < text.Length && IsWord( text[ at ] );

	private static bool IsWholeWord( string text, int at, int length ) => !InWord( text, at - 1 ) && !InWord( text, at + length );

	private static int CountSpaces( string line, int most )
	{
		var count = 0;
		while ( count < most && count < line.Length && line[ count ] == ' ' )
			count++;

		return count;
	}

	private static (int, int) Ordered( string text, int start, int end )
	{
		start = Math.Clamp( start, 0, text.Length );
		end = Math.Clamp( end, 0, text.Length );
		return start <= end ? (start, end) : (end, start);
	}
}
