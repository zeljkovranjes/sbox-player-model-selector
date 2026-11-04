using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>The names a script declares: its own locals, its script variables, its functions, and the scopes they live in.</summary>
public sealed partial class Compiler
{
	private Symbol Lookup( string name )
	{
		var function = F;
		for ( var s = function.Scopes.Count - 1; s >= 0; s-- )
		{
			var scope = function.Scopes[ s ];
			for ( var i = scope.Count - 1; i >= 0; i-- )
			{
				if ( scope[ i ].Name == name )
					return scope[ i ];
			}
		}

		for ( var i = _globals.Count - 1; i >= 0; i-- )
		{
			if ( _globals[ i ].Name == name )
				return _globals[ i ];
		}

		return null;
	}

	// Declares a variable in the current block: a script variable at the very top, a local anywhere else
	private Symbol Declare( string name, ScriptType type, int start, int scopeEnd )
	{
		var function = F;
		Symbol symbol;
		if ( function.IsMain && function.BlockDepth == 0 )
		{
			symbol = new Symbol { Name = name, Kind = SymbolKind.Global, Type = type, Slot = _globalSlots, Start = start };
			_globalSlots += type.Width;
			if ( _globalSlots > ScriptLimits.MaxGlobals )
				Error( $"The script has too many variables (more than {ScriptLimits.MaxGlobals} slots)", start, name.Length );

			_globals.Add( symbol );
			_symbols.Add( new ScriptSymbol( name, type, "variable", start, start, _tree.Source.Length ) );
			return symbol;
		}

		symbol = new Symbol { Name = name, Kind = SymbolKind.Local, Type = type, Slot = Reserve( type.Width ), Start = start };
		function.Scopes[ ^1 ].Add( symbol );
		_symbols.Add( new ScriptSymbol( name, type, "local", start, start, scopeEnd ) );
		return symbol;
	}

	// Reserves slots in the current frame
	private int Reserve( int width )
	{
		var function = F;
		var slot = function.NextSlot;
		function.NextSlot += width;
		function.MaxSlot = Math.Max( function.MaxSlot, function.NextSlot );
		if ( function.NextSlot > ScriptLimits.MaxLocals )
			Error( $"A function has at most {ScriptLimits.MaxLocals} local value slots", _position, 1 );

		return slot;
	}

	private void EnterScope()
	{
		F.Scopes.Add( new List<Symbol>() );
		F.BlockDepth++;
	}

	private void LeaveScope( int mark )
	{
		F.Scopes.RemoveAt( F.Scopes.Count - 1 );
		F.BlockDepth--;
		F.NextSlot = mark;
	}

	private void Load( Symbol symbol )
	{
		switch ( symbol.Kind )
		{
			case SymbolKind.Local:
				Emit( OpCode.LoadLocal, symbol.Slot, symbol.Type.Width );
				Push( symbol.Type.Width );
				break;
			case SymbolKind.Global:
				Emit( OpCode.LoadGlobal, symbol.Slot, symbol.Type.Width );
				Push( symbol.Type.Width );
				break;
			case SymbolKind.Function:
				PushConstant( new double[] { symbol.Function } );
				break;
			case SymbolKind.Alias when symbol.Input >= 0:
				Emit( OpCode.LoadInput, (int)symbol.Input, symbol.Type.Width );
				Push( symbol.Type.Width );
				break;
		}
	}

	private void Store( Symbol symbol )
	{
		Emit( symbol.Kind == SymbolKind.Global ? OpCode.StoreGlobal : OpCode.StoreLocal, symbol.Slot, symbol.Type.Width );
		Pop( symbol.Type.Width );
	}
}
