using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>Signature help: the call's signature, its parameters (name: type), the one being typed, and the doc.</summary>
public sealed record SignatureInfo( string Signature, IReadOnlyList<string> Parameters, int Active, string Doc );

/// <summary>
/// Signature help while typing inside a call's brackets: the library function or method (or the script's own
/// function) being called, with the parameter the caret is on.
/// </summary>
public static class ScriptSignature
{
	/// <summary>The signature help at <paramref name="caret"/>; null outside a call.</summary>
	public static SignatureInfo At( string source, int caret, ScriptKind kind = ScriptKind.Effect, IPlayerLibrary library = null )
	{
		var context = ScriptContext.Of( source, caret, kind, library );
		var before = context.Before;
		var depth = 0;
		var commas = 0;
		for ( var k = before.Count - 1; k >= 0; k-- )
		{
			var token = before[ k ].Kind;
			if ( token is TokenKind.RightParen or TokenKind.RightBracket or TokenKind.RightBrace )
				depth++;
			else if ( token is TokenKind.LeftBracket or TokenKind.LeftBrace )
			{
				if ( depth-- == 0 )
					return null;
			}
			else if ( token == TokenKind.LeftParen )
			{
				if ( depth-- > 0 )
					continue;

				return Of( context, context.ChainBefore( k ), commas );
			}
			else if ( token == TokenKind.Comma && depth == 0 )
				commas++;
		}

		return null;
	}

	private static SignatureInfo Of( ScriptContext context, List<string> callee, int active )
	{
		if ( callee.Count == 0 )
			return null;

		var (_, _, entry) = context.Resolve( callee, context.Caret );
		if ( callee.Count == 1 && ScriptLibrary.Global( callee[ 0 ] ) is { Kind: EntryKind.Function } global && entry is null )
			entry = global;

		if ( entry is { Kind: EntryKind.Function or EntryKind.Method } )
		{
			var parameters = new List<string>();
			foreach ( var parameter in entry.Parameters )
				parameters.Add( $"{parameter.Name}: {parameter.Type}" );

			return new SignatureInfo( entry.Signature(), parameters, active, entry.Doc );
		}

		return callee.Count == 1 ? Own( context, callee[ 0 ], active ) : null;
	}

	// One of the script's own functions, by its declaration
	private static SignatureInfo Own( ScriptContext context, string name, int active )
	{
		foreach ( var statement in context.Tree?.Root?.Statements ?? new List<Stmt>() )
		{
			if ( statement is not FunctionStmt { Target: NameExpr target } function || target.Name != name )
				continue;

			var parameters = new List<string>();
			foreach ( var parameter in function.Body.Parameters )
				parameters.Add( parameter.Type is null ? parameter.Name : $"{parameter.Name}: {context.Source.Substring( parameter.Type.Start, parameter.Type.End - parameter.Type.Start )}" );

			return new SignatureInfo( $"{name}( {string.Join( ", ", parameters )} )", parameters, active, "Your function." );
		}

		return null;
	}
}
