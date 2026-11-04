using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// A tooltip: the span it is about, its title (a signature, or <c>name: type</c>), its one-line doc and example (the
/// reference's own text), and a colour (<c>#rrggbb</c>) or key (<c>g</c>) to draw beside it.
/// </summary>
public sealed record HoverInfo( int Start, int Length, string Title, string Doc, string Example = null, string Color = null, string Key = null );

/// <summary>
/// What the code editor shows on hovering: a library function, field, keyword, key, colour or event (from the same
/// entries as the reference), or one of the script's own names with its type and where it is declared.
/// </summary>
public static class ScriptHover
{
	/// <summary>The tooltip for <paramref name="offset"/> in <paramref name="source"/>; null over nothing it knows.</summary>
	public static HoverInfo At( string source, int offset, ScriptKind kind = ScriptKind.Effect, IPlayerLibrary library = null )
	{
		if ( source is null || offset < 0 || offset >= source.Length || source.Length > ScriptLimits.MaxSourceLength )
			return null;

		var tokens = Lexer.All( source, new LineMap( source ), out _ );
		var index = tokens.FindIndex( x => offset >= x.Start && offset < x.End );
		if ( index < 0 )
			return null;

		var token = tokens[ index ];
		if ( ColorLiterals.At( source, offset ) is { } colour && token.Kind != TokenKind.Comment )
		{
			var hex = ScriptValue.ColorText( colour.R, colour.G, colour.B, colour.A );
			return new HoverInfo( colour.Start, colour.Length, "Color " + hex, "A colour: click its swatch to pick another.", null, hex );
		}

		switch ( token.Kind )
		{
			case TokenKind.String:
				return Text( source, token, kind, library );
			case TokenKind.Name:
				return Name( source, tokens, index, kind, library );
			case var keyword when Token.IsKeyword( keyword ) && ScriptLibrary.Keyword( source.Substring( token.Start, token.Length ) ) is { } entry:
				return new HoverInfo( token.Start, token.Length, entry.Name, entry.Doc, entry.Example );
			default:
				return null;
		}
	}

	private static HoverInfo Text( string source, Token token, ScriptKind kind, IPlayerLibrary library )
	{
		var compiled = ScriptCompiler.Compile( source, kind, library );
		foreach ( var key in compiled.Program?.Keys ?? new List<KeyLiteral>() )
		{
			if ( key.Start == token.Start )
			{
				var entry = ScriptLibrary.Key( key.Key );
				return new HoverInfo( token.Start, token.Length, $"Key \"{key.Key}\"", entry.Doc, null, null, key.Key );
			}
		}

		foreach ( var item in library?.Items ?? new List<LibraryItem>() )
		{
			if ( item.Name == Lexer.TextOf( source, token ) )
				return new HoverInfo( token.Start, token.Length, $"{item.Kind} \"{item.Name}\"", $"One of your own {item.Kind.ToString().ToLowerInvariant()}s." );
		}

		return null;
	}

	private static HoverInfo Name( string source, List<Token> tokens, int index, ScriptKind kind, IPlayerLibrary library )
	{
		var token = tokens[ index ];
		var name = source.Substring( token.Start, token.Length );
		if ( index > 0 && tokens[ index - 1 ].Kind == TokenKind.LeftBracket && ScriptLibrary.Attribute( name ) is { } attribute )
			return Entry( token, attribute );

		if ( index > 0 && tokens[ index - 1 ].Kind == TokenKind.Function && ScriptLibrary.Event( name ) is { } handler )
			return Entry( token, handler );

		if ( index + 1 < tokens.Count && tokens[ index + 1 ].Kind == TokenKind.Assign && ScriptLibrary.Attribute( "Parameter." + name ) is { } option && InsideAttribute( tokens, index ) )
			return Entry( token, option );

		var context = ScriptContext.Of( source, token.End, kind, library );
		var chain = context.ChainBefore( context.Before.Count );
		chain.Add( name );
		if ( chain.Count == 1 )
		{
			var symbol = context.Visible( token.Start + 1 ).Find( x => x.Name == name );
			if ( symbol is not null )
			{
				var line = new LineMap( source ).Position( symbol.DeclaredAt ).Line;
				return new HoverInfo( token.Start, token.Length, $"{name}: {symbol.Type}", $"Your {symbol.Kind}, declared on line {line}." );
			}

			if ( ScriptLibrary.Type( name ) is { } type && index > 0 && tokens[ index - 1 ].Kind is TokenKind.Colon or TokenKind.LeftBrace or TokenKind.Arrow )
				return Entry( token, type );
		}

		var (space, _, entry) = context.Resolve( chain, token.End );
		if ( space is not null )
			entry = ScriptLibrary.Namespace( space );

		return entry is null ? null : Entry( token, entry );
	}

	private static bool InsideAttribute( List<Token> tokens, int index )
	{
		for ( var k = index - 1; k >= 0; k-- )
		{
			if ( tokens[ k ].Kind == TokenKind.LeftBracket && tokens[ k ].NewlineBefore )
				return true;

			if ( tokens[ k ].Kind is TokenKind.RightBracket or TokenKind.Local )
				return false;
		}

		return false;
	}

	private static HoverInfo Entry( Token token, LibraryEntry entry )
	{
		var icon = ScriptCompletion.Icon( entry );
		return new HoverInfo( token.Start, token.Length, entry.Signature(), entry.Doc, entry.Example, icon?.StartsWith( "color:" ) == true ? icon[ 6.. ] : null,
			entry.Kind == EntryKind.Key ? entry.Name : null );
	}
}
