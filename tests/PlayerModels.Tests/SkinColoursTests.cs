using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;
using Xunit.Abstractions;

namespace PlayerModels.Tests;

/// <summary>
/// A weapon skin's moving colours, worked out by its scripts into the table the shader reads (see
/// <see cref="SkinColours"/>), give what the skin shader worked out itself before the scripts took over: its maths is
/// copied below as the reference, and both are put through the shader's shading (old: the motion's own formula; new:
/// shade, paint, glow and sheen from the table) for the same surfaces. Compared at the clan tags' whole milliseconds, a
/// band's own texel, or a continuous skin's texel centres (the shader blends between those). Two changes are meant: a
/// skin's glow is as bright as the weapon's own surface (see <see cref="GlowDetail"/>), and Glow breathes at 0.6 rather
/// than 1.5, so the gun's detail reads through it instead of a flat silhouette.
/// </summary>
public class SkinColoursTests
{
	private static readonly string[] Own = { "#112233", "#e04a8a", "#40c0ff" };
	private static readonly (float R, float G, float B)[] Surfaces = { (0.05f, 0.05f, 0.06f), (0.6f, 0.4f, 0.2f), (0.9f, 0.9f, 0.9f) };
	private const float Roughness = 0.6f;
	private const float Close = 2e-4f;
	private const long GridEnd = 6000;
	private const int GridStep = 37;

	// ----- The skin shader's maths before the scripts (weapon_skin.shader at 05d0249), in float like the GPU -----

	private static float Mod( float a, float b ) => a - b * MathF.Floor( a / b );
	private static float Frac( float x ) => x - MathF.Floor( x );
	private static float Saturate( float x ) => Math.Clamp( x, 0f, 1f );
	private static float SmoothStep( float a, float b, float x ) { var t = Saturate( (x - a) / (b - a) ); return t * t * (3f - 2f * t); }
	private static (float, float, float) Lerp( (float R, float G, float B) a, (float R, float G, float B) b, float t ) => (a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
	private static (float, float, float) Mul( (float R, float G, float B) a, float k ) => (a.R * k, a.G * k, a.B * k);
	private static (float, float, float) Mul( (float R, float G, float B) a, (float R, float G, float B) b ) => (a.R * b.R, a.G * b.G, a.B * b.B);

	private static (float, float, float) Hue( float hue ) =>
		(Saturate( MathF.Abs( Frac( hue ) * 6f - 3f ) - 1f ), Saturate( MathF.Abs( Frac( hue + 2f / 3f ) * 6f - 3f ) - 1f ), Saturate( MathF.Abs( Frac( hue + 1f / 3f ) * 6f - 3f ) - 1f ));

	private static (float, float, float) PaletteAt( IReadOnlyList<(float, float, float)> palette, float index ) => palette[ (int)Mod( index, palette.Count ) ];

	private static (float, float, float) BandColour( WeaponSkin.Motion motion, float band, float bands, float ms, float along, IReadOnlyList<(float, float, float)> palette )
	{
		float count = palette.Count;
		switch ( motion )
		{
			case WeaponSkin.Motion.Rainbow: return Hue( Frac( along - ms * 0.00025f ) );
			case WeaponSkin.Motion.Rain: return PaletteAt( palette, Mod( MathF.Floor( ms / 100f ), count ) + band );
			case WeaponSkin.Motion.Cycl:
			{
				var basis = Mod( MathF.Floor( ms / (bands * 100f) ), count );
				var position = Mod( MathF.Floor( ms / 100f ), bands );
				return PaletteAt( palette, band < position ? basis : basis + count - 1f );
			}
			case WeaponSkin.Motion.Cyln:
			{
				var position = MathF.Floor( (MathF.Cos( ms * 0.004f ) + 1f) * 0.5f * bands );
				return band == position ? palette[ 0 ] : palette[ 1 ];
			}
			default:
			{
				var step = ms / 1000f;
				return Lerp( PaletteAt( palette, MathF.Floor( step ) ), PaletteAt( palette, MathF.Floor( step ) + 1f ), SmoothStep( 0f, 1f, Frac( step ) ) );
			}
		}
	}

	// The old shading of one point: the surface after a moving colour, its emission and roughness
	private static ((float, float, float) Albedo, (float, float, float) Emission, float Rough) Old( WeaponSkin.Motion motion, (float R, float G, float B) albedo, float along,
		float seconds, float ms, float bands, IReadOnlyList<(float, float, float)> palette )
	{
		var emission = (0f, 0f, 0f);
		var rough = Roughness;
		var p0 = palette[ 0 ];
		var surface = albedo;
		switch ( motion )
		{
			case WeaponSkin.Motion.Rainbow or WeaponSkin.Motion.Rain or WeaponSkin.Motion.Cycl or WeaponSkin.Motion.Cyln or WeaponSkin.Motion.Palette:
			{
				var band = MathF.Min( MathF.Floor( along * bands ), bands - 1f );
				var moving = BandColour( motion, band, bands, ms, along, palette );
				var light = MathF.Max( albedo.R * 0.3f + albedo.G * 0.59f + albedo.B * 0.11f, 0.35f );
				albedo = Lerp( albedo, Mul( moving, Saturate( light * 1.8f ) ), 0.85f );
				emission = Mul( moving, 0.12f );
				break;
			}
			case WeaponSkin.Motion.Pulse:
			{
				var beat = 0.5f - 0.5f * MathF.Cos( seconds * 6.28318f );
				albedo = Lerp( albedo, Mul( Mul( albedo, p0 ), 1.4f ), beat );
				break;
			}
			case WeaponSkin.Motion.Glow:
				emission = Mul( p0, (0.6f + 0.4f * MathF.Sin( seconds * 6.28318f )) * GlowStrength );
				break;
			case WeaponSkin.Motion.Shimmer:
			{
				var sweep = Frac( seconds * 0.5f ) * 1.6f - 0.3f;
				var offset = (along - sweep) * 16f;
				var sheen = MathF.Exp( -offset * offset );
				emission = Mul( p0, sheen * 1.2f );
				rough = Roughness + (0.1f - Roughness) * sheen;
				break;
			}
		}

		return (albedo, Mul( emission, GlowDetail( surface ) ), rough);
	}

	// Meant changes from the old shader: Glow's strength, and glow following the surface's own brightness (the shader's
	// GlowFloor and GlowDetail)
	private const float GlowStrength = 0.6f;

	private static float GlowDetail( (float R, float G, float B) surface ) =>
		0.35f + 0.65f * Saturate( (surface.R * 0.3f + surface.G * 0.59f + surface.B * 0.11f) * 3f );

	// The new shading of one point, from the table as the shader reads it (a band's texel, else the one at along)
	private static ((float, float, float) Albedo, (float, float, float) Emission, float Rough) New( SkinColours table, (float R, float G, float B) albedo, int texel )
	{
		var paint = table.At( SkinColours.PaintRow, texel );
		var glow = table.At( SkinColours.GlowRow, texel );
		var shade = table.At( SkinColours.ShadeRow, texel );
		var light = MathF.Max( albedo.R * 0.3f + albedo.G * 0.59f + albedo.B * 0.11f, 0.35f );
		var shaded = Mul( albedo, (shade.X, shade.Y, shade.Z) );
		var result = Lerp( shaded, Mul( (paint.X, paint.Y, paint.Z), Saturate( light * 1.8f ) ), paint.W );
		var detail = 0.35f + 0.65f * Saturate( (albedo.R * 0.3f + albedo.G * 0.59f + albedo.B * 0.11f) * 3f );
		return (result, Mul( (glow.X, glow.Y, glow.Z), detail ), Roughness + (0.1f - Roughness) * glow.W);
	}

	private static (float, float, float) Linear( string hex )
	{
		var value = int.Parse( hex.AsSpan( 1 ), NumberStyles.HexNumber );
		static float Channel( int c ) => (float)ScriptMachine.Linear( c / 255.0 );
		return (Channel( value >> 16 & 255 ), Channel( value >> 8 & 255 ), Channel( value & 255 ));
	}

	public static IEnumerable<object[]> Skins()
	{
		foreach ( var motion in new[] { WeaponSkin.Motion.Rainbow, WeaponSkin.Motion.Rain, WeaponSkin.Motion.Cycl, WeaponSkin.Motion.Cyln, WeaponSkin.Motion.Pulse,
			WeaponSkin.Motion.Glow, WeaponSkin.Motion.Shimmer, WeaponSkin.Motion.Palette } )
		{
			yield return new object[] { motion, false, 1f, 8 };
			yield return new object[] { motion, true, 2.5f, 5 };
		}
	}

	[Theory]
	[MemberData( nameof( Skins ) )]
	public void ASkinsScriptsShadeItAsTheShaderDid( WeaponSkin.Motion motion, bool ownPalette, float speed, int bands )
	{
		var skin = new WeaponSkin { Animation = motion.ToString(), Palette = ownPalette ? new List<string>( Own ) : null, Speed = speed, Bands = bands }.Sanitized();
		var palette = new List<(float, float, float)>();
		foreach ( var hex in skin.Colours() )
			palette.Add( Linear( hex ) );

		var table = SkinColours.Of( skin, new ScriptCache(), new ScriptStack() );
		Assert.NotNull( table );
		Assert.Equal( motion is not (WeaponSkin.Motion.Rainbow or WeaponSkin.Motion.Shimmer), table.Banded );

		var checks = 0;
		for ( long scaled = 0; scaled <= GridEnd; scaled += GridStep )
		{
			// A time whose skin-paced milliseconds are whole, as the clan tags count them
			var seconds = scaled / 1000.0 / speed;
			table.Fill( seconds );
			var ms = (long)(seconds * 1000 * speed);
			var places = table.Banded ? bands : SkinColours.Texels;
			for ( var texel = 0; texel < places; texel++ )
			{
				var along = (texel + 0.5f) / places;
				foreach ( var surface in Surfaces )
				{
					var old = Old( motion, surface, along, ms / 1000f, ms, bands, palette );
					var made = New( table, surface, texel );
					Same( old.Albedo, made.Albedo, $"{motion} albedo at {ms} ms, texel {texel} of {places}" );
					Same( old.Emission, made.Emission, $"{motion} emission at {ms} ms, texel {texel} of {places}" );
					Assert.True( MathF.Abs( old.Rough - made.Rough ) < Close, $"{motion} roughness at {ms} ms, texel {texel}: {old.Rough} vs {made.Rough}" );
					checks++;
				}
			}
		}

		Assert.True( checks > 1000 );
	}

	private static void Same( (float R, float G, float B) a, (float R, float G, float B) b, string where ) =>
		Assert.True( MathF.Abs( a.R - b.R ) < Close && MathF.Abs( a.G - b.G ) < Close && MathF.Abs( a.B - b.B ) < Close, $"{where}: {a} vs {b}" );

	[Theory]
	[InlineData( 0.5f, 0f )]
	[InlineData( -1.25f, 0.4f )]
	[InlineData( 0f, -2f )]
	public void APatternScrollsAndSpinsAsTheShaderDid( float scroll, float spin )
	{
		var skin = new WeaponSkin { PatternName = nameof( WeaponSkin.Pattern.Stripes ), PatternScroll = scroll, PatternSpin = spin }.Sanitized();
		var table = SkinColours.Of( skin, new ScriptCache(), new ScriptStack() );
		Assert.NotNull( table );
		for ( var seconds = 0.0; seconds < 20; seconds += 0.37 )
		{
			table.Fill( seconds );
			var travel = table.At( SkinColours.PatternRow, 3 );
			Assert.True( MathF.Abs( travel.X - (float)seconds * scroll ) < Close && MathF.Abs( travel.Y - (float)seconds * spin ) < Close, $"{seconds} s: {travel}" );

			// Nothing painted, its own colour kept
			Assert.Equal( (1f, 1f, 1f, 0f), table.At( SkinColours.PaintRow, 3 ) );
			Assert.Equal( (1f, 1f, 1f, 1f), table.At( SkinColours.ShadeRow, 3 ) );
		}
	}

	[Fact]
	public void AStillSkinHasNoTable()
	{
		Assert.Null( SkinColours.Of( new WeaponSkin { PatternName = nameof( WeaponSkin.Pattern.Camo ) }.Sanitized(), new ScriptCache(), new ScriptStack() ) );
		Assert.Equal( "", SkinColours.KeyOf( null ) );
	}

	[Fact]
	public void SkinsAlikeShareATableKey()
	{
		var a = new WeaponSkin { Animation = "Rain", Speed = 2f, PatternName = "Checker", PatternColor = "#ff0000" }.Sanitized();
		var b = new WeaponSkin { Animation = "Rain", Speed = 2f, PatternName = "Checker", PatternColor = "#00ff00" }.Sanitized();
		var c = new WeaponSkin { Animation = "Rain", Speed = 3f }.Sanitized();
		Assert.Equal( SkinColours.KeyOf( a ), SkinColours.KeyOf( b ) );
		Assert.NotEqual( SkinColours.KeyOf( a ), SkinColours.KeyOf( c ) );
	}

	[Fact]
	public void EveryStockSkinScriptCompiles()
	{
		foreach ( var motion in Enum.GetValues<WeaponSkin.Motion>() )
		{
			if ( PresetScripts.SkinMotionScript( motion ) is { } script )
				ScriptKit.Compile( script, ScriptKind.Skin );
		}

		ScriptKit.Compile( PresetScripts.SkinPatternMotion, ScriptKind.Skin );
	}
}

/// <summary>Filling a skin's table each frame allocates nothing, and what it costs for a hundred players' skins.</summary>
[Collection( Measurements.Name )]
public class SkinColoursCostTests
{
	private readonly ITestOutputHelper _output;

	public SkinColoursCostTests( ITestOutputHelper output ) => _output = output;

	[Fact]
	public void FillingAllocatesNothingAndIsCheap()
	{
		var cache = new ScriptCache();
		var stack = new ScriptStack();
		var tables = new List<SkinColours>();
		var motions = new[] { "Rainbow", "Rain", "Cycl", "Cyln", "Pulse", "Glow", "Shimmer", "Palette" };
		for ( var i = 0; i < 100; i++ )
		{
			// A hundred different skins: every motion, different speeds and bands, half with a moving pattern
			var skin = new WeaponSkin { Animation = motions[ i % motions.Length ], Speed = 0.5f + i * 0.05f, Bands = 2 + i % 30,
				PatternName = i % 2 == 0 ? "Stripes" : null, PatternScroll = i % 2 == 0 ? 0.5f : 0f }.Sanitized();
			tables.Add( SkinColours.Of( skin, cache, stack ) );
		}

		foreach ( var table in tables )
			table.Fill( 0.5 );

		GC.Collect();
		const int Frames = 200;
		var clock = new Stopwatch();
		var before = GC.GetAllocatedBytesForCurrentThread();
		clock.Start();
		for ( var frame = 0; frame < Frames; frame++ )
		{
			foreach ( var table in tables )
				table.Fill( frame / 60.0 );
		}

		clock.Stop();
		Assert.Equal( 0, GC.GetAllocatedBytesForCurrentThread() - before );
		var perFrame = clock.Elapsed.TotalMilliseconds / Frames;
		_output.WriteLine( $"100 different skins filled in {perFrame:0.000} ms a frame ({perFrame * 10:0.0} µs a skin); one skin shared by 100 players fills once: {perFrame / 100:0.000} ms" );
		Assert.True( perFrame < 4, $"{perFrame} ms a frame" );
	}
}
