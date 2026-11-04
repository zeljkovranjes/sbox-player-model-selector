using System.Collections.Generic;

namespace PlayerModels.Core.Script;

public sealed partial class Compiler
{
	// Where a function the script declares itself runs
	private ScriptPlace HelperPlace() => _kind switch
	{
		ScriptKind.Effect => ScriptPlace.Effect,
		ScriptKind.Shape => ScriptPlace.ShapeBuild,
		ScriptKind.Skin => ScriptPlace.SkinRun,
		_ => ScriptPlace.StickerRun,
	};

	private void FunctionStatement( FunctionStmt statement )
	{
		if ( !F.IsMain || F.BlockDepth > 0 )
		{
			Error( "Functions are made at the top of the script, not inside functions or blocks", statement.Target );
			return;
		}

		if ( statement.Target is MemberExpr member )
		{
			AssignFunction( member, statement.Body );
			return;
		}

		var name = ((NameExpr)statement.Target).Name;
		if ( ScriptLibrary.Event( name ) is { } handler )
		{
			EventFunction( statement, name, handler );
			return;
		}

		if ( name.StartsWith( "On" ) && name.Length > 2 && char.IsUpper( name[ 2 ] ) )
		{
			var events = new List<string>();
			foreach ( var known in ScriptLibrary.AllEvents )
				events.Add( known.Name );

			if ( Suggestions.Closest( name, events ) is { } close )
				Error( $"There is no event '{name}': did you mean '{close}'?", statement.Target );
		}

		var parameters = AnnotatedParameters( statement.Body, name );
		var returns = statement.Body.ReturnType is null ? null : TypeOf( statement.Body.ReturnType );
		var symbol = new Symbol { Name = name, Kind = SymbolKind.Function, Start = statement.Target.Start };
		if ( returns is not null )
			symbol.Type = ScriptType.FunctionOf( parameters, returns );

		_globals.Add( symbol );
		var (_, type) = Function( statement.Body, name, parameters, returns, HelperPlace(), symbol );
		symbol.Type = type;
		_symbols.Add( new ScriptSymbol( name, type, "function", statement.Target.Start, statement.Target.Start, _tree.Source.Length ) );
	}

	private List<ScriptType> AnnotatedParameters( FunctionBody body, string name )
	{
		var types = new List<ScriptType>();
		foreach ( var parameter in body.Parameters )
		{
			if ( parameter.Type is null )
			{
				Error( $"Give '{parameter.Name}' a type, like {parameter.Name}: number", parameter );
				types.Add( ScriptType.Error );
			}
			else
				types.Add( TypeOf( parameter.Type ) );
		}

		return types;
	}

	private void EventFunction( FunctionStmt statement, string name, LibraryEntry handler )
	{
		if ( _kind != ScriptKind.Effect )
		{
			Error( $"{name} is an event: events are for effect scripts", statement.Target );
			return;
		}

		if ( _events[ (int)handler.Event ] >= 0 )
		{
			Error( $"{name} is already handled earlier in the script", statement.Target );
			return;
		}

		var body = statement.Body;
		if ( body.Parameters.Count > handler.Parameters.Count )
		{
			Error( handler.Parameters.Count == 0 ? $"{name} takes no parameters" : $"{name} takes at most {handler.Parameters.Count} parameter: {handler.Signature()}", body.Parameters[ handler.Parameters.Count ] );
			return;
		}

		var types = new List<ScriptType>();
		var inputs = new List<ScriptInput>();
		for ( var i = 0; i < body.Parameters.Count; i++ )
		{
			var wanted = handler.Parameters[ i ];
			if ( body.Parameters[ i ].Type is { } annotation && !TypeOf( annotation ).Same( wanted.Type ) )
				Error( $"{name}'s '{body.Parameters[ i ].Name}' is {Article( wanted.Type )}", annotation );

			types.Add( wanted.Type );
			inputs.Add( wanted.Input );
		}

		if ( body.ReturnType is not null )
			Error( $"{name} gives back nothing", body.ReturnType );

		var (index, _) = Function( body, name, types, ScriptType.Void, ScriptPlace.EffectRun, null, inputs );
		_events[ (int)handler.Event ] = index;
	}

	// layer.Particle = function( p ) ... end, or function layer.Particle( p ) ... end
	private void AssignFunction( MemberExpr target, FunctionBody body )
	{
		Assign( target, new FunctionExpr { Body = body, Start = body.Start, End = body.End } );
	}

	/// <summary>
	/// Compiles a function's body into a function of its own: its parameters (those of a context type, a particle or a
	/// shot, read straight from the inputs; an event's from its input), its return type given or worked out from its
	/// first return, and whether every way through gives back its value.
	/// </summary>
	private (int Index, ScriptType Type) Function( FunctionBody body, string name, IReadOnlyList<ScriptType> parameters, ScriptType returns,
		ScriptPlace place, Symbol self, IReadOnlyList<ScriptInput> inputs = null )
	{
		var outerStatement = _statement;
		var outerPosition = _position;
		var outerEnd = _scopeEnd;
		var function = Begin( name, place );
		if ( self is not null )
			self.Function = function.Index;

		for ( var i = 0; i < body.Parameters.Count && i < parameters.Count; i++ )
		{
			var parameter = body.Parameters[ i ];
			var type = parameters[ i ];
			var symbol = new Symbol { Name = parameter.Name, Type = type, Start = parameter.Start };
			if ( type.Kind is TypeKind.Particle or TypeKind.Shot || inputs is not null )
			{
				symbol.Kind = SymbolKind.Alias;
				symbol.Input = inputs is not null && type.Kind is not (TypeKind.Particle or TypeKind.Shot) ? inputs[ i ] : (ScriptInput)(-1);
			}
			else
			{
				symbol.Kind = SymbolKind.Local;
				symbol.Slot = Reserve( type.Width );
				function.ParamWidth += type.Width;
			}

			function.Scopes[ 0 ].Add( symbol );
			_symbols.Add( new ScriptSymbol( parameter.Name, type, "parameter", parameter.Start, body.Start, body.End ) );
		}

		function.ReturnType = returns;
		_scopeEnd = body.End;
		Block( body.Body );
		_position = body.End;
		function.ReturnType ??= ScriptType.Void;
		if ( function.ReturnType.Kind == TypeKind.Void )
			Emit( OpCode.Return, 0 );
		else
		{
			if ( !AlwaysReturns( body.Body ) )
				Error( $"'{name}' must give back {Article( function.ReturnType )} on every path", body.Start, 8 );

			Emit( OpCode.Fault, (int)FaultKind.NoReturn );
		}

		End();
		_statement = outerStatement;
		_position = outerPosition;
		_scopeEnd = outerEnd;
		return (function.Index, ScriptType.FunctionOf( parameters, function.ReturnType ));
	}

	private void Assign( Expr target, Expr value )
	{
		switch ( target )
		{
			case NameExpr name: AssignName( name, value ); break;
			case MemberExpr member: AssignMember( member, value ); break;
			case IndexExpr index: AssignIndex( index, value ); break;
		}
	}

	// list[ i ] = value: the item is changed where the list is, so every variable holding the list sees it
	private void AssignIndex( IndexExpr target, Expr value )
	{
		var list = Value( target.Target );
		if ( list.Kind != TypeKind.List )
		{
			if ( list.Kind != TypeKind.Error )
				Error( $"Only a list's items can be assigned to with [ ], not {Article( list )}'s", target.Target );

			return;
		}

		var at = Value( target.Index, ScriptType.Number );
		if ( at.Kind is not (TypeKind.Number or TypeKind.Error) )
			Error( $"A list is indexed by a number (from 0), not {Article( at )}", target.Index );

		Check( Value( value, list.Element ), list.Element, value );
		Emit( OpCode.StoreIndex, list.Element.Width );
		Pop( 3 + list.Element.Width );
	}

	private void AssignName( NameExpr target, Expr value )
	{
		if ( Lookup( target.Name ) is { } symbol )
		{
			if ( symbol.Kind == SymbolKind.Function )
				Error( $"'{target.Name}' is a function: it can't be assigned to", target );
			else if ( symbol.Kind == SymbolKind.Alias )
				Error( $"'{target.Name}' can't be assigned to: set its fields, like {target.Name}.Color = ...", target );
			else
			{
				Check( Value( value, symbol.Type ), symbol.Type, value );
				Store( symbol );
			}

			return;
		}

		var entry = ScriptLibrary.Global( target.Name );
		if ( entry is null )
		{
			Error( $"'{target.Name}' isn't declared: write local {target.Name} = ... to make it{Suggest( target.Name, Visible() )}", target );
			return;
		}

		if ( entry.Kind != EntryKind.Output )
		{
			Error( $"'{target.Name}' can't be changed", target );
			return;
		}

		if ( !Allowed( entry, target, write: true ) )
			return;

		Check( Value( value, entry.Type ), entry.Type, value );
		StoreOutput( entry );
	}

	private void StoreOutput( LibraryEntry entry )
	{
		var range = double.IsNaN( entry.Min ) ? -1 : Constant( new[] { entry.Min, entry.Max } );
		Emit( OpCode.StoreOutput, entry.Slot, entry.Type.Width, range );
		Pop( entry.Type.Width );
	}

	private void AssignMember( MemberExpr target, Expr value )
	{
		if ( NamespaceOf( target.Target ) is { } space )
		{
			Error( $"{space}.{target.Name} can't be changed", target );
			return;
		}

		var owner = Value( target.Target );
		if ( owner.Kind == TypeKind.Error )
			return;

		var entry = ScriptLibrary.Member( owner.Kind, target.Name );
		if ( entry is null || entry.Kind != EntryKind.Property )
		{
			UnknownMember( owner, target );
			return;
		}

		if ( !entry.IsWritable )
		{
			Error( $"{entry.Owner}.{entry.Member} is read-only", target );
			return;
		}

		if ( !Allowed( entry, target, write: true ) )
			return;

		Check( Value( value, entry.Type ), entry.Type, value );
		if ( entry.Access == PropertyAccess.Output )
		{
			StoreOutput( entry );
			return;
		}

		var site = Site( value, null );
		Emit( OpCode.SetProp, entry.Slot, site );
		Pop( entry.Type.Width + owner.Width );
		if ( !F.IsMain && target.Target is NameExpr layer && Lookup( layer.Name ) is { Kind: SymbolKind.Global } global && owner.Kind == TypeKind.Layer )
			_runningSets.Add( (global.Slot, (PropertyId)entry.Slot, site) );
	}

	// Records the statement a value is set or made by, for the visual editor
	private int Site( Expr value, string localName )
	{
		var statement = _statement;
		var parameter = value is NameExpr name && Lookup( name.Name ) is { IsParameter: true } ? name.Name : null;
		var site = new ScriptSite( statement?.Start ?? 0, statement?.End ?? 0, value?.Start ?? 0, value?.End ?? 0, IsLiteral( value ),
			F.IsMain && F.BlockDepth == 0, localName, parameter );
		_sites.Add( site );
		return _sites.Count - 1;
	}
}
