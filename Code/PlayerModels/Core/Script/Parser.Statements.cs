using System.Collections.Generic;

namespace PlayerModels.Core.Script;

public sealed partial class Parser
{
	private Block ParseBlock()
	{
		var block = new Block();
		var start = Current.Start;
		if ( !Enter() )
			return block;

		while ( _error is null )
		{
			var kind = Current.Kind;
			if ( kind is TokenKind.End or TokenKind.Else or TokenKind.ElseIf or TokenKind.EndOfFile )
				break;

			if ( kind == TokenKind.Semicolon )
			{
				Advance();
				continue;
			}

			var first = _at;
			Break( _blockDepth );

			var statement = ParseStatement();
			if ( statement is null || _error is not null )
				break;

			statement.FirstToken = first;
			block.Statements.Add( statement );
			if ( statement is ReturnStmt or BreakStmt && Current.Kind is not (TokenKind.End or TokenKind.Else or TokenKind.ElseIf or TokenKind.EndOfFile or TokenKind.Semicolon) )
				Fail( $"'{(statement is ReturnStmt ? "return" : "break")}' must be the last statement of its block", Current );
		}

		Leave();
		return Made( block, start );
	}

	private Stmt ParseStatement()
	{
		var token = Current;
		switch ( token.Kind )
		{
			case TokenKind.Local: return ParseLocal();
			case TokenKind.LeftBracket: return ParseAttributed();
			case TokenKind.Function: return ParseFunctionStatement( false, token.Start );
			case TokenKind.If: return ParseIf();
			case TokenKind.While: return ParseWhile();
			case TokenKind.For: return ParseFor();
			case TokenKind.Return: return ParseReturn();
			case TokenKind.Break:
				Advance();
				return Made( new BreakStmt(), token.Start );
			case TokenKind.Nil:
				Fail( "There is no nil: every value has a type and starts with a value", token );
				return null;
			case TokenKind.Repeat or TokenKind.Until:
				Fail( "There is no repeat ... until: use while ... do ... end", token );
				return null;
			case TokenKind.Goto:
				Fail( "There is no goto: use if, while or break", token );
				return null;
			case TokenKind.LeftParen:
				Fail( "A statement can't start with '(': assign or call something", token );
				return null;
			default: return ParseExpressionStatement();
		}
	}

	private Stmt ParseLocal()
	{
		var start = Current.Start;
		Advance();
		if ( Is( TokenKind.Function ) )
			return ParseFunctionStatement( true, start );

		if ( !Is( TokenKind.Name ) )
		{
			Fail( $"Expected a name after 'local', found {Describe( Current )}", Current );
			return null;
		}

		var name = Current;
		Advance();
		TypeSyntax type = null;
		if ( Is( TokenKind.Colon ) )
		{
			Advance();
			type = ParseType();
		}

		Expr value = null;
		if ( Is( TokenKind.Assign ) )
		{
			Advance();
			value = ParseExpr();
		}
		else if ( Is( TokenKind.Comma ) )
		{
			Fail( "Declare one local at a time: local a = 1 then local b = 2", Current );
			return null;
		}

		return Made( new LocalStmt { Name = Text( name ), NameStart = name.Start, Type = type, Value = value }, start );
	}

	// [Parameter( "Title", Name = value, ... )] local name = value
	private Stmt ParseAttributed()
	{
		var start = Current.Start;
		Advance();
		if ( !Is( TokenKind.Name ) )
		{
			Fail( $"Expected an attribute's name after '[', like [Parameter], found {Describe( Current )}", Current );
			return null;
		}

		var name = Current;
		Advance();
		var attribute = new AttributeSyntax { Name = Text( name ) };
		if ( Is( TokenKind.LeftParen ) )
		{
			Advance();
			while ( _error is null && !Is( TokenKind.RightParen ) )
			{
				if ( Is( TokenKind.Name ) && Peek( TokenKind.Assign ) )
				{
					var argument = Current;
					Advance();
					Advance();
					attribute.Named.Add( (Text( argument ), argument.Start, ParseExpr()) );
				}
				else
					attribute.Arguments.Add( ParseExpr() );

				if ( !Is( TokenKind.Comma ) )
					break;

				Advance();
			}

			if ( !Expect( TokenKind.RightParen, "')' to close the attribute" ) )
				return null;
		}

		if ( !Expect( TokenKind.RightBracket, "']' to close the attribute" ) )
			return null;

		Made( attribute, start );
		if ( !Is( TokenKind.Local ) )
		{
			Fail( $"An attribute goes on a local: [{attribute.Name}] then local name = value, found {Describe( Current )}", Current );
			return null;
		}

		Continue( _blockDepth );
		var declared = ParseLocal();
		if ( declared is FunctionStmt function )
			Fail( "An attribute goes on a value, not a function", function.Target.Start, function.Target.End - function.Target.Start );

		if ( declared is not LocalStmt local )
			return null;

		local.Attribute = attribute;
		local.Start = start;
		return local;
	}

	private Stmt ParseFunctionStatement( bool isLocal, int start )
	{
		Advance();
		if ( !Is( TokenKind.Name ) )
		{
			Fail( $"Expected the function's name, found {Describe( Current )}", Current );
			return null;
		}

		var first = Current;
		Expr target = Made( new NameExpr { Name = Text( first ) }, first.Start );
		Advance();
		if ( Is( TokenKind.Dot ) && !isLocal )
		{
			Advance();
			if ( !Is( TokenKind.Name ) )
			{
				Fail( "Expected a name after '.'", Current );
				return null;
			}

			var member = Current;
			Advance();
			target = Made( new MemberExpr { Target = target, Name = Text( member ), NameStart = member.Start }, first.Start );
		}

		if ( Is( TokenKind.Colon ) )
		{
			Fail( "Use '.' rather than ':' (methods are called like C#: layer.Emit( 10 ))", Current );
			return null;
		}

		var body = ParseFunctionBody( first.Start );
		return Made( new FunctionStmt { Target = target, Body = body, IsLocal = isLocal }, start );
	}

	private FunctionBody ParseFunctionBody( int start )
	{
		var opener = Current;
		var parameters = new List<Parameter>();
		if ( !Expect( TokenKind.LeftParen, "'(' and the function's parameters" ) )
			return null;

		while ( Is( TokenKind.Name ) )
		{
			var name = Current;
			Advance();
			TypeSyntax type = null;
			if ( Is( TokenKind.Colon ) )
			{
				Advance();
				type = ParseType();
			}

			parameters.Add( Made( new Parameter { Name = Text( name ), Type = type }, name.Start ) );
			if ( !Is( TokenKind.Comma ) )
				break;

			Advance();
		}

		if ( !Expect( TokenKind.RightParen, "')' after the parameters" ) )
			return null;

		TypeSyntax returnType = null;
		if ( Is( TokenKind.Colon ) )
		{
			Advance();
			returnType = ParseType();
		}

		_blockDepth++;
		var body = ParseBlock();
		_blockDepth--;
		CloseWith( "function", opener );
		return Made( new FunctionBody { Parameters = parameters, ReturnType = returnType, Body = body }, start );
	}

	// Expects the 'end' that closes what opened at opener
	private void CloseWith( string what, Token opener )
	{
		if ( _error is not null )
			return;

		if ( Current.Kind != TokenKind.End )
		{
			var (line, _) = _lines.Position( opener.Start );
			Fail( $"Expected 'end' to close the '{what}' on line {line}, found {Describe( Current )}", Current );
			return;
		}

		Break( _blockDepth );
		Advance();
	}

	private Stmt ParseIf()
	{
		var opener = Current;
		var conditions = new List<Expr>();
		var blocks = new List<Block>();
		Block otherwise = null;
		do
		{
			if ( conditions.Count > 0 )
				Break( _blockDepth );

			Advance();
			conditions.Add( ParseExpr() );
			if ( !Expect( TokenKind.Then, "'then'" ) )
				return null;

			blocks.Add( Nested() );
		}
		while ( Is( TokenKind.ElseIf ) );

		if ( Is( TokenKind.Else ) )
		{
			Break( _blockDepth );
			Advance();
			otherwise = Nested();
		}

		CloseWith( "if", opener );
		return Made( new IfStmt { Conditions = conditions, Blocks = blocks, Else = otherwise }, opener.Start );
	}

	private Block Nested()
	{
		_blockDepth++;
		var block = ParseBlock();
		_blockDepth--;
		return block;
	}

	private Stmt ParseWhile()
	{
		var opener = Current;
		Advance();
		var condition = ParseExpr();
		if ( !Expect( TokenKind.Do, "'do'" ) )
			return null;

		var body = Nested();
		CloseWith( "while", opener );
		return Made( new WhileStmt { Condition = condition, Body = body }, opener.Start );
	}

	private Stmt ParseFor()
	{
		var opener = Current;
		Advance();
		if ( !Is( TokenKind.Name ) )
		{
			Fail( $"Expected the loop's name after 'for', found {Describe( Current )}", Current );
			return null;
		}

		var name = Current;
		Advance();
		if ( Is( TokenKind.Comma ) || Is( TokenKind.In ) )
		{
			Fail( "Only counting loops: for i = 0, list.Count - 1 do ... end", Current );
			return null;
		}

		if ( !Expect( TokenKind.Assign, "'=' and where the loop starts" ) )
			return null;

		var from = ParseExpr();
		if ( !Expect( TokenKind.Comma, "',' and where the loop ends" ) )
			return null;

		var to = ParseExpr();
		Expr step = null;
		if ( Is( TokenKind.Comma ) )
		{
			Advance();
			step = ParseExpr();
		}

		if ( !Expect( TokenKind.Do, "'do'" ) )
			return null;

		var body = Nested();
		CloseWith( "for", opener );
		return Made( new ForStmt { Name = Text( name ), NameStart = name.Start, From = from, To = to, Step = step, Body = body }, opener.Start );
	}

	private Stmt ParseReturn()
	{
		var start = Current.Start;
		Advance();
		Expr value = null;
		if ( _error is null && Current.Kind is not (TokenKind.End or TokenKind.Else or TokenKind.ElseIf or TokenKind.EndOfFile or TokenKind.Semicolon) )
			value = ParseExpr();

		return Made( new ReturnStmt { Value = value }, start );
	}

	private Stmt ParseExpressionStatement()
	{
		var start = Current.Start;
		var expression = ParseSuffixed();
		if ( _error is not null )
			return null;

		if ( Is( TokenKind.Assign ) )
		{
			if ( expression is not (NameExpr or MemberExpr or IndexExpr) )
			{
				Fail( "Only a name, a field or a list item can be assigned to", expression.Start, expression.End - expression.Start );
				return null;
			}

			Advance();
			var value = ParseExpr();
			return Made( new AssignStmt { Target = expression, Value = value }, start );
		}

		if ( expression is CallExpr call )
			return Made( new CallStmt { Call = call }, start );

		if ( Is( TokenKind.Comma ) )
			Fail( "Assign one thing at a time: a = 1 then b = 2", Current );
		else
			Fail( "This does nothing on its own: a statement assigns (x = 1) or calls (f())", expression.Start, expression.End - expression.Start );

		return null;
	}

	private string Text( Token token ) => _source.Substring( token.Start, token.Length );
}
