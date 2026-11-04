using System;
using System.Collections.Generic;
using System.Globalization;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>
/// A moving sticker's fill, worked out by its motion's script (see <see cref="PresetScripts.StickerMotionScript"/>),
/// gives what the C# gave on weapons and tiles before the scripts took over: its formulas are copied below as the
/// reference, the stickers of a weapon counted as one row. One difference is meant: a scanner (Cyln) given more than two
/// colours sweeps its first over its second, as tiles and layers always did, where a weapon used to take its last.
/// </summary>
public class StickerMotionTests
{
	private static readonly int[] Counts = { 1, 3, 8 };
	private static readonly string[] Own = { "#112233", "#e04a8a", "#40c0ff" };
	private const long GridEnd = 6000;
	private const int GridStep = 37;
	private const float Close = 1e-4f;

	// What the weapon dresser worked out for sticker i of count, scaled milliseconds in (its own pace taken in)
	private static (float R, float G, float B) Reference( Sticker.Motion motion, IReadOnlyList<string> cycle, string fill, long scaled, int i, int count )
	{
		switch ( motion )
		{
			case Sticker.Motion.Rainbow:
				return Hsv( (scaled / 1000f * 0.5f + i / (float)count) % 1f * 360f );
			case Sticker.Motion.Pulse:
			{
				var own = Rgb( fill );
				var level = 1f - 0.5f * (0.5f - 0.5f * MathF.Cos( scaled / 1000f * MathF.Tau ));
				return (own.R * level, own.G * level, own.B * level);
			}
			case Sticker.Motion.Cyln:
				return Rgb( ColorCycle.IsScannerHighlight( scaled, i, count ) ? cycle[ 0 ] : cycle[ 1 ] );
			default:
			{
				var kind = motion == Sticker.Motion.Cycl ? ColorCycle.Kind.Cycl : ColorCycle.Kind.Rain;
				return Rgb( cycle[ ColorCycle.PaletteIndex( kind, scaled, i, count, cycle.Count ) ] );
			}
		}
	}

	private static (float R, float G, float B) Rgb( string hex )
	{
		var value = int.Parse( hex.AsSpan( 1 ), NumberStyles.HexNumber );
		return ((value >> 16 & 255) / 255f, (value >> 8 & 255) / 255f, (value & 255) / 255f);
	}

	// Full saturation and value, as ColorHsv gave
	private static (float R, float G, float B) Hsv( float hue )
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

	[Theory]
	[InlineData( Sticker.Motion.Rainbow, false )]
	[InlineData( Sticker.Motion.Pulse, false )]
	[InlineData( Sticker.Motion.Rain, false )]
	[InlineData( Sticker.Motion.Rain, true )]
	[InlineData( Sticker.Motion.Cycl, false )]
	[InlineData( Sticker.Motion.Cycl, true )]
	[InlineData( Sticker.Motion.Cyln, false )]
	[InlineData( Sticker.Motion.Cyln, true )]
	public void AStickersScriptGivesWhatTheCodedMotionGave( Sticker.Motion motion, bool ownPalette )
	{
		var sticker = new Sticker { Fill = Own[ 1 ], Animation = motion.ToString(), Palette = ownPalette ? new List<string>( Own ) : null };
		var cycle = sticker.CycleColours();
		var moving = MovingColour.OfSticker( motion, cycle, sticker.Fill, new ScriptCache(), new ScriptStack() );
		Assert.NotNull( moving );

		foreach ( var count in Counts )
		{
			for ( long ms = 0; ms <= GridEnd; ms += GridStep )
			{
				for ( var i = 0; i < count; i++ )
				{
					var expected = Reference( motion, cycle, sticker.Fill, ms, i, count );
					var made = moving.At( ms, i, count );
					Assert.True( MathF.Abs( expected.R - made.R ) < Close && MathF.Abs( expected.G - made.G ) < Close && MathF.Abs( expected.B - made.B ) < Close,
						$"{motion} at {ms} ms, {i} of {count}: {expected} vs {made}" );
				}
			}
		}
	}

	[Fact]
	public void AStillStickerHasNoMotion() => Assert.Null( MovingColour.OfSticker( Sticker.Motion.None, ColorCycle.GamePalette, "#ffffff", new ScriptCache(), new ScriptStack() ) );

	[Fact]
	public void ATilesCycleRunsItsLayersColourMode()
	{
		var moving = MovingColour.Of( PresetScripts.ColorModeScript( LayerColorMode.Cycl ), ColorCycle.GamePalette, new ScriptCache(), new ScriptStack() );
		for ( long ms = 0; ms <= GridEnd; ms += GridStep )
		{
			for ( var i = 0; i < 3; i++ )
				Assert.Equal( Rgb( ColorCycle.GamePalette[ ColorCycle.PaletteIndex( ColorCycle.Kind.Cycl, ms, i, 3, 5 ) ] ), moving.At( ms, i, 3 ) );
		}
	}

	[Fact]
	public void AMovingStickerIsColouredEachFrameWithoutAllocating()
	{
		var moving = MovingColour.OfSticker( Sticker.Motion.Cycl, ColorCycle.GamePalette, "#ffffff", new ScriptCache(), new ScriptStack() );
		moving.At( 0, 0, 8 );
		var before = GC.GetAllocatedBytesForCurrentThread();
		for ( var ms = 0; ms < 1000; ms++ )
			moving.At( ms * 16, ms % 8, 8 );

		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
	}
}
