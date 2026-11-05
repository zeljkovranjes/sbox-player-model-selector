using System;
using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>
/// The built-in colour modes and rhythms, written as scripts, give what the C# gives: the World at War cycles exactly
/// ColorCycle's values (which match the original JS) over its 13,325-sample grid, Mix and Fade exactly, and the rhythms
/// what Engine/EffectLayerObject.cs works out (its formulas copied below as the reference, in floats like the engine).
/// </summary>
public class ColorModeTests
{
	private static readonly int[] Counts = { 1, 4, 15, 21 };
	private const int GridEnd = 12000;
	private const int GridStep = 37;

	private static (ScriptMachine Machine, ScriptIO IO) Element( string script, IReadOnlyDictionary<string, string> parameters = null )
	{
		var machine = new ScriptMachine( ScriptKit.Compile( script, ScriptKind.Sticker ) );
		if ( parameters is not null )
			machine.SetParameters( parameters );

		return (machine, new ScriptIO());
	}

	private static double[] Run( ScriptMachine machine, ScriptIO io, double ms, int i, int count, double life = 0 )
	{
		io.Set( ScriptInput.Time, ms );
		io.Set( ScriptInput.Seconds, ms / 1000 );
		io.Set( ScriptInput.Index, i );
		io.Set( ScriptInput.Count, count );
		io.Set( ScriptInput.Life, life );
		io.OutputsSet = 0;
		Assert.True( machine.RunElement( io ), machine.Fault.Describe( machine.Program )?.ToString() );
		return io.Outputs;
	}

	private static string Hex( double[] outputs ) => ScriptValue.ColorText( outputs[ 0 ], outputs[ 1 ], outputs[ 2 ], 1 ).ToUpperInvariant();

	[Theory]
	[InlineData( "Rain" )]
	[InlineData( "Cycl" )]
	[InlineData( "Cyln" )]
	public void WorldAtWarCyclesMatchColorCycleOnItsGrid( string kind )
	{
		var script = kind switch { "Rain" => PresetScripts.Rain, "Cycl" => PresetScripts.Cycl, _ => PresetScripts.Cyln };
		var (machine, io) = Element( script );
		var samples = 0;
		foreach ( var count in Counts )
		{
			for ( long ms = 0; ms <= GridEnd; ms += GridStep )
			{
				for ( var i = 0; i < count; i++ )
				{
					var expected = kind == "Cyln"
						? ColorCycle.IsScannerHighlight( ms, i, count ) ? ColorCycle.ScannerHighlight : ColorCycle.ScannerRest
						: ColorCycle.GamePalette[ ColorCycle.PaletteIndex( Enum.Parse<ColorCycle.Kind>( kind ), ms, i, count, ColorCycle.GamePalette.Count ) ];

					Assert.Equal( expected, Hex( Run( machine, io, ms, i, count ) ) );
					samples++;
				}
			}
		}

		Assert.Equal( 13325, samples );
	}

	[Fact]
	public void CyclesUseAPlayersOwnPaletteOfAnyLength()
	{
		var own = new[] { "#112233", "#445566", "#778899" };
		var (machine, io) = Element( PresetScripts.Rain );
		machine.SetPalette( own );
		for ( long ms = 0; ms <= 2000; ms += GridStep )
		{
			for ( var i = 0; i < 7; i++ )
				Assert.Equal( own[ ColorCycle.PaletteIndex( ColorCycle.Kind.Rain, ms, i, 7, own.Length ) ].ToUpperInvariant(), Hex( Run( machine, io, ms, i, 7 ) ) );
		}
	}

	[Fact]
	public void MixGivesEachElementThePaletteInTurn()
	{
		var (machine, io) = Element( PresetScripts.Mix );
		for ( var i = 0; i < 40; i++ )
			Assert.Equal( ColorCycle.GamePalette[ i % ColorCycle.GamePalette.Count ], Hex( Run( machine, io, 0, i, 40 ) ) );
	}

	[Fact]
	public void FadeGoesFromStartToEndOverLife()
	{
		var (machine, io) = Element( PresetScripts.Fade, new Dictionary<string, string> { ["startColor"] = "#ff0000", ["endColor"] = "#0000ff" } );
		for ( var life = 0.0; life <= 1.0; life += 0.125 )
		{
			var outputs = Run( machine, io, 0, 0, 1, life );
			Assert.Equal( 1 - life, outputs[ 0 ], 12 );
			Assert.Equal( life, outputs[ 2 ], 12 );
		}
	}

	// The engine's own formulas (Engine/EffectLayerObject.cs: Beat, Flicker, Wobble), in its floats
	private static float PulseLevel( float beat, float amount ) => 1f - amount * (0.5f - 0.5f * MathF.Cos( beat * MathF.Tau ));

	private static float FlickerLevel( float beat, float amount )
	{
		var wave = 0.5f * MathF.Sin( beat * MathF.Tau ) + 0.3f * MathF.Sin( beat * MathF.Tau * 2.3f + 1.3f ) + 0.2f * MathF.Sin( beat * MathF.Tau * 5.1f + 0.7f );
		return 1f - amount * (0.5f + 0.5f * wave);
	}

	private static (float, float, float) Wobble( float beat )
	{
		var t = beat * MathF.Tau;
		return (MathF.Sin( t ) * 0.6f, MathF.Sin( t * 1.3f + 1.7f ) * 0.6f, MathF.Sin( t * 0.7f + 0.4f ) * 0.5f);
	}

	// Float maths against the script's doubles: they agree to well within a byte of colour (1/255) and a degree
	private const double Tolerance = 1e-4;
	private const double DegreesTolerance = 1e-2;

	private static readonly Dictionary<string, string> Beat = new() { ["speed"] = "1.2", ["amount"] = "0.35", ["phase"] = "0.618" };

	private static IEnumerable<float> Seconds()
	{
		for ( var s = 0f; s < 20f; s += 0.0137f )
			yield return s;
	}

	[Fact]
	public void PulseMatchesTheEngine()
	{
		var (machine, io) = Element( PresetScripts.Pulse, Beat );
		foreach ( var s in Seconds() )
			Assert.Equal( PulseLevel( s * 1.2f + 0.618f, 0.35f ), Run( machine, io, s * 1000, 0, 1 )[ 0 ], Tolerance );
	}

	[Fact]
	public void FlickerMatchesTheEngine()
	{
		var (machine, io) = Element( PresetScripts.Flicker, Beat );
		foreach ( var s in Seconds() )
			Assert.Equal( FlickerLevel( s * 1.2f + 0.618f, 0.35f ), Run( machine, io, s * 1000, 0, 1 )[ 1 ], Tolerance );
	}

	[Fact]
	public void SpinMatchesTheEngine()
	{
		var (machine, io) = Element( PresetScripts.Spin, Beat );
		foreach ( var s in Seconds() )
			Assert.Equal( (s * 1.2f + 0.618f) % 1f * 360f, Run( machine, io, s * 1000, 0, 1 )[ (int)ScriptOutput.Rotation ], DegreesTolerance );
	}

	[Fact]
	public void JiggleMatchesTheEngine()
	{
		const float jiggleReach = 4f;
		var (machine, io) = Element( PresetScripts.Jiggle, Beat );
		foreach ( var s in Seconds() )
		{
			var (x, y, z) = Wobble( s * 1.2f + 0.618f );
			var outputs = Run( machine, io, s * 1000, 0, 1 );
			Assert.Equal( x * 0.35f * jiggleReach, outputs[ (int)ScriptOutput.OffsetX ], Tolerance );
			Assert.Equal( y * 0.35f * jiggleReach, outputs[ (int)ScriptOutput.OffsetY ], Tolerance );
			Assert.Equal( z * 0.35f * jiggleReach, outputs[ (int)ScriptOutput.OffsetZ ], Tolerance );
		}
	}

	[Fact]
	public void RainbowGoesRoundTheWheelOnceABeat()
	{
		var (machine, io) = Element( PresetScripts.RainbowCycle, Beat );
		foreach ( var s in Seconds() )
		{
			var hue = (s * 1.2f + 0.618f) % 1f * 360f;
			var (r, g, b) = Hsv( hue );
			var outputs = Run( machine, io, s * 1000, 0, 1 );
			Assert.Equal( r, outputs[ 0 ], Tolerance );
			Assert.Equal( g, outputs[ 1 ], Tolerance );
			Assert.Equal( b, outputs[ 2 ], Tolerance );
		}
	}

	// Full saturation and value round the wheel, as s&box's ColorHsv gives it
	private static (float, float, float) Hsv( float hue )
	{
		var sector = hue / 60f;
		var x = 1f - MathF.Abs( sector % 2f - 1f );
		return (int)sector switch
		{
			0 => (1f, x, 0f),
			1 => (x, 1f, 0f),
			2 => (0f, 1f, x),
			3 => (0f, x, 1f),
			4 => (x, 0f, 1f),
			_ => (1f, 0f, x),
		};
	}

	[Fact]
	public void EveryModeAndRhythmHasAScript()
	{
		foreach ( var mode in Enum.GetValues<LayerColorMode>() )
			ScriptKit.Compile( PresetScripts.ColorModeScript( mode ), ScriptKind.Sticker );

		foreach ( var rhythm in Enum.GetValues<LayerRhythm>() )
		{
			if ( PresetScripts.RhythmScript( rhythm ) is { } script )
				ScriptKit.Compile( script, ScriptKind.Sticker );
			else
				Assert.Equal( LayerRhythm.Steady, rhythm );
		}
	}
}
