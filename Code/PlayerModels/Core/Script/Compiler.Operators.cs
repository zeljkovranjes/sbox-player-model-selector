namespace PlayerModels.Core.Script;

/// <summary>The operators: arithmetic on numbers, colours and vectors, comparisons, and short-circuit and/or.</summary>
public sealed partial class Compiler
{
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
}
