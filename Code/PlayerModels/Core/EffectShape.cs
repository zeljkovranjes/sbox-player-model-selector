using System;
using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>
/// A picture made of lines rather than pixels: closed outlines that are filled and open lines drawn at a width, on a
/// square from -1 to 1 each way (y up), with how soft its edge is and how much it glows. Every client draws it the same
/// from those few numbers (see <see cref="Rasterize"/>), so the curated pictures sprites are made of (see
/// <see cref="Of"/>) need no image files, and shapes of any other kind can join them the same way.
/// </summary>
public sealed class EffectShape
{
	/// <summary>
	/// Closed outlines, each its points as x, y pairs one after another, filled in; where they overlap is filled once, with
	/// no edge between them (one outline crossing itself leaves out what it goes round twice).
	/// </summary>
	public List<float[]> Fills { get; init; } = new();

	/// <summary>Open lines, each its points as x, y pairs one after another, drawn <see cref="Width"/> wide.</summary>
	public List<float[]> Lines { get; init; } = new();

	/// <summary>How wide lines are drawn, on the square's scale (2 across).</summary>
	public float Width { get; init; } = 0.1f;

	/// <summary>How far its edge blurs, on the square's scale.</summary>
	public float Softness { get; init; } = 0.04f;

	/// <summary>How much light spills around it, 0 to 1, and how far out it reaches before it has faded to a third.</summary>
	public float Glow { get; init; }
	public float GlowReach { get; init; } = 0.3f;

	/// <summary>How much its fill is broken up into wisps, 0 (solid) to 1: smoke that doesn't look like discs.</summary>
	public float Haze { get; init; }

	// Everything fades to nothing before the square's edge: light stopping at the edge in a line shows as a square, and
	// an aura's many big glows added over each other under the game's exposure and bloom show every one. A glow fades out
	// from this far from the middle to none at the circle inside the square; the outermost pixels are clear whatever
	// the picture, so a sprite's edge never shows
	private const float GlowFadeFrom = 0.6f;
	private const int ClearBorder = 2;

	/// <summary>
	/// How much of each pixel of a <paramref name="size"/> by <paramref name="size"/> square it covers, 0 to 1, row by row
	/// from the top: inside it is covered, its edge blurs over <see cref="Softness"/>, and its glow fades out from there,
	/// to nothing before the square's edge.
	/// </summary>
	public float[] Rasterize( int size )
	{
		var coverage = new float[ size * size ];
		for ( var row = 0; row < size; row++ )
		{
			for ( var column = 0; column < size; column++ )
			{
				var x = (column + 0.5f) / size * 2f - 1f;
				var y = 1f - (row + 0.5f) / size * 2f;
				var border = Math.Min( Math.Min( column, size - 1 - column ), Math.Min( row, size - 1 - row ) );
				coverage[ row * size + column ] = CoverageAt( x, y ) * Math.Clamp( border / (float)ClearBorder, 0f, 1f );
			}
		}

		return coverage;
	}

	// How far the point is outside the shape (negative inside), then softened, lit by the glow and broken up by the haze.
	// Overlapping outlines join: the nearest any of them is, each measured from its own inside, so the edges of one
	// within another (smoke's puffs) leave no seam
	private float CoverageAt( float x, float y )
	{
		var distance = float.MaxValue;
		foreach ( var fill in Fills )
		{
			var edge = DistanceToOutline( fill, x, y, true );
			distance = MathF.Min( distance, Contains( fill, x, y ) ? -edge : edge );
		}

		foreach ( var line in Lines )
			distance = MathF.Min( distance, DistanceToOutline( line, x, y, false ) - Width * 0.5f );

		var solid = Math.Clamp( 0.5f - distance / MathF.Max( Softness, 0.001f ), 0f, 1f );
		if ( Haze > 0f )
			solid *= 1f - Haze + Haze * Noise( x * 3f + 11f, y * 3f + 5f );

		var glow = Glow * MathF.Exp( -MathF.Max( distance, 0f ) / MathF.Max( GlowReach, 0.001f ) ) * FadeOut( MathF.Sqrt( x * x + y * y ) );
		return Math.Clamp( MathF.Max( solid, glow ), 0f, 1f );
	}

	// 1 out to GlowFadeFrom from the middle, falling smoothly to none at 1
	private static float FadeOut( float radius )
	{
		var t = Math.Clamp( (radius - GlowFadeFrom) / (1f - GlowFadeFrom), 0f, 1f );
		return 1f - t * t * (3f - 2f * t);
	}

	private static float DistanceToOutline( float[] points, float x, float y, bool closed )
	{
		var nearest = float.MaxValue;
		var count = points.Length / 2;
		var segments = closed ? count : count - 1;
		for ( var i = 0; i < segments; i++ )
		{
			var j = (i + 1) % count;
			nearest = MathF.Min( nearest, DistanceToSegment( x, y, points[ i * 2 ], points[ i * 2 + 1 ], points[ j * 2 ], points[ j * 2 + 1 ] ) );
		}

		// A single point is a dot
		return count == 1 ? MathF.Sqrt( (x - points[ 0 ]) * (x - points[ 0 ]) + (y - points[ 1 ]) * (y - points[ 1 ]) ) : nearest;
	}

	private static float DistanceToSegment( float x, float y, float ax, float ay, float bx, float by )
	{
		var dx = bx - ax;
		var dy = by - ay;
		var length = dx * dx + dy * dy;
		var along = length > 0f ? Math.Clamp( ((x - ax) * dx + (y - ay) * dy) / length, 0f, 1f ) : 0f;
		var px = ax + along * dx - x;
		var py = ay + along * dy - y;
		return MathF.Sqrt( px * px + py * py );
	}

	// Even-odd: a ray to the right crosses the outline an odd number of times from inside
	internal static bool Contains( float[] points, float x, float y )
	{
		var inside = false;
		var count = points.Length / 2;
		for ( int i = 0, j = count - 1; i < count; j = i++ )
		{
			float xi = points[ i * 2 ], yi = points[ i * 2 + 1 ], xj = points[ j * 2 ], yj = points[ j * 2 + 1 ];
			if ( (yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi )
				inside = !inside;
		}

		return inside;
	}

	// Smooth value noise, 0 to 1
	private static float Noise( float x, float y )
	{
		var cellX = MathF.Floor( x );
		var cellY = MathF.Floor( y );
		var fx = x - cellX;
		var fy = y - cellY;
		fx = fx * fx * (3f - 2f * fx);
		fy = fy * fy * (3f - 2f * fy);

		var a = Hash( cellX, cellY );
		var b = Hash( cellX + 1f, cellY );
		var c = Hash( cellX, cellY + 1f );
		var d = Hash( cellX + 1f, cellY + 1f );
		return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
	}

	private static float Hash( float x, float y )
	{
		var h = MathF.Sin( x * 127.1f + y * 311.7f ) * 43758.5453f;
		return h - MathF.Floor( h );
	}

	/// <summary>The curated picture <paramref name="picture"/>, drawn from lines.</summary>
	public static EffectShape Of( LayerPicture picture ) => picture switch
	{
		LayerPicture.Spark => new EffectShape { Fills = { Circle( 0f, 0f, 0.14f ) }, Softness = 0.08f, Glow = 0.45f, GlowReach = 0.22f },
		LayerPicture.Flame => new EffectShape { Fills = { Teardrop( 0.45f, -0.35f, 1.25f ) }, Softness = 0.35f, Glow = 0.2f, GlowReach = 0.15f },
		LayerPicture.Smoke => new EffectShape
		{
			Fills = { Circle( 0f, 0.1f, 0.42f ), Circle( -0.32f, -0.12f, 0.34f ), Circle( 0.3f, -0.15f, 0.36f ), Circle( 0.05f, -0.3f, 0.3f ) },
			Softness = 0.45f, Haze = 0.5f,
		},
		LayerPicture.Sparkle => new EffectShape { Fills = { Star( 4, 0.95f, 0.12f, 0f ) }, Softness = 0.05f, Glow = 0.5f, GlowReach = 0.18f },
		LayerPicture.Heart => new EffectShape { Fills = { Heart( 0.85f ) }, Softness = 0.05f },
		LayerPicture.Snowflake => new EffectShape { Lines = Snowflake( 0.8f ), Width = 0.1f, Softness = 0.08f, Glow = 0.3f, GlowReach = 0.12f },
		LayerPicture.Droplet => new EffectShape { Fills = { Teardrop( 0.18f, -0.65f, 1.5f ) }, Softness = 0.08f, Glow = 0.15f, GlowReach = 0.1f },
		_ => new EffectShape { Fills = { Circle( 0f, 0f, 0.04f ) }, Softness = 0.05f, Glow = 1f, GlowReach = 0.33f },
	};

	// The number of points round a circle: enough that its edge never shows a corner at the sizes pictures are drawn
	internal const int CirclePoints = 48;

	internal static float[] Circle( float x, float y, float radius )
	{
		var points = new float[ CirclePoints * 2 ];
		for ( var i = 0; i < CirclePoints; i++ )
		{
			var angle = i * MathF.Tau / CirclePoints;
			points[ i * 2 ] = x + MathF.Cos( angle ) * radius;
			points[ i * 2 + 1 ] = y + MathF.Sin( angle ) * radius;
		}

		return points;
	}

	// A drop pointed at the top: round below, as wide as width with the middle of its round part at middle, and tall
	// enough above that to reach its point
	internal static float[] Teardrop( float width, float middle, float height )
	{
		var points = new float[ CirclePoints * 2 ];
		for ( var i = 0; i < CirclePoints; i++ )
		{
			var angle = i * MathF.Tau / CirclePoints - MathF.PI / 2f;
			var up = MathF.Sin( angle );
			points[ i * 2 ] = MathF.Cos( angle ) * width * (up > 0f ? MathF.Pow( 1f - up, 1.2f ) : 1f);
			points[ i * 2 + 1 ] = middle + up * (up > 0f ? height : width);
		}

		return points;
	}

	// Points round the middle, out to outer and in to inner between them, the first at the angle given (radians, from up)
	internal static float[] Star( int tips, float outer, float inner, float angle )
	{
		var points = new float[ tips * 4 ];
		for ( var i = 0; i < tips * 2; i++ )
		{
			var radius = i % 2 == 0 ? outer : inner;
			var at = angle + i * MathF.PI / tips + MathF.PI / 2f;
			points[ i * 2 ] = MathF.Cos( at ) * radius;
			points[ i * 2 + 1 ] = MathF.Sin( at ) * radius;
		}

		return points;
	}

	// The classic heart curve, scaled to reach size from the middle
	internal static float[] Heart( float size )
	{
		var points = new float[ CirclePoints * 2 ];
		for ( var i = 0; i < CirclePoints; i++ )
		{
			var t = i * MathF.Tau / CirclePoints;
			var sin = MathF.Sin( t );
			points[ i * 2 ] = 16f * sin * sin * sin / 17f * size;
			points[ i * 2 + 1 ] = (13f * MathF.Cos( t ) - 5f * MathF.Cos( 2f * t ) - 2f * MathF.Cos( 3f * t ) - MathF.Cos( 4f * t ) + 2.5f) / 17f * size;
		}

		return points;
	}

	// Six arms, each with a pair of short branches
	private static List<float[]> Snowflake( float reach )
	{
		var lines = new List<float[]>();
		for ( var arm = 0; arm < 6; arm++ )
		{
			var angle = arm * MathF.PI / 3f;
			var (dx, dy) = (MathF.Cos( angle ), MathF.Sin( angle ));
			lines.Add( new[] { 0f, 0f, dx * reach, dy * reach } );

			var branchAt = reach * 0.55f;
			var branch = reach * 0.3f;
			foreach ( var side in new[] { -1f, 1f } )
			{
				var turn = angle + side * MathF.PI / 4f;
				lines.Add( new[] { dx * branchAt, dy * branchAt, dx * branchAt + MathF.Cos( turn ) * branch, dy * branchAt + MathF.Sin( turn ) * branch } );
			}
		}

		return lines;
	}
}
