using System.Collections.Generic;

namespace PlayerModels.Core.Script;

public sealed partial class Parser
{
	// Binding strength of each binary operator: or, and, comparisons, + -, * / %
	private static int Precedence( TokenKind kind ) => kind switch
	{
		TokenKind.Or => 1,
		TokenKind.And => 2,
		TokenKind.Equal or TokenKind.NotEqual or TokenKind.Less or TokenKind.LessEqual or TokenKind.Greater or TokenKind.GreaterEqual => 3,
		TokenKind.Plus or TokenKind.Minus => 4,
		TokenKind.Star or TokenKind.Slash or TokenKind.Percent => 5,
		_ => 0,
	};

	private Expr ParseExpr()
	{
		if ( !Enter() )
			return null;

		var expression = ParseBinary( 0 );
		Leave();
		return expression;
	}

	private Expr ParseBinary( int least )
	{
		var start = Current.Start;
		var left = ParseUnary();
		while ( _error is null )
		{
			var op = Current;
			var precedence = Precedence( op.Kind );
			if ( precedence <= least )
				break;

			Advance();
			if ( !Enter() )
				return null;

			var right = ParseBinary( precedence );
			Leave();
			left = Made( new BinaryExpr { Operator = op.Kind, Left = left, Right = right, OperatorStart = op.Start }, start );
		}

		return left;
	}

	private Expr ParseUnary()
	{
		var op = Current;
		if ( op.Kind is not (TokenKind.Not or TokenKind.Minus) || _error is not null )
			return ParseSuffixed();

		Advance();
		if ( !Enter() )
			return null;

		var operand = ParseUnary();
		Leave();
		return Made( new UnaryExpr { Operator = op.Kind, Operand = operand }, op.Start );
	}

	private Expr ParseSuffixed()
	{
		var start = Current.Start;
		var expression = ParsePrimary();
		while ( _error is null )
		{
			switch ( Current.Kind )
			{
				case TokenKind.Dot:
				{
					Advance();
					if ( !Is( TokenKind.Name ) )
					{
						Fail( $"Expected a name after '.', found {Describe( Current )}", Current );
						return expression;
					}

					var name = Current;
					Advance();
					expression = Made( new MemberExpr { Target = expression, Name = Text( name ), NameStart = name.Start }, start );
					break;
				}

				// A [ starting a line starts an attribute, never an index
				case TokenKind.LeftBracket when !Current.NewlineBefore:
				{
					Advance();
					var index = ParseExpr();
					if ( !Expect( TokenKind.RightBracket, "']'" ) )
						return expression;

					expression = Made( new IndexExpr { Target = expression, Index = index }, start );
					break;
				}

				case TokenKind.LeftParen:
				{
					Advance();
					var arguments = new List<Expr>();
					while ( _error is null && !Is( TokenKind.RightParen ) )
					{
						arguments.Add( ParseExpr() );
						if ( !Is( TokenKind.Comma ) )
							break;

						Advance();
					}

					if ( !Expect( TokenKind.RightParen, "')' to close the call" ) )
						return expression;

					expression = Made( new CallExpr { Callee = expression, Arguments = arguments }, start );
					break;
				}

				case TokenKind.Colon when expression is not null && Peek( TokenKind.Name ):
					Fail( "Use '.' rather than ':' (methods are called like C#: layer.Emit( 10 ))", Current );
					return expression;

				default:
					return expression;
			}
		}

		return expression;
	}

	// Whether the token after the current one is of kind, reading it in when needed
	private bool Peek( TokenKind kind )
	{
		var saved = _at;
		Advance();
		var found = _error is null && Current.Kind == kind;
		_at = saved;
		return found;
	}

	private Expr ParsePrimary()
	{
		var token = Current;
		if ( _error is not null )
			return null;

		switch ( token.Kind )
		{
			case TokenKind.Number:
				Advance();
				return Made( new NumberExpr { Value = Lexer.NumberOf( _source, token ) }, token.Start );
			case TokenKind.String:
				Advance();
				return Made( new StringExpr { Value = Lexer.TextOf( _source, token ) }, token.Start );
			case TokenKind.True or TokenKind.False:
				Advance();
				return Made( new BoolExpr { Value = token.Kind == TokenKind.True }, token.Start );
			case TokenKind.Name:
				Advance();
				return Made( new NameExpr { Name = Text( token ) }, token.Start );
			case TokenKind.LeftParen:
			{
				Advance();
				var inner = ParseExpr();
				Expect( TokenKind.RightParen, "')'" );
				return Made( new ParenExpr { Inner = inner }, token.Start );
			}

			case TokenKind.LeftBrace: return ParseList();
			case TokenKind.Function:
				Advance();
				return Made( new FunctionExpr { Body = ParseFunctionBody( token.Start ) }, token.Start );
			case TokenKind.If: return ParseIfExpr();
			case TokenKind.Nil:
				Fail( "There is no nil: every value has a type and starts with a value", token );
				return null;
			default:
				Fail( $"Expected a value, found {Describe( token )}", token );
				return null;
		}
	}

	private Expr ParseList()
	{
		var start = Current.Start;
		Advance();
		var items = new List<Expr>();
		while ( _error is null && !Is( TokenKind.RightBrace ) )
		{
			if ( items.Count >= ScriptLimits.MaxListLength )
			{
				Fail( $"A list has at most {ScriptLimits.MaxListLength} items", Current );
				return null;
			}

			items.Add( ParseExpr() );
			if ( !Is( TokenKind.Comma ) )
				break;

			Advance();
		}

		Expect( TokenKind.RightBrace, "'}' to close the list" );
		return Made( new ListExpr { Items = items }, start );
	}

	private Expr ParseIfExpr()
	{
		var start = Current.Start;
		var conditions = new List<Expr>();
		var values = new List<Expr>();
		do
		{
			Advance();
			conditions.Add( ParseExpr() );
			if ( !Expect( TokenKind.Then, "'then'" ) )
				return null;

			values.Add( ParseExpr() );
		}
		while ( Is( TokenKind.ElseIf ) );

		if ( !Expect( TokenKind.Else, "'else' (an if-expression always has one)" ) )
			return null;

		var otherwise = ParseExpr();
		return Made( new IfExpr { Conditions = conditions, Values = values, Else = otherwise }, start );
	}

	private TypeSyntax ParseType()
	{
		var token = Current;
		if ( _error is not null || !Enter() )
			return null;

		TypeSyntax type = null;
		if ( token.Kind == TokenKind.Name )
		{
			Advance();
			type = Made( new TypeSyntax { Name = Text( token ) }, token.Start );
		}
		else if ( token.Kind == TokenKind.LeftBrace )
		{
			Advance();
			var element = ParseType();
			if ( Expect( TokenKind.RightBrace, "'}' to close the list type" ) )
				type = Made( new TypeSyntax { Element = element }, token.Start );
		}
		else if ( token.Kind == TokenKind.LeftParen )
		{
			Advance();
			var parameters = new List<TypeSyntax>();
			while ( _error is null && !Is( TokenKind.RightParen ) )
			{
				parameters.Add( ParseType() );
				if ( !Is( TokenKind.Comma ) )
					break;

				Advance();
			}

			if ( Expect( TokenKind.RightParen, "')'" ) && Expect( TokenKind.Arrow, "'->' and the function's return type" ) )
				type = Made( new TypeSyntax { Parameters = parameters, Return = ParseType() }, token.Start );
		}
		else
			Fail( $"Expected a type (like number, Color or {{number}}), found {Describe( token )}", token );

		Leave();
		return type;
	}
}
