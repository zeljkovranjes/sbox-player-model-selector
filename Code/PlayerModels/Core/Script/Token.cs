namespace PlayerModels.Core.Script;

/// <summary>What a token of a script is.</summary>
public enum TokenKind
{
	EndOfFile, Name, Number, String, Comment,

	// Keywords
	And, Break, Do, Else, ElseIf, End, False, For, Function, If, In, Local, Not, Or, Return, Then, True, While,

	// Lua words this language keeps back, so they are never names: each is refused with what to write instead
	Nil, Repeat, Until, Goto,

	Plus, Minus, Star, Slash, Percent, Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual, Assign,
	LeftParen, RightParen, LeftBrace, RightBrace, LeftBracket, RightBracket, Semicolon, Colon, Comma, Dot, Arrow,
}

/// <summary>
/// One token of a script: its kind, where it is, and whether a line break (or a blank line) comes before it; a number's
/// or a string's value is read from the source when it is needed (<see cref="Lexer.NumberOf"/>, <see cref="Lexer.TextOf"/>),
/// so a token is small. Comments are tokens too, so formatting and minifying keep them.
/// </summary>
public readonly struct Token
{
	public TokenKind Kind { get; }
	public int Start { get; }
	public int Length { get; }
	public bool NewlineBefore { get; }
	public bool BlankLineBefore { get; }

	public Token( TokenKind kind, int start, int length, bool newlineBefore, bool blankLineBefore )
	{
		Kind = kind;
		Start = start;
		Length = length;
		NewlineBefore = newlineBefore;
		BlankLineBefore = blankLineBefore;
	}

	public int End => Start + Length;

	/// <summary>Whether it is a keyword, by its kind.</summary>
	public static bool IsKeyword( TokenKind kind ) => kind is >= TokenKind.And and <= TokenKind.Goto;

	/// <summary>Whether the token is a word to a reader: a name, a keyword or a number, so two in a row need a space.</summary>
	public static bool IsWordLike( TokenKind kind ) => kind is TokenKind.Name or TokenKind.Number || IsKeyword( kind );
}
