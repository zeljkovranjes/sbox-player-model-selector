using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// A colour a palette script works out for one element of a row as time passes: a moving sticker's fill (see
/// <see cref="Sticker.Motion"/> and <see cref="PresetScripts.StickerMotionScript"/>), or a picture of a layer's colour
/// cycle on a tile. Made when it starts showing; <see cref="At"/> is asked each frame and allocates nothing.
/// </summary>
public sealed class MovingColour
{
	private const double MillisecondsPerSecond = 1000;

	private readonly ScriptMachine _machine;
	private readonly ScriptStack _stack;
	private readonly ScriptIO _io = new();

	private MovingColour( ScriptMachine machine, ScriptStack stack ) => (_machine, _stack) = (machine, stack);

	/// <summary>
	/// <paramref name="script"/> (a sticker script) going through <paramref name="palette"/> (hex colours; the game's five
	/// for none), compiled by <paramref name="cache"/>, run on <paramref name="stack"/>; null for no script or one that
	/// doesn't compile.
	/// </summary>
	public static MovingColour Of( string script, IReadOnlyList<string> palette, ScriptCache cache, ScriptStack stack )
	{
		if ( script is null || cache.Compile( script, ScriptKind.Sticker ) is not { Succeeded: true } compiled )
			return null;

		var machine = new ScriptMachine( compiled.Program );
		machine.SetPalette( palette );
		return new MovingColour( machine, stack );
	}

	/// <summary>
	/// A sticker's fill moving as <paramref name="motion"/> does, through <paramref name="cycle"/> (its cycle's colours)
	/// or, pulsing, its own <paramref name="fill"/>; null for one that doesn't move.
	/// </summary>
	public static MovingColour OfSticker( Sticker.Motion motion, IReadOnlyList<string> cycle, string fill, ScriptCache cache, ScriptStack stack ) =>
		Of( PresetScripts.StickerMotionScript( motion ), motion == Sticker.Motion.Pulse ? new[] { fill ?? "#ffffff" } : cycle, cache, stack );

	/// <summary>
	/// Its colour <paramref name="ms"/> milliseconds in (already at its own speed), as element <paramref name="index"/> of
	/// <paramref name="count"/>; white when the script faults.
	/// </summary>
	public (float R, float G, float B) At( long ms, int index, int count )
	{
		var io = _io;
		io.Outputs[ (int)ScriptOutput.ColorR ] = io.Outputs[ (int)ScriptOutput.ColorG ] = io.Outputs[ (int)ScriptOutput.ColorB ] = 1;
		io.OutputsSet = 0;
		io.Set( ScriptInput.Time, ms );
		io.Set( ScriptInput.Seconds, ms / MillisecondsPerSecond );
		io.Set( ScriptInput.Index, index );
		io.Set( ScriptInput.Count, Math.Max( 1, count ) );
		if ( !_machine.RunElement( io, _stack ) )
			return (1f, 1f, 1f);

		var outputs = io.Outputs;
		return ((float)outputs[ (int)ScriptOutput.ColorR ], (float)outputs[ (int)ScriptOutput.ColorG ], (float)outputs[ (int)ScriptOutput.ColorB ]);
	}
}
