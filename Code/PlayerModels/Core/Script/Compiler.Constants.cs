using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>Values known while compiling: literals, named constants, colours and vectors of them, keys; and which values the visual editor can rewrite.</summary>
public sealed partial class Compiler
{
	/// <summary>
	/// Whether <paramref name="expression"/> is a value known while compiling (a literal, a named constant, a colour or
	/// vector made of them), and that value; errors in it (a bad colour or key) are reported when <paramref name="report"/>.
	/// </summary>
	private bool TryConstant( Expr expression, ScriptType expected, bool report, out ScriptType type, out double[] value )
	{
		type = null;
		value = null;
		switch ( expression )
		{
			case NumberExpr number:
				(type, value) = (ScriptType.Number, new[] { number.Value });
				return true;
			case BoolExpr flag:
				(type, value) = (ScriptType.Bool, new[] { flag.Value ? 1.0 : 0.0 });
				return true;
			case StringExpr text when expected?.Kind == TypeKind.Key:
				return KeyConstant( text, report, out type, out value );
			case StringExpr text:
				(type, value) = (ScriptType.String, new double[] { Intern( text.Value ) });
				return true;
			case ParenExpr group:
				return TryConstant( group.Inner, expected, report, out type, out value );
			case UnaryExpr { Operator: TokenKind.Minus, Operand: NumberExpr negated }:
				(type, value) = (ScriptType.Number, new[] { -negated.Value });
				return true;
			case NameExpr name when Lookup( name.Name ) is null && ScriptLibrary.Global( name.Name ) is { Kind: EntryKind.Constant } entry:
				if ( report && !Allowed( entry, name ) )
					return false;

				(type, value) = (entry.Type, entry.Value);
				return true;
			case MemberExpr member when NamespaceOf( member.Target ) is { } space && ScriptLibrary.InNamespace( space, member.Name ) is { Kind: EntryKind.Constant } entry:
				(type, value) = (entry.Type, entry.Value);
				return true;
			case CallExpr call:
				return ConstantCall( call, report, out type, out value );
			default:
				return false;
		}
	}

	private bool KeyConstant( StringExpr text, bool report, out ScriptType type, out double[] value )
	{
		type = ScriptType.Key;
		value = null;
		var key = Effect.KeyNamed( text.Value );
		if ( key is null )
		{
			if ( report )
				Error( $"\"{text.Value}\" isn't a key you can bind: use a letter, a digit, f1 to f12, mouse4 or mouse5{Suggest( text.Value, Effect.Keys )}", text );

			return false;
		}

		if ( report )
			_keys.Add( new KeyLiteral( text.Start, text.End - text.Start, key ) );

		value = new double[] { KeyIndex( key ) };
		return true;
	}

	/// <summary>The index of <paramref name="key"/> (one of <see cref="Effect.Keys"/>), the value a key holds.</summary>
	internal static int KeyIndex( string key )
	{
		for ( var i = 0; i < Effect.Keys.Count; i++ )
		{
			if ( Effect.Keys[ i ] == key )
				return i;
		}

		return -1;
	}

	// Color( r, g, b[, a] ), Vector3( x, y, z ), Color.Parse( "#..." ), Color.FromHsv( h, s, v[, a] ) of literal numbers
	private bool ConstantCall( CallExpr call, bool report, out ScriptType type, out double[] value )
	{
		type = null;
		value = null;
		var callee = call.Callee switch
		{
			NameExpr name when Lookup( name.Name ) is null => name.Name,
			MemberExpr { Target: NameExpr space } member when NamespaceOf( space ) is not null => space.Name + "." + member.Name,
			_ => null,
		};

		if ( callee == "Color.Parse" && call.Arguments.Count == 1 && call.Arguments[ 0 ] is StringExpr hex )
		{
			if ( !ScriptMath.TryParseColor( hex.Value, out var r, out var g, out var b, out var a ) )
			{
				if ( report )
					Error( $"\"{hex.Value}\" isn't a colour: write it like \"#ff8800\" (or \"#f80\", or \"#ff880080\" with alpha)", hex );

				return false;
			}

			(type, value) = (ScriptType.Color, new[] { r, g, b, a });
			return true;
		}

		var (arguments, least, most) = callee switch
		{
			"Color" => (call.Arguments, 3, 4),
			"Color.FromHsv" => (call.Arguments, 3, 4),
			"Vector3" => (call.Arguments, 3, 3),
			_ => (null, 0, 0),
		};

		if ( arguments is null || arguments.Count < least || arguments.Count > most )
			return false;

		var numbers = new double[ 4 ];
		numbers[ 3 ] = 1;
		for ( var i = 0; i < arguments.Count; i++ )
		{
			if ( !TryConstant( arguments[ i ], ScriptType.Number, false, out var argumentType, out var argument ) || argumentType.Kind != TypeKind.Number )
				return false;

			numbers[ i ] = argument[ 0 ];
		}

		switch ( callee )
		{
			case "Vector3":
				(type, value) = (ScriptType.Vector3, new[] { numbers[ 0 ], numbers[ 1 ], numbers[ 2 ] });
				return true;
			case "Color":
				(type, value) = (ScriptType.Color, numbers);
				return true;
			default:
			{
				var (r, g, b) = ScriptMath.FromHsv( numbers[ 0 ], numbers[ 1 ], numbers[ 2 ] );
				(type, value) = (ScriptType.Color, new[] { r, g, b, numbers[ 3 ] });
				return true;
			}
		}
	}

	/// <summary>
	/// Whether <paramref name="expression"/> is a value the visual editor can rewrite in place: a literal or constant, a
	/// list of them, or a script variable (a drawn shape, say).
	/// </summary>
	private bool IsLiteral( Expr expression )
	{
		switch ( expression )
		{
			case ListExpr list:
				foreach ( var item in list.Items )
				{
					if ( !IsLiteral( item ) )
						return false;
				}

				return true;
			case NameExpr name when Lookup( name.Name ) is { Kind: SymbolKind.Global }:
				return true;
			case FunctionExpr:
				return false;
			default:
				return TryConstant( expression, null, false, out _, out _ );
		}
	}
}
