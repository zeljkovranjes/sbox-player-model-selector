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
