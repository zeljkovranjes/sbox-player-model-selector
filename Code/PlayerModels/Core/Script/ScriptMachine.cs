using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// Runs one compiled script for one wearer: builds it once (the top of an effect or shape script makes its layers and
/// shapes), then runs its event, particle and per-element functions as often as the host asks, each run within an
/// instruction budget and a call-depth limit. A run allocates nothing, never throws, and never leaves a value unsafe
/// (no NaN or infinity; outputs held to their ranges); a run that faults stops there and changes nothing more, and
/// <see cref="Fault"/> says why. Its script variables and lists live as long as it does.
/// </summary>
public sealed partial class ScriptMachine
{
	public ScriptProgram Program { get; }

	/// <summary>Why the last run stopped early; none when it finished.</summary>
	public ScriptFault Fault;

	/// <summary>
	/// How much of its budget the last run left: what it cost is the budget less this (a run that faulted left none). For
	/// a host holding many runs to one budget a frame.
	/// </summary>
	public int InstructionsLeft;

	internal readonly double[] Globals;
	internal string[] Strings;
	private double[][] _parameterValues;
	internal double[] Arena;
	internal double[] Live = Array.Empty<double>();
	internal ulong[] LiveSet = Array.Empty<ulong>();
	internal int Layers;

	private ScriptBuilder _builder;
	private ScriptIO _io;
	private IScriptHost _host;
	private ScriptStack _ownStack;
	private ScriptIO _ownIO;
	private int _paletteAt = -1;

	// How much of the arena is lists; the rest is room to grow into, so adding to it is cheap however often it's done
	private int _arenaUsed;

	public ScriptMachine( ScriptProgram program )
	{
		Program = program ?? throw new ArgumentNullException( nameof( program ) );
		Globals = new double[ program.GlobalSlots ];
		Arena = new double[ program.Arena.Length ];
		Array.Copy( program.Arena, Arena, Arena.Length );
		_arenaUsed = Arena.Length;
		Strings = program.Strings;
		SetPalette( null );
	}

	/// <summary>
	/// Chooses values for the script's parameters by name (as text, see <see cref="ScriptParameter.TryRead"/>), used from
	/// the next build or run on: each clamped to its parameter, names that aren't one and values that don't read dropped.
	/// Gives back what was kept, as the effect should store it. Not per frame (it allocates).
	/// </summary>
	public Dictionary<string, string> SetParameters( IReadOnlyDictionary<string, string> values )
	{
		var kept = new Dictionary<string, string>();
		var parameters = Program.Parameters;
		_parameterValues = new double[ parameters.Count ][];
		var strings = new List<string>( Program.Strings );
		for ( var i = 0; i < parameters.Count && values is not null; i++ )
		{
			var parameter = parameters[ i ];
			if ( !values.TryGetValue( parameter.Name, out var text ) || !parameter.TryRead( text, strings, out var value ) )
				continue;

			kept[ parameter.Name ] = parameter.Text( value, strings );
			if ( parameter.Kind == ParameterKind.Palette )
			{
				var at = AppendArena( value.Length );
				Array.Copy( value, 0, Arena, at, value.Length );
				value = new double[] { at, value.Length / 4 };
			}

			_parameterValues[ i ] = value;
		}

		Strings = strings.ToArray();
		return kept;
	}

	/// <summary>How many layers the built effect has.</summary>
	public int LayerCount => Layers;

	/// <summary>
	/// Builds an effect or shape script: runs its top once, within <see cref="ScriptLimits.BuildInstructions"/>, naming
	/// the player's own effects and shapes from <paramref name="library"/>. Allocates (it makes the effect); the result
	/// says what it made, or the fault that stopped it.
	/// </summary>
	public ScriptBuild Build( IPlayerLibrary library = null, ScriptStack stack = null )
	{
		_builder = new ScriptBuilder( this, library );
		_io = _ownIO ??= new ScriptIO();
		_host = null;
		var finished = Execute( 0, stack, ScriptLimits.BuildInstructions );
		var builder = _builder;
		_builder = null;
		if ( !finished )
			return ScriptBuild.Failed( Fault.Describe( Program ) );

		var build = builder.Finish();
		if ( build.Error is not null )
			return build;

		builder.FillLive();
		return build;
	}

	/// <summary>Runs the handler of <paramref name="scriptEvent"/>, if the script has one; false when it faulted.</summary>
	public bool RunEvent( ScriptEvent scriptEvent, ScriptIO io, IScriptHost host = null, ScriptStack stack = null, int budget = ScriptLimits.RunInstructions )
	{
		var function = Program.EventFunctions[ (int)scriptEvent ];
		return function < 0 || Run( function, io, host, stack, budget );
	}

	/// <summary>Whether layer <paramref name="layer"/> has a particle function (<c>function layer.Particle( p )</c>).</summary>
	public bool HasParticleFunction( int layer ) => ParticleFunction( layer ) >= 0;

	/// <summary>Runs layer <paramref name="layer"/>'s particle function for the particle in <paramref name="io"/>; false when it faulted.</summary>
	public bool RunParticle( int layer, ScriptIO io, IScriptHost host = null, ScriptStack stack = null, int budget = ScriptLimits.RunInstructions )
	{
		var function = ParticleFunction( layer );
		return function < 0 || Run( function, io, host, stack, budget );
	}

	/// <summary>Runs a sticker script for the element in <paramref name="io"/>; false when it faulted.</summary>
	public bool RunElement( ScriptIO io, ScriptStack stack = null, int budget = ScriptLimits.RunInstructions ) => Run( 0, io, null, stack, budget );

	/// <summary>
	/// Sets the palette a sticker script reads (<c>palette</c>, <c>highlight</c>, <c>rest</c>) from colours as hex: its
	/// own, or the game's five for none; the highlight and rest are its first two when it has two or more. In
	/// <paramref name="linear"/> light (a weapon skin's, as its shader lights them) rather than as picked. Not per frame.
	/// </summary>
	public void SetPalette( IReadOnlyList<string> own, bool linear = false )
	{
		var colours = own is { Count: > 0 } ? own : ColorCycle.GamePalette;
		var count = Math.Min( colours.Count, EffectLayer.MaxPalette );
		if ( _paletteAt < 0 )
			_paletteAt = AppendArena( EffectLayer.MaxPalette * 4 );

		var at = _paletteAt;
		for ( var i = 0; i < count; i++ )
			WriteColor( colours[ i ], Arena, at + i * 4, linear );

		Globals[ ScriptLibrary.PaletteSlot ] = at;
		Globals[ ScriptLibrary.PaletteSlot + 1 ] = count;
		var two = own is { Count: >= 2 };
		WriteColor( two ? own[ 0 ] : ColorCycle.ScannerHighlight, Globals, ScriptLibrary.HighlightSlot, linear );
		WriteColor( two ? own[ 1 ] : ColorCycle.ScannerRest, Globals, ScriptLibrary.RestSlot, linear );
	}

	/// <summary>
	/// The value of a layer's property while it runs (its build value until a function changes it), slot
	/// <paramref name="component"/> of it (a colour's r, g, b, a; a vector's x, y, z).
	/// </summary>
	public double LiveValue( int layer, PropertyId property, int component = 0 ) =>
		Live[ layer * ScriptProperties.LiveBlockSize + ScriptProperties.LiveOffset[ (int)property ] + component ];

	/// <summary>Whether a function has changed a layer's property since <see cref="ClearLiveChanges"/>.</summary>
	public bool IsLiveChanged( int layer, PropertyId property ) => (LiveSet[ layer ] & (1UL << (int)property)) != 0;

	/// <summary>Forgets which layer properties functions changed, once the host has applied them.</summary>
	public void ClearLiveChanges() => Array.Clear( LiveSet );

	/// <summary>The value of the script variable <paramref name="name"/>, as text; null when there is none (for examples and tests).</summary>
	public string Variable( string name ) => Program.GlobalNames.TryGetValue( name, out var variable )
		? ScriptValue.Format( variable.Type, Globals.AsSpan( variable.Slot, variable.Type.Width ), Strings, Arena )
		: null;

	private int ParticleFunction( int layer )
	{
		if ( layer < 0 || layer >= Layers )
			return -1;

		var function = (int)Live[ layer * ScriptProperties.LiveBlockSize + ScriptProperties.LiveOffset[ (int)PropertyId.LayerParticle ] ];
		return function > 0 && function < Program.Functions.Length ? function : -1;
	}

	private bool Run( int function, ScriptIO io, IScriptHost host, ScriptStack stack, int budget )
	{
		_io = io ?? (_ownIO ??= new ScriptIO());
		_host = host;
		var finished = Execute( function, stack, budget );
		_host = null;
		return finished;
	}

	/// <summary>Adds <paramref name="slots"/> to the arena (while building, or setting a palette), giving where they start.</summary>
	internal int AppendArena( int slots )
	{
		var at = _arenaUsed;
		if ( at + slots > Arena.Length )
		{
			var grown = new double[ Math.Max( at + slots, Arena.Length * 2 ) ];
			Array.Copy( Arena, grown, at );
			Arena = grown;
		}

		_arenaUsed = at + slots;
		return at;
	}

	internal const string ArenaFull = "The script's lists are too big together";

	/// <summary>
	/// Adds <paramref name="slots"/> to the arena while building, as long as it stays within
	/// <see cref="ScriptLimits.MaxArenaSlots"/>: where they start, or -1 when that would be too many.
	/// </summary>
	internal int TryAppendArena( int slots ) => _arenaUsed + slots > ScriptLimits.MaxArenaSlots ? -1 : AppendArena( slots );

	/// <summary>
	/// Writes the colour <paramref name="hex"/> (white when it isn't one) as r, g, b, a at <paramref name="at"/>, its r, g
	/// and b in <paramref name="linear"/> light when asked.
	/// </summary>
	internal static void WriteColor( string hex, double[] into, int at, bool linear = false )
	{
		if ( !ScriptMath.TryParseColor( hex, out var r, out var g, out var b, out var a ) )
			(r, g, b, a) = (1, 1, 1, 1);

		into[ at ] = linear ? Linear( r ) : r;
		into[ at + 1 ] = linear ? Linear( g ) : g;
		into[ at + 2 ] = linear ? Linear( b ) : b;
		into[ at + 3 ] = a;
	}

	/// <summary>A colour channel as picked (sRGB) in linear light.</summary>
	public static double Linear( double srgb ) => srgb <= 0.04045 ? srgb / 12.92 : Math.Pow( (srgb + 0.055) / 1.055, 2.4 );

	private bool Stop( FaultKind kind, int pc, int index = 0, int count = 0, string message = null )
	{
		var positions = Program.Positions;
		Fault = new ScriptFault
		{
			Kind = kind, Position = pc >= 0 && pc < positions.Length ? positions[ pc ] : 0, Index = index, Count = count, Message = message,
		};

		return false;
	}
}
