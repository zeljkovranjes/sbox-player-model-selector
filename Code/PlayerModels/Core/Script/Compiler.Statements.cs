using System.Collections.Generic;

namespace PlayerModels.Core.Script;

public sealed partial class Compiler
{
	private int _scopeEnd;

	private void Block( Block block, bool isFunctionBody = false )
	{
		if ( block is null )
			return;

		var outerEnd = _scopeEnd;
		_scopeEnd = isFunctionBody && F.IsMain ? _tree.Source.Length : block.End;
		foreach ( var statement in block.Statements )
		{
			if ( _errors.Count >= ScriptLimits.MaxErrors )
				break;

			Statement( statement );
		}

		_scopeEnd = outerEnd;
	}

	private void Nested( Block block )
	{
		var mark = F.NextSlot;
		EnterScope();
		Block( block );
		LeaveScope( mark );
	}

	private void Statement( Stmt statement )
	{
		_statement = statement;
		_position = statement.Start;
		F.Depth = 0;
		switch ( statement )
		{
			case LocalStmt local: Local( local ); break;
			case AssignStmt assign: Assign( assign.Target, assign.Value ); break;
			case CallStmt call:
			{
				var type = Call( call.Call, allowVoid: true );
				if ( type.Width > 0 )
				{
					Emit( OpCode.Pop, type.Width );
					Pop( type.Width );
				}

				break;
			}

			case IfStmt branch: If( branch ); break;
			case WhileStmt loop: While( loop ); break;
			case ForStmt loop: For( loop ); break;
			case ReturnStmt give: Return( give ); break;
			case BreakStmt stop: Break( stop ); break;
			case FunctionStmt function: FunctionStatement( function ); break;
		}
	}

	private void Local( LocalStmt local )
	{
		if ( local.Attribute is not null )
		{
			ParameterLocal( local );
			return;
		}

		var declared = local.Type is null ? null : TypeOf( local.Type );
		ScriptType type;
		if ( local.Value is null )
		{
			type = declared ?? ScriptType.Error;
			if ( declared is null )
				Error( $"Give '{local.Name}' a value or a type: local {local.Name} = 0, or local {local.Name}: number", local );
			else if ( !HasDefault( declared ) )
				Error( $"A {declared} needs a starting value: local {local.Name}: {declared} = ...", local );

			PushConstant( new double[ type.Width ] );
		}
		else
		{
			var valueType = Value( local.Value, declared );
			if ( declared is not null )
				Check( valueType, declared, local.Value );

			type = declared ?? valueType;
			if ( !type.IsStorable )
			{
				Error( type.Kind == TypeKind.Void ? "This gives back nothing, so there is nothing to keep" : $"A {type} can't be kept in a variable: read its fields", local.Value );
				type = ScriptType.Error;
			}
		}

		Store( Declare( local.Name, type, local.NameStart, _scopeEnd ) );
	}

	private static bool HasDefault( ScriptType type ) => type.Kind is TypeKind.Number or TypeKind.Bool or TypeKind.String or TypeKind.Color
		or TypeKind.Vector3 or TypeKind.List or TypeKind.Enum or TypeKind.Error;

	private void Check( ScriptType actual, ScriptType wanted, Node at )
	{
		if ( !actual.FitsInto( wanted ) )
			Error( $"Cannot use {Article( actual )} as {Article( wanted )}", at );
	}

	private static string Article( ScriptType type )
	{
		var name = type.ToString();
		return type.Kind == TypeKind.Void ? "nothing" : ("aeiouAEIOU".Contains( name[ 0 ] ) ? "an " : "a ") + name;
	}

	private ScriptType Condition( Expr condition )
	{
		var type = Value( condition, ScriptType.Bool );
		if ( type.Kind is not (TypeKind.Bool or TypeKind.Error) )
			Error( $"A condition must be a bool (true or false), not {Article( type )}: compare it, like n > 0", condition );

		return type;
	}

	private void If( IfStmt branch )
	{
		var ends = new List<int>();
		for ( var i = 0; i < branch.Conditions.Count; i++ )
		{
			_position = branch.Conditions[ i ].Start;
			Condition( branch.Conditions[ i ] );
			var next = JumpForward( OpCode.JumpIfFalse );
			Pop( 1 );
			Nested( branch.Blocks[ i ] );
			if ( i < branch.Conditions.Count - 1 || branch.Else is not null )
				ends.Add( JumpForward( OpCode.Jump ) );

			Land( next );
		}

		if ( branch.Else is not null )
			Nested( branch.Else );

		foreach ( var end in ends )
			Land( end );
	}

	private void While( WhileStmt loop )
	{
		var start = Here;
		Condition( loop.Condition );
		var exit = JumpForward( OpCode.JumpIfFalse );
		Pop( 1 );
		F.Breaks.Add( new List<int>() );
		Nested( loop.Body );
		JumpBack( OpCode.Jump, start );
		Land( exit );
		foreach ( var stop in Pop( F.Breaks ) )
			Land( stop );
	}

	private void For( ForStmt loop )
	{
		var mark = F.NextSlot;
		EnterScope();
		var counter = Reserve( 3 );
		foreach ( var (part, offset) in new[] { (loop.From, 0), (loop.To, 1), (loop.Step, 2) } )
		{
			if ( part is null )
				PushConstant( new double[] { 1 } );
			else if ( Value( part, ScriptType.Number ) is { Kind: not (TypeKind.Number or TypeKind.Error) } type )
				Error( $"A for loop counts with numbers, not {Article( type )}", part );

			Emit( OpCode.StoreLocal, counter + offset, 1 );
			Pop( 1 );
		}

		var variable = Declare( loop.Name, ScriptType.Number, loop.NameStart, loop.Body.End );
		var start = Here;
		Emit( OpCode.ForCheck, counter, 0 );
		var exit = Here - 1;
		Emit( OpCode.LoadLocal, counter, 1 );
		Push( 1 );
		Store( variable );
		F.Breaks.Add( new List<int>() );
		Nested( loop.Body );
		Emit( OpCode.ForStep, counter );
		JumpBack( OpCode.Jump, start );
		Land( exit );
		foreach ( var stop in Pop( F.Breaks ) )
			Land( stop );

		LeaveScope( mark );
	}

	private static List<int> Pop( List<List<int>> stack )
	{
		var top = stack[ ^1 ];
		stack.RemoveAt( stack.Count - 1 );
		return top;
	}

	private void Break( BreakStmt stop )
	{
		if ( F.Breaks.Count == 0 )
		{
			Error( "'break' is only for leaving a loop", stop );
			return;
		}

		F.Breaks[ ^1 ].Add( JumpForward( OpCode.Jump ) );
	}

	private void Return( ReturnStmt give )
	{
		var function = F;
		if ( function.IsMain )
		{
			if ( give.Value is not null )
				Error( "The top of a script can't give back a value: just return", give.Value );

			Emit( OpCode.Halt );
			return;
		}

		if ( give.Value is null )
		{
			function.ReturnType ??= ScriptType.Void;
			if ( function.ReturnType.Kind is not (TypeKind.Void or TypeKind.Error) )
				Error( $"'{function.Name}' must give back {Article( function.ReturnType )}", give );

			Emit( OpCode.Return, 0 );
			return;
		}

		var type = Value( give.Value, function.ReturnType );
		if ( function.ReturnType is null )
		{
			if ( !type.IsStorable )
				Error( $"{Article( type )} can't be given back", give.Value );

			function.ReturnType = type;
		}
		else
			Check( type, function.ReturnType, give.Value );

		Emit( OpCode.Return, function.ReturnType.Width );
		Pop( function.ReturnType.Width );
	}

	private ScriptType TypeOf( TypeSyntax syntax )
	{
		if ( syntax is null )
			return ScriptType.Error;

		if ( syntax.Element is not null )
			return ScriptType.ListOf( TypeOf( syntax.Element ) );

		if ( syntax.Return is not null )
		{
			var parameters = new List<ScriptType>();
			foreach ( var parameter in syntax.Parameters )
				parameters.Add( TypeOf( parameter ) );

			return ScriptType.FunctionOf( parameters, syntax.Return.Name == "nothing" ? ScriptType.Void : TypeOf( syntax.Return ) );
		}

		if ( ScriptType.Named.TryGetValue( syntax.Name, out var named ) )
			return named;

		foreach ( var choices in ScriptEnums.All )
		{
			if ( choices.Name == syntax.Name )
				return choices.Type;
		}

		var names = new List<string>( ScriptType.Named.Keys );
		foreach ( var choices in ScriptEnums.All )
			names.Add( choices.Name );

		Error( $"There is no type '{syntax.Name}'{Suggest( syntax.Name, names )}", syntax );
		return ScriptType.Error;
	}

	// Whether every way through block ends in a return
	private static bool AlwaysReturns( Block block )
	{
		if ( block is null || block.Statements.Count == 0 )
			return false;

		switch ( block.Statements[ ^1 ] )
		{
			case ReturnStmt: return true;
			case IfStmt branch when branch.Else is not null:
				foreach ( var inner in branch.Blocks )
				{
					if ( !AlwaysReturns( inner ) )
						return false;
				}

				return AlwaysReturns( branch.Else );
			default: return false;
		}
	}
}
