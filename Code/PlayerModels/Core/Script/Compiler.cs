using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// Checks a parsed script's types and compiles it to bytecode in one pass, like C#'s compiler would check it: every name
/// must be the script's own or in <see cref="ScriptLibrary"/>, every value of the type it is used as, each library name
/// used where it works (a layer made at the top, an input read inside a function). Errors are plain English at
/// line:col. Functions are compiled where they are declared and see the script variables declared before them (Lua's
/// rule), so the pass never goes back.
/// </summary>
public sealed partial class Compiler
{
	private enum SymbolKind { Local, Global, Function, Alias }

	private sealed class Symbol
	{
		public string Name;
		public SymbolKind Kind;
		public ScriptType Type;
		public int Slot;
		public int Function = -1;
		public int Start;
		public ScriptInput Input = (ScriptInput)(-1);
		public bool IsParameter;
	}

	private sealed class FunctionState
	{
		public int Index;
		public string Name;
		public readonly List<int> Code = new();
		public readonly List<int> Positions = new();
		public readonly List<List<Symbol>> Scopes = new();
		public readonly List<List<int>> Breaks = new();
		public int NextSlot;
		public int MaxSlot;
		public int ParamWidth;
		public int Depth;
		public int MaxDepth;
		public ScriptType ReturnType;
		public bool IsMain;
		public ScriptPlace Place;
		public int BlockDepth;
	}

	private readonly SyntaxTree _tree;
	private readonly ScriptKind _kind;
	private readonly IPlayerLibrary _library;
	private readonly LineMap _lines;
	private readonly List<ScriptError> _errors = new();
	private readonly List<FunctionState> _functions = new();
	private readonly List<FunctionState> _active = new();
	private readonly List<Symbol> _globals = new();
	private readonly List<double> _constants = new();
	private readonly List<string> _strings = new();
	private readonly Dictionary<string, int> _stringIndex = new();
	private readonly List<double> _arena = new();
	private readonly List<ScriptSite> _sites = new();
	private readonly List<(int, PropertyId, int)> _runningSets = new();
	private readonly List<ScriptSymbol> _symbols = new();
	private readonly List<KeyLiteral> _keys = new();
	private readonly int[] _events = new int[ (int)ScriptEvent.Total ];
	private int _globalSlots = ScriptLibrary.ReservedSlots;
	private int _position;
	private Stmt _statement;
	private bool _hasCalls;

	private Compiler( SyntaxTree tree, ScriptKind kind, IPlayerLibrary library )
	{
		_tree = tree;
		_kind = kind;
		_library = library;
		_lines = tree.Lines;
		Array.Fill( _events, -1 );
		Intern( "" );
	}

	/// <summary>
	/// Compiles a parsed script of <paramref name="kind"/>, the player's own names checked against
	/// <paramref name="library"/>: the program, or null with <paramref name="errors"/> saying why.
	/// </summary>
	internal static ScriptProgram Compile( SyntaxTree tree, ScriptKind kind, IPlayerLibrary library, out List<ScriptError> errors )
	{
		return Compile( tree, kind, library, out errors, out _ );
	}

	/// <summary>The same, also giving the names it declares (even when it has errors), for completion and hover.</summary>
	internal static ScriptProgram Compile( SyntaxTree tree, ScriptKind kind, IPlayerLibrary library, out List<ScriptError> errors, out List<ScriptSymbol> symbols )
	{
		var compiler = new Compiler( tree, kind, library );
		var program = compiler.Run();
		errors = compiler._errors;
		symbols = compiler._symbols;
		return compiler._errors.Count == 0 ? program : null;
	}

	private FunctionState F => _active[ ^1 ];

	private ScriptProgram Run()
	{
		var main = Begin( "the script", MainPlace(), isMain: true );
		Block( _tree.Root, isFunctionBody: true );
		Emit( OpCode.Halt );
		End();

		if ( _errors.Count > 0 )
			return null;

		var code = new List<int>();
		var positions = new List<int>();
		var functions = new FunctionInfo[ _functions.Count ];
		foreach ( var function in _functions )
		{
			functions[ function.Index ] = new FunctionInfo( code.Count, function.ParamWidth, function.MaxSlot, function.MaxDepth, function.Name );
			code.AddRange( function.Code );
			positions.AddRange( function.Positions );
		}

		if ( code.Count > ScriptLimits.MaxCode )
		{
			Error( $"The script compiles too big (more than {ScriptLimits.MaxCode} instructions)", 0, 1 );
			return null;
		}

		var names = new Dictionary<string, (int, ScriptType)>();
		foreach ( var global in _globals )
		{
			if ( global.Kind == SymbolKind.Global )
				names[ global.Name ] = (global.Slot, global.Type);
		}

		return new ScriptProgram
		{
			Kind = _kind, Source = _tree.Source, Lines = _lines, Code = code.ToArray(), Positions = positions.ToArray(),
			Constants = _constants.ToArray(), Strings = _strings.ToArray(), Functions = functions, GlobalSlots = _globalSlots,
			Arena = _arena.ToArray(), EventFunctions = _events, HasCalls = _hasCalls, Sites = _sites, RunningSets = _runningSets,
			Symbols = _symbols, Keys = _keys, GlobalNames = names, Parameters = _parameters,
		};
	}

	// Where the top of a script of this kind runs
	private ScriptPlace MainPlace() => _kind switch
	{
		ScriptKind.Effect => ScriptPlace.EffectBuild,
		ScriptKind.Shape => ScriptPlace.ShapeBuild,
		_ => ScriptPlace.StickerRun,
	};

	private FunctionState Begin( string name, ScriptPlace place, bool isMain = false )
	{
		var function = new FunctionState { Index = _functions.Count, Name = name, Place = place, IsMain = isMain };
		if ( _functions.Count >= ScriptLimits.MaxFunctions + 1 )
			Error( $"A script has at most {ScriptLimits.MaxFunctions} functions", _position, 1 );

		_functions.Add( function );
		_active.Add( function );
		function.Scopes.Add( new List<Symbol>() );
		return function;
	}

	private void End() => _active.RemoveAt( _active.Count - 1 );

	// --- Errors ---

	private void Error( string message, Node at ) => Error( message, at?.Start ?? _position, at is null ? 1 : at.End - at.Start );

	private void Error( string message, int start, int length )
	{
		if ( _errors.Count < ScriptLimits.MaxErrors )
			_errors.Add( _lines.Error( message, start, length ) );
	}

	// --- Emitting ---

	private void Emit( OpCode op ) => Put( (int)op );

	private void Emit( OpCode op, int a )
	{
		Put( (int)op );
		Put( a );
	}

	private void Emit( OpCode op, int a, int b )
	{
		Put( (int)op );
		Put( a );
		Put( b );
	}

	private void Emit( OpCode op, int a, int b, int c )
	{
		Put( (int)op );
		Put( a );
		Put( b );
		Put( c );
	}

	private void Put( int value )
	{
		F.Code.Add( value );
		F.Positions.Add( _position );
	}

	// The value stack grows and shrinks by what each instruction pushes and pops; the deepest it gets sizes the frame
	private void Push( int slots )
	{
		F.Depth += slots;
		F.MaxDepth = Math.Max( F.MaxDepth, F.Depth );
	}

	private void Pop( int slots ) => F.Depth -= slots;

	private int Here => F.Code.Count;

	// A jump whose target is patched later: returns where its operand is
	private int JumpForward( OpCode op )
	{
		Emit( op, 0 );
		return Here - 1;
	}

	private void Land( int operand ) => F.Code[ operand ] = Here - (operand + 1);

	private void JumpBack( OpCode op, int target )
	{
		Emit( op, 0 );
		F.Code[ Here - 1 ] = target - Here;
	}

	// --- Constants ---

	private int Constant( ReadOnlySpan<double> value )
	{
		var index = _constants.Count;
		foreach ( var slot in value )
			_constants.Add( slot );

		if ( _constants.Count > ScriptLimits.MaxConstants )
			Error( $"The script has too many constant values (more than {ScriptLimits.MaxConstants})", _position, 1 );

		return index;
	}

	private void PushConstant( ReadOnlySpan<double> value )
	{
		Emit( OpCode.Const, Constant( value ), value.Length );
		Push( value.Length );
	}

	private int Intern( string text )
	{
		if ( _stringIndex.TryGetValue( text, out var index ) )
			return index;

		index = _strings.Count;
		_strings.Add( text );
		_stringIndex[ text ] = index;
		return index;
	}

	// --- Symbols ---

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

	/// <summary>The closest of <paramref name="candidates"/> to <paramref name="name"/>, for "did you mean"; null when none is close.</summary>
	internal static string Closest( string name, IEnumerable<string> candidates )
	{
		// A name of one or two letters is only matched by case: anything is a letter or two away from it
		string best = null;
		var bestDistance = (name.Length <= 2 ? 0 : Math.Max( 2, name.Length / 3 )) + 1;
		foreach ( var candidate in candidates )
		{
			var distance = string.Equals( candidate, name, StringComparison.OrdinalIgnoreCase ) ? 0 : Distance( name, candidate );
			if ( distance < bestDistance )
				(best, bestDistance) = (candidate, distance);
		}

		return best;
	}

	// Levenshtein distance, short strings only (names)
	private static int Distance( string a, string b )
	{
		if ( Math.Abs( a.Length - b.Length ) > 3 )
			return int.MaxValue;

		var previous = new int[ b.Length + 1 ];
		var current = new int[ b.Length + 1 ];
		for ( var j = 0; j <= b.Length; j++ )
			previous[ j ] = j;

		for ( var i = 1; i <= a.Length; i++ )
		{
			current[ 0 ] = i;
			for ( var j = 1; j <= b.Length; j++ )
			{
				var cost = char.ToLowerInvariant( a[ i - 1 ] ) == char.ToLowerInvariant( b[ j - 1 ] ) ? 0 : 1;
				current[ j ] = Math.Min( Math.Min( current[ j - 1 ] + 1, previous[ j ] + 1 ), previous[ j - 1 ] + cost );
			}

			(previous, current) = (current, previous);
		}

		return previous[ b.Length ];
	}

	private static string Suggest( string name, IEnumerable<string> candidates ) =>
		Closest( name, candidates ) is { } close ? $": did you mean '{close}'?" : "";
}
