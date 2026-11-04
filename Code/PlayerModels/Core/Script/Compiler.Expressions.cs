using System.Collections.Generic;

namespace PlayerModels.Core.Script;

public sealed partial class Compiler
{
	/// <summary>Compiles <paramref name="expression"/> to push its value, giving its type; <paramref name="expected"/> helps type empty lists, functions and keys.</summary>
	private ScriptType Value( Expr expression, ScriptType expected = null )
	{
		if ( expression is null )
			return ScriptType.Error;

		var outer = _position;
		_position = expression.Start;
		var type = ValueOf( expression, expected );
		_position = outer;
		return type;
	}

	private ScriptType ValueOf( Expr expression, ScriptType expected )
	{
		if ( TryConstant( expression, expected, true, out var constantType, out var constant ) )
		{
			PushConstant( constant );
			return constantType;
		}

		return expression switch
		{
			ParenExpr group => Value( group.Inner, expected ),
			NameExpr name => Name( name ),
			MemberExpr member => Member( member ),
			IndexExpr index => Index( index ),
			CallExpr call => Call( call, allowVoid: false ),
			UnaryExpr unary => Unary( unary ),
			BinaryExpr binary => Binary( binary ),
			ListExpr list => List( list, expected ),
			IfExpr choice => IfValue( choice, expected ),
			FunctionExpr function => FunctionValue( function, expected ),
			_ => ScriptType.Error,
		};
	}

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

	// The namespace target names (Math, Joint, Color...) when it is one and no variable hides it
	private string NamespaceOf( Expr target ) =>
		target is NameExpr name && Lookup( name.Name ) is null && ScriptLibrary.Namespace( name.Name ) is not null ? name.Name : null;

	private ScriptType Name( NameExpr name )
	{
		if ( Lookup( name.Name ) is { } symbol )
		{
			if ( symbol.Kind == SymbolKind.Function && symbol.Type is null )
			{
				Error( $"'{name.Name}' uses itself, so give it a return type: function {name.Name}( ... ): number", name );
				return ScriptType.Error;
			}

			Load( symbol );
			return symbol.Type;
		}

		var entry = ScriptLibrary.Global( name.Name );
		if ( entry is null )
		{
			if ( ScriptLibrary.Namespace( name.Name ) is not null )
				Error( $"'{name.Name}' is a namespace: use one of its members, like {name.Name}.{First( name.Name )}", name );
			else if ( ScriptLibrary.Event( name.Name ) is not null )
				Error( $"{name.Name} is an event the game calls: declare it with function {name.Name}( ... ) ... end", name );
			else
				Error( $"'{name.Name}' isn't declared{Suggest( name.Name, Visible() )}", name );

			return ScriptType.Error;
		}

		if ( !Allowed( entry, name ) )
			return ScriptType.Error;

		var width = entry.Type.Width;
		switch ( entry.Kind )
		{
			case EntryKind.Input when entry.Access == PropertyAccess.Reserved:
				Emit( OpCode.LoadGlobal, entry.Slot, width );
				break;
			case EntryKind.Input:
				Emit( OpCode.LoadInput, entry.Slot, width );
				break;
			case EntryKind.Output:
				Emit( OpCode.LoadOutput, entry.Slot, width );
				break;
			default:
				Error( $"'{name.Name}' is a function: call it, like {name.Name}()", name );
				return ScriptType.Error;
		}

		Push( width );
		return entry.Type;
	}

	private static string First( string space )
	{
		foreach ( var member in ScriptLibrary.NamespaceMembers( space ) )
			return member.Member;

		return "...";
	}

	// The names visible here, for "did you mean"
	private IEnumerable<string> Visible()
	{
		foreach ( var scope in F.Scopes )
		{
			foreach ( var symbol in scope )
				yield return symbol.Name;
		}

		foreach ( var global in _globals )
			yield return global.Name;

		foreach ( var entry in ScriptLibrary.TopLevel() )
		{
			if ( (entry.Where & F.Place) != 0 && !entry.Name.StartsWith( "namespace:" ) )
				yield return entry.Name;
		}
	}

	private ScriptType Member( MemberExpr member )
	{
		if ( NamespaceOf( member.Target ) is { } space )
		{
			var inSpace = ScriptLibrary.InNamespace( space, member.Name );
			if ( inSpace is null )
				Error( $"'{space}.{member.Name}' doesn't exist{Suggest( member.Name, MemberNames( ScriptLibrary.NamespaceMembers( space ) ) )}", member );
			else if ( Allowed( inSpace, member ) )
				Error( $"{space}.{member.Name} is a function: call it, like {inSpace.Signature()}", member );

			return ScriptType.Error;
		}

		var owner = Value( member.Target );
		if ( owner.Kind == TypeKind.Error )
			return ScriptType.Error;

		var entry = ScriptLibrary.Member( owner.Kind, member.Name );
		if ( entry is null )
		{
			UnknownMember( owner, member );
			return ScriptType.Error;
		}

		if ( entry.Kind == EntryKind.Method )
		{
			Error( $"{entry.Name} is a method: call it, like {member.Name}( ... )", member );
			return ScriptType.Error;
		}

		if ( !Allowed( entry, member ) )
			return ScriptType.Error;

		var width = entry.Type.Width;
		switch ( entry.Access )
		{
			case PropertyAccess.Component: Emit( OpCode.Pick, owner.Width, entry.Slot ); break;
			case PropertyAccess.ListCount: Emit( OpCode.ListCount ); break;
			case PropertyAccess.Intrinsic: Emit( OpCode.Intrinsic, entry.Slot ); break;
			case PropertyAccess.Input: Emit( OpCode.LoadInput, entry.Slot, width ); break;
			case PropertyAccess.Output: Emit( OpCode.LoadOutput, entry.Slot, width ); break;
			default: Emit( OpCode.GetProp, entry.Slot ); break;
		}

		Pop( owner.Width );
		Push( width );
		return entry.Type;
	}

	private static IEnumerable<string> MemberNames( IEnumerable<LibraryEntry> entries )
	{
		foreach ( var entry in entries )
			yield return entry.Member;
	}

	private void UnknownMember( ScriptType owner, MemberExpr member )
	{
		var known = MemberNames( ScriptLibrary.MembersOf( owner.Kind ) );
		Error( $"{Article( owner )} has no '{member.Name}'{Suggest( member.Name, known )}", member.NameStart, member.Name.Length );
	}

	private ScriptType Index( IndexExpr index )
	{
		var list = Value( index.Target );
		if ( list.Kind != TypeKind.List )
		{
			if ( list.Kind != TypeKind.Error )
				Error( $"Only a list can be indexed with [ ], not {Article( list )}", index.Target );

			return ScriptType.Error;
		}

		var at = Value( index.Index, ScriptType.Number );
		if ( at.Kind is not (TypeKind.Number or TypeKind.Error) )
			Error( $"A list is indexed by a number (from 0), not {Article( at )}", index.Index );

		Emit( OpCode.Index, list.Element.Width );
		Pop( 3 );
		Push( list.Element.Width );
		return list.Element;
	}

	private ScriptType Unary( UnaryExpr unary )
	{
		var operand = Value( unary.Operand, unary.Operator == TokenKind.Not ? ScriptType.Bool : null );
		if ( unary.Operator == TokenKind.Not )
		{
			if ( operand.Kind is not (TypeKind.Bool or TypeKind.Error) )
				Error( $"'not' needs a bool, not {Article( operand )}", unary.Operand );

			Emit( OpCode.Not );
			return ScriptType.Bool;
		}

		switch ( operand.Kind )
		{
			case TypeKind.Number: Emit( OpCode.Neg ); break;
			case TypeKind.Vector3 or TypeKind.Color: Emit( OpCode.NegW, operand.Width ); break;
			case TypeKind.Error: break;
			default:
				Error( $"'-' can't be used on {Article( operand )}", unary );
				return ScriptType.Error;
		}

		return operand;
	}

	private ScriptType Binary( BinaryExpr binary )
	{
		var op = binary.Operator;
		if ( op is TokenKind.And or TokenKind.Or )
			return Logical( binary );

		if ( op is TokenKind.Equal or TokenKind.NotEqual )
			return Equality( binary );

		var left = Value( binary.Left );
		var right = Value( binary.Right );
		if ( left.Kind == TypeKind.Error || right.Kind == TypeKind.Error )
			return ScriptType.Error;

		var (n, v, c) = (TypeKind.Number, TypeKind.Vector3, TypeKind.Color);
		var (code, width, result) = (op, left.Kind, right.Kind) switch
		{
			(TokenKind.Less, _, _) when left.Kind == n && right.Kind == n => (OpCode.Less, 0, ScriptType.Bool),
			(TokenKind.LessEqual, _, _) when left.Kind == n && right.Kind == n => (OpCode.LessEqual, 0, ScriptType.Bool),
			(TokenKind.Greater, _, _) when left.Kind == n && right.Kind == n => (OpCode.Greater, 0, ScriptType.Bool),
			(TokenKind.GreaterEqual, _, _) when left.Kind == n && right.Kind == n => (OpCode.GreaterEqual, 0, ScriptType.Bool),
			(TokenKind.Plus, var a, var b) when a == n && b == n => (OpCode.Add, 0, ScriptType.Number),
			(TokenKind.Minus, var a, var b) when a == n && b == n => (OpCode.Sub, 0, ScriptType.Number),
			(TokenKind.Star, var a, var b) when a == n && b == n => (OpCode.Mul, 0, ScriptType.Number),
			(TokenKind.Slash, var a, var b) when a == n && b == n => (OpCode.Div, 0, ScriptType.Number),
			(TokenKind.Percent, var a, var b) when a == n && b == n => (OpCode.Mod, 0, ScriptType.Number),
			(TokenKind.Plus, var a, var b) when a == b && (a == v || a == c) => (OpCode.AddW, left.Width, left),
			(TokenKind.Minus, var a, var b) when a == b && (a == v || a == c) => (OpCode.SubW, left.Width, left),
			(TokenKind.Star, var a, var b) when (a == v || a == c) && b == n => (OpCode.MulWN, left.Width, left),
			(TokenKind.Star, var a, var b) when a == n && (b == v || b == c) => (OpCode.MulNW, right.Width, right),
			(TokenKind.Star, var a, var b) when a == c && b == c => (OpCode.MulWW, 4, left),
			(TokenKind.Slash, var a, var b) when (a == v || a == c) && b == n => (OpCode.DivWN, left.Width, left),
			_ => (OpCode.Halt, -1, null),
		};

		if ( result is null )
		{
			var hint = op == TokenKind.Star && left.Kind == v && right.Kind == v ? ": use Vector3.Dot or Vector3.Cross" : "";
			Error( $"'{Parser.Spelling( op )}' can't be used on {Article( left )} and {Article( right )}{hint}", binary.OperatorStart, Parser.Spelling( op ).Length );
			return ScriptType.Error;
		}

		if ( width > 0 )
			Emit( code, width );
		else
			Emit( code );

		Pop( left.Width + right.Width );
		Push( result.Width );
		return result;
	}

	private ScriptType Logical( BinaryExpr binary )
	{
		var isAnd = binary.Operator == TokenKind.And;
		Condition( binary.Left );
		var shortCut = JumpForward( isAnd ? OpCode.JumpIfFalse : OpCode.JumpIfTrue );
		Pop( 1 );
		Condition( binary.Right );
		var end = JumpForward( OpCode.Jump );
		Pop( 1 );
		Land( shortCut );
		PushConstant( new[] { isAnd ? 0.0 : 1.0 } );
		Land( end );
		return ScriptType.Bool;
	}

	private ScriptType Equality( BinaryExpr binary )
	{
		// A key compared with text: the text is the key, checked here
		var (first, second) = binary.Left is StringExpr && binary.Right is not StringExpr ? (binary.Right, binary.Left) : (binary.Left, binary.Right);
		var left = Value( first );
		var right = Value( second, left );
		if ( left.Kind == TypeKind.Error || right.Kind == TypeKind.Error )
			return ScriptType.Bool;

		if ( !right.FitsInto( left ) && !left.FitsInto( right ) )
			Error( $"{Article( left )} can't be compared with {Article( right )}", binary.OperatorStart, 2 );
		else if ( left.Kind is TypeKind.List or TypeKind.Function || !left.IsStorable )
			Error( $"Two {left}s can't be compared with {Parser.Spelling( binary.Operator )}", binary.OperatorStart, 2 );

		Emit( binary.Operator == TokenKind.Equal ? OpCode.Equal : OpCode.NotEqual, left.Width );
		Pop( left.Width + right.Width );
		Push( 1 );
		return ScriptType.Bool;
	}

	private ScriptType List( ListExpr list, ScriptType expected )
	{
		var element = expected?.Kind == TypeKind.List ? expected.Element : null;
		if ( list.Items.Count == 0 )
		{
			if ( element is null )
				Error( "An empty list needs a type: local items: {number} = {}", list );

			PushConstant( new double[] { 0, 0 } );
			return ScriptType.ListOf( element ?? ScriptType.Error );
		}

		var constant = new List<double>();
		ScriptType constantType = null;
		foreach ( var item in list.Items )
		{
			if ( !TryConstant( item, element ?? constantType, false, out var itemType, out var value ) || constantType is not null && !itemType.Same( constantType ) )
			{
				constantType = null;
				break;
			}

			constantType ??= itemType;
			constant.AddRange( value );
		}

		if ( constantType is not null && constantType.Kind != TypeKind.Key )
		{
			if ( element is not null )
				Check( constantType, element, list );

			var at = ArenaReserve( constant.Count );
			for ( var i = 0; i < constant.Count; i++ )
				_arena[ at + i ] = constant[ i ];

			PushConstant( new double[] { at, list.Items.Count } );
			return ScriptType.ListOf( constantType );
		}

		ScriptType type = element;
		foreach ( var item in list.Items )
		{
			var itemType = Value( item, type );
			if ( type is null )
			{
				type = itemType;
				if ( !type.IsStorable )
					Error( $"A list can't hold {Article( type )}", item );
			}
			else if ( !itemType.FitsInto( type ) )
				Error( $"Every item of a list has one type: this is {Article( itemType )}, the list holds {type}s", item );
		}

		var start = ArenaReserve( list.Items.Count * type.Width );
		Emit( OpCode.MakeList, start, list.Items.Count, type.Width );
		Pop( list.Items.Count * type.Width );
		Push( 2 );
		return ScriptType.ListOf( type );
	}

	private int ArenaReserve( int slots )
	{
		var at = _arena.Count;
		for ( var i = 0; i < slots; i++ )
			_arena.Add( 0 );

		if ( _arena.Count > ScriptLimits.MaxListSlots )
			Error( $"The script's lists are too big together (more than {ScriptLimits.MaxListSlots} values)", _position, 1 );

		return at;
	}

	private ScriptType IfValue( IfExpr choice, ScriptType expected )
	{
		var ends = new List<int>();
		var type = expected;
		for ( var i = 0; i < choice.Conditions.Count; i++ )
		{
			Condition( choice.Conditions[ i ] );
			var next = JumpForward( OpCode.JumpIfFalse );
			Pop( 1 );
			type = Branch( choice.Values[ i ], type );
			ends.Add( JumpForward( OpCode.Jump ) );
			Pop( type.Width );
			Land( next );
		}

		type = Branch( choice.Else, type );
		foreach ( var end in ends )
			Land( end );

		return type;
	}

	private ScriptType Branch( Expr value, ScriptType type )
	{
		var valueType = Value( value, type );
		if ( type is null )
			return valueType;

		Check( valueType, type, value );
		return type;
	}

	private ScriptType FunctionValue( FunctionExpr function, ScriptType expected )
	{
		if ( !F.IsMain )
		{
			Error( "A function can't be made inside another function: make it at the top of the script", function );
			return ScriptType.Error;
		}

		var wanted = expected?.Kind == TypeKind.Function ? expected : null;
		var body = function.Body;
		var parameters = new List<ScriptType>();
		for ( var i = 0; i < body.Parameters.Count; i++ )
		{
			var parameter = body.Parameters[ i ];
			if ( parameter.Type is not null )
				parameters.Add( TypeOf( parameter.Type ) );
			else if ( wanted is not null && i < wanted.Parameters.Count )
				parameters.Add( wanted.Parameters[ i ] );
			else
			{
				Error( $"Give '{parameter.Name}' a type, like {parameter.Name}: number", parameter );
				parameters.Add( ScriptType.Error );
			}
		}

		var returns = body.ReturnType is not null ? TypeOf( body.ReturnType ) : wanted?.Return;
		var place = parameters.Exists( x => x.Kind is TypeKind.Particle or TypeKind.Shot ) ? ScriptPlace.EffectRun : HelperPlace();
		var (index, type) = Function( body, "the function", parameters, returns, place, null );
		PushConstant( new double[] { index } );
		return type;
	}
}
