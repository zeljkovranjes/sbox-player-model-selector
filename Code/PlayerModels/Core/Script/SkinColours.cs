using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlayerModels.Core.Script;

/// <summary>
/// A weapon skin's moving colours (see <see cref="WeaponSkin"/>), worked out by its scripts on the CPU into a small table
/// the skin shader reads (<see cref="Texels"/> wide, <see cref="Rows"/> tall, four numbers each): the colour painted over
/// the weapon and how much it covers, its glow and sheen, the shade its own colour is multiplied by, and how far its
/// pattern is scrolled and spun. Its motion's script (see <see cref="PresetScripts.SkinMotionScript"/>) runs once a band
/// along the weapon, or at every texel when it reads <c>along</c>, or once in all when it reads neither (a pulse); its
/// pattern's script (<see cref="PresetScripts.SkinPatternMotion"/>) once. Filled once a frame for each different skin,
/// whoever wears it; <see cref="Fill"/> allocates nothing. Colours are in linear light, as the shader lights them: a
/// skin's palette reaches its script that way (a colour it works out itself, like the rainbow's, is used as it is).
/// </summary>
public sealed class SkinColours
{
	/// <summary>How many places along the weapon the table has, and how many rows: paint, glow, shade, pattern.</summary>
	public const int Texels = 64;
	public const int Rows = 4;

	/// <summary>Where in a texel's four numbers each row keeps what: paint (r, g, b, cover), glow (r, g, b, sheen), shade (r, g, b, 1), pattern (scroll, spin, 0, 0).</summary>
	public const int PaintRow = 0, GlowRow = 1, ShadeRow = 2, PatternRow = 3;

	private const double MillisecondsPerSecond = 1000;

	/// <summary>How the shader reads it: a band's own texel (a band script, or one that colours the weapon alike), else blended along the weapon.</summary>
	public bool Banded { get; }

	/// <summary>How many bands the weapon is coloured in (a banded table's first texels).</summary>
	public int Bands { get; }

	/// <summary>The table, row after row, four numbers a texel: what is uploaded each frame.</summary>
	public float[] Data { get; } = new float[ Texels * Rows * 4 ];

	private readonly ScriptMachine _motion;
	private readonly ScriptMachine _pattern;
	private readonly ScriptStack _stack;
	private readonly ScriptIO _io = new();
	private readonly double _speed;
	private readonly int _runs;

	private SkinColours( ScriptMachine motion, ScriptMachine pattern, ScriptStack stack, double speed, int bands )
	{
		(_motion, _pattern, _stack, _speed, Bands) = (motion, pattern, stack, speed, bands);
		var program = motion?.Program;
		Banded = program is null || !program.Reads( ScriptInput.Along );
		_runs = !Banded ? Texels : program is not null && (program.Reads( ScriptInput.Index ) || program.Reads( ScriptInput.Count )) ? bands : 1;
	}

	/// <summary>Whether <paramref name="skin"/> moves at all: a moving colour, or a pattern that scrolls or spins.</summary>
	public static bool Moves( WeaponSkin skin ) => skin is not null
		&& (skin.AnimationIs != WeaponSkin.Motion.None || skin.PatternIs != WeaponSkin.Pattern.None && (skin.PatternScroll != 0f || skin.PatternSpin != 0f));

	/// <summary>What tells one skin's moving colours from another's, as text: two skins alike here share one table.</summary>
	public static string KeyOf( WeaponSkin skin )
	{
		if ( !Moves( skin ) )
			return "";

		var patterned = skin.PatternIs != WeaponSkin.Pattern.None;
		return string.Join( "|", skin.AnimationIs, string.Join( ",", skin.Colours() ), Number( skin.Speed ), skin.Bands,
			Number( patterned ? skin.PatternScroll : 0f ), Number( patterned ? skin.PatternSpin : 0f ) );
	}

	/// <summary>
	/// <paramref name="skin"/>'s moving colours, its scripts compiled by <paramref name="cache"/> and run on
	/// <paramref name="stack"/>; null when it doesn't move (see <see cref="Moves"/>). A script that doesn't compile leaves
	/// its part still. Filled at its start before it is given back. Allocates: once per skin, never per frame.
	/// </summary>
	public static SkinColours Of( WeaponSkin skin, ScriptCache cache, ScriptStack stack )
	{
		if ( !Moves( skin ) )
			return null;

		ScriptMachine motion = null;
		if ( PresetScripts.SkinMotionScript( skin.AnimationIs ) is { } script && cache.Compile( script, ScriptKind.Skin ) is { Succeeded: true } compiled )
		{
			motion = new ScriptMachine( compiled.Program );
			motion.SetPalette( skin.Colours(), linear: true );
		}

		ScriptMachine pattern = null;
		if ( skin.PatternIs != WeaponSkin.Pattern.None && (skin.PatternScroll != 0f || skin.PatternSpin != 0f)
			&& cache.Compile( PresetScripts.SkinPatternMotion, ScriptKind.Skin ) is { Succeeded: true } moving )
		{
			pattern = new ScriptMachine( moving.Program );
			pattern.SetParameters( new Dictionary<string, string> { ["scrolls"] = Number( skin.PatternScroll ), ["spins"] = Number( skin.PatternSpin ) } );
		}

		var colours = new SkinColours( motion, pattern, stack, Math.Clamp( skin.Speed, 0f, WeaponSkin.MaxSpeed ), Math.Clamp( skin.Bands, WeaponSkin.MinBands, WeaponSkin.MaxBands ) );
		colours.Fill( 0 );
		return colours;
	}

	/// <summary>Fills <see cref="Data"/> for <paramref name="seconds"/> since the skin started showing. No allocations.</summary>
	public void Fill( double seconds )
	{
		// The pattern moves by the clock; the colours at the skin's own pace, in whole milliseconds as the clan tags count
		var (scroll, spin) = (0.0, 0.0);
		if ( _pattern is not null && Run( _pattern, (long)(seconds * MillisecondsPerSecond), seconds, 0, 1, 0.5 ) )
			(scroll, spin) = (_io.Get( ScriptOutput.Scroll ), _io.Get( ScriptOutput.Spin ));

		var ms = (long)(seconds * MillisecondsPerSecond * _speed);
		var bands = Bands;
		for ( var run = 0; run < _runs; run++ )
		{
			var along = Banded ? (run + 0.5) / bands : (run + 0.5) / Texels;
			var band = Banded ? run : Math.Min( (int)(along * bands), bands - 1 );
			var moved = _motion is not null && Run( _motion, ms, ms / MillisecondsPerSecond, band, bands, along );
			var outputs = _io.Outputs;
			var painted = moved && (_io.OutputsSet & ScriptIO.ColorSet) != 0;
			var cover = !painted ? 0.0 : (_io.OutputsSet & ScriptIO.AlphaSet) != 0 ? outputs[ (int)ScriptOutput.Alpha ] : 1.0;
			var first = _runs == 1 ? 0 : run;
			var last = _runs == 1 ? Texels - 1 : run;
			for ( var texel = first; texel <= last; texel++ )
			{
				Put( PaintRow, texel, Out( moved, ScriptOutput.ColorR, 1 ), Out( moved, ScriptOutput.ColorG, 1 ), Out( moved, ScriptOutput.ColorB, 1 ), cover );
				Put( GlowRow, texel, Out( moved, ScriptOutput.GlowR, 0 ), Out( moved, ScriptOutput.GlowG, 0 ), Out( moved, ScriptOutput.GlowB, 0 ),
					Out( moved, ScriptOutput.Sheen, 0 ) );
				Put( ShadeRow, texel, Out( moved, ScriptOutput.ShadeR, 1 ), Out( moved, ScriptOutput.ShadeG, 1 ), Out( moved, ScriptOutput.ShadeB, 1 ), 1 );
				Put( PatternRow, texel, scroll + Out( moved, ScriptOutput.Scroll, 0 ), spin + Out( moved, ScriptOutput.Spin, 0 ), 0, 0 );
			}
		}
	}

	/// <summary>Texel <paramref name="texel"/> of row <paramref name="row"/>, as its four numbers (for tests).</summary>
	public (float X, float Y, float Z, float W) At( int row, int texel )
	{
		var at = (row * Texels + texel) * 4;
		return (Data[ at ], Data[ at + 1 ], Data[ at + 2 ], Data[ at + 3 ]);
	}

	// One run of a script for band of bands at along, its outputs starting still (no paint, no glow, its own shade)
	private bool Run( ScriptMachine machine, long ms, double seconds, int band, int bands, double along )
	{
		var io = _io;
		var outputs = io.Outputs;
		Array.Clear( outputs );
		outputs[ (int)ScriptOutput.ColorR ] = outputs[ (int)ScriptOutput.ColorG ] = outputs[ (int)ScriptOutput.ColorB ] = outputs[ (int)ScriptOutput.ColorA ] = 1;
		outputs[ (int)ScriptOutput.ShadeR ] = outputs[ (int)ScriptOutput.ShadeG ] = outputs[ (int)ScriptOutput.ShadeB ] = outputs[ (int)ScriptOutput.ShadeA ] = 1;
		io.OutputsSet = 0;
		io.Set( ScriptInput.Time, ms );
		io.Set( ScriptInput.Seconds, seconds );
		io.Set( ScriptInput.Index, band );
		io.Set( ScriptInput.Count, bands );
		io.Set( ScriptInput.Along, along );
		return machine.RunElement( io, _stack );
	}

	// An output as the run left it, or its still value when the run faulted
	private double Out( bool moved, ScriptOutput output, double still ) => moved ? _io.Outputs[ (int)output ] : still;

	private void Put( int row, int texel, double x, double y, double z, double w )
	{
		var at = (row * Texels + texel) * 4;
		Data[ at ] = (float)x;
		Data[ at + 1 ] = (float)y;
		Data[ at + 2 ] = (float)z;
		Data[ at + 3 ] = (float)w;
	}

	private static string Number( float value ) => value.ToString( "R", CultureInfo.InvariantCulture );
}
