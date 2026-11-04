using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// What the editor's helpers (completion, hover, signature help) know about a place in a script being typed: the tokens
/// before it, the word being typed, whether it is inside text (and what that text is for), the names declared and
/// visible there with their types, and whether it is at the top or inside a function. Built from the script with the
/// caret's line left out, so a half-typed line doesn't hide the rest.
/// </summary>
internal sealed class ScriptContext
{
	// How many unclosed blocks around the caret are closed to read the script
	private const int MostEndsAdded = 3;

	public string Source { get; private init; }
	public int Caret { get; private init; }
	public ScriptKind Kind { get; private init; }
	public IPlayerLibrary Library { get; private init; }

	/// <summary>The tokens before the word at the caret, comments left out.</summary>
	public List<Token> Before { get; private init; }

	/// <summary>The part of a name typed before the caret, and where it starts.</summary>
	public string Word { get; private init; }
	public int WordStart { get; private init; }

	/// <summary>When the caret is inside text: where its quote is (else -1), and what is typed of it.</summary>
	public int StringStart { get; private init; } = -1;
	public string StringSoFar { get; private init; }

	/// <summary>Whether the caret is inside a comment.</summary>
	public bool InComment { get; private init; }

	public IReadOnlyList<ScriptSymbol> Symbols { get; private init; }
	public SyntaxTree Tree { get; private init; }

	public static ScriptContext Of( string source, int caret, ScriptKind kind, IPlayerLibrary library )
	{
		source ??= "";
		caret = Math.Clamp( caret, 0, source.Length );
		var wordStart = caret;
		while ( wordStart > 0 && (char.IsAsciiLetterOrDigit( source[ wordStart - 1 ] ) || source[ wordStart - 1 ] == '_') )
			wordStart--;

		var head = source[ ..wordStart ];
		var tokens = Lexer.All( head, new LineMap( head ), out var error );
		var stringStart = error is not null && error.Start < head.Length && head[ error.Start ] is '"' or '\'' ? error.Start : -1;
		var last = tokens.Count >= 2 ? tokens[ ^2 ] : default;
		var inComment = last.Kind == TokenKind.Comment && last.End == head.Length && TokenLayout.IsLineComment( head, last )
			|| error is not null && error.Message.StartsWith( "This comment" );

		var before = new List<Token>();
		foreach ( var token in tokens )
		{
			if ( token.Kind is not (TokenKind.Comment or TokenKind.EndOfFile) )
				before.Add( token );
		}

		var blanked = Blank( source, caret );
		var compiled = ScriptCompiler.Compile( blanked, kind, library );

		// A block still being typed has no end yet: read it as the editor's closing would have it
		for ( var ends = 1; ends <= MostEndsAdded && compiled.Tree?.Error is not null; ends++ )
		{
			var closed = ScriptCompiler.Compile( blanked + string.Concat( System.Linq.Enumerable.Repeat( "\nend", ends ) ), kind, library );
			if ( closed.Tree?.Error is null )
				compiled = closed;
		}

		return new ScriptContext
		{
			Source = source, Caret = caret, Kind = kind, Library = library, Before = before, Word = source[ wordStart..caret ], WordStart = wordStart,
			StringStart = stringStart, StringSoFar = stringStart >= 0 ? source[ (stringStart + 1)..caret ] : null, InComment = inComment,
			Symbols = compiled.Symbols, Tree = compiled.Tree,
		};
	}

	// The source with the caret's line replaced by spaces, keeping every offset
	private static string Blank( string source, int caret )
	{
		var start = caret > 0 ? source.LastIndexOf( '\n', caret - 1 ) + 1 : 0;
		var end = source.IndexOf( '\n', caret );
		end = end < 0 ? source.Length : end;
		return source[ ..start ] + new string( ' ', end - start ) + source[ end.. ];
	}

	/// <summary>The names declared and seen at <paramref name="offset"/>, the latest of each name.</summary>
	public List<ScriptSymbol> Visible( int offset )
	{
		var visible = new Dictionary<string, ScriptSymbol>();
		foreach ( var symbol in Symbols ?? Array.Empty<ScriptSymbol>() )
		{
			if ( symbol.DeclaredAt < offset && symbol.ScopeStart <= offset && offset <= symbol.ScopeEnd )
				visible[ symbol.Name ] = symbol;
		}

		return new List<ScriptSymbol>( visible.Values );
	}

	/// <summary>Where the caret is, as a place library names are allowed (the top, or inside a function).</summary>
	public ScriptPlace Place
	{
		get
		{
			if ( Kind == ScriptKind.Shape )
				return ScriptPlace.ShapeBuild;

			if ( Kind == ScriptKind.Sticker )
				return ScriptPlace.StickerRun;

			if ( Kind == ScriptKind.Skin )
				return ScriptPlace.SkinRun;

			return Tree?.Root is { } root && InFunction( root, Caret ) ? ScriptPlace.Effect : ScriptPlace.EffectBuild;
		}
	}

	private static bool InFunction( Block block, int offset )
	{
		foreach ( var statement in block.Statements )
		{
			if ( offset < statement.Start || offset > statement.End )
				continue;

			if ( statement is FunctionStmt || statement is AssignStmt { Value: FunctionExpr } )
				return true;
		}

		return false;
	}

	/// <summary>The dotted chain the word at the caret is the last part of: "glow" for <c>glow.Si|ze</c>; empty for none.</summary>
	public List<string> ChainToWord() =>
		Before.Count > 0 && Before[ ^1 ].Kind == TokenKind.Dot ? ChainBefore( Before.Count - 1 ) : new List<string>();

	/// <summary>
	/// The names of the dotted chain ending just before token <paramref name="end"/> (exclusive) in <see cref="Before"/>:
	/// for <c>glow.Palette.</c> "glow", "Palette". Empty when there is no plain chain there.
	/// </summary>
	public List<string> ChainBefore( int end )
	{
		var names = new List<string>();
		var k = end - 1;
		while ( k >= 0 && Before[ k ].Kind == TokenKind.Name )
		{
			names.Insert( 0, Text( Before[ k ] ) );
			if ( k == 0 || Before[ k - 1 ].Kind != TokenKind.Dot )
				break;

			k -= 2;
		}

		return names;
	}

	public string Text( Token token ) => Source.Substring( token.Start, token.Length );

	/// <summary>
	/// What a chain of names is: a namespace (its name), or a value's type (and the library entry of its last part).
	/// </summary>
	public (string Namespace, ScriptType Type, LibraryEntry Entry) Resolve( IReadOnlyList<string> chain, int offset )
	{
		if ( chain.Count == 0 )
			return (null, null, null);

		var head = chain[ 0 ];
		ScriptType type = null;
		LibraryEntry entry = null;
		var symbol = Visible( offset ).Find( x => x.Name == head );
		if ( symbol is not null )
			type = symbol.Type;
		else if ( ScriptLibrary.Namespace( head ) is not null )
		{
			if ( chain.Count == 1 )
				return (head, null, ScriptLibrary.Namespace( head ));

			entry = ScriptLibrary.InNamespace( head, chain[ 1 ] );
			type = entry?.Type;
			chain = chain.Count > 2 ? Slice( chain, 1 ) : null;
			if ( chain is null )
				return (null, type, entry);
		}
		else if ( ScriptLibrary.Global( head ) is { } global )
			(type, entry) = (global.Type, global);

		for ( var i = 1; i < chain.Count && type is not null; i++ )
		{
			entry = ScriptLibrary.Member( type.Kind, chain[ i ] );
			type = entry?.Type;
		}

		return (null, type, entry);
	}

	private static List<string> Slice( IReadOnlyList<string> chain, int from )
	{
		var slice = new List<string>();
		for ( var i = from; i < chain.Count; i++ )
			slice.Add( chain[ i ] );

		return slice;
	}
}
