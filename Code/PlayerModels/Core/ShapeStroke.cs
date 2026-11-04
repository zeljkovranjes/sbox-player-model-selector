using System;
using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>
/// One line of a shape a player made (see <see cref="DrawnShape"/>): its points as x, y pairs one after another, on the
/// square from -1 to 1 each way (y up) that shapes are drawn on, and whether it closes round on itself (filled in) or is
/// an open line drawn at a width. The shape creator's tools (mirror, scale, turn, fit, freehand tidying) work on these.
/// </summary>
public sealed class ShapeStroke
{
	/// <summary>Whether it closes round on itself and is filled in, rather than a line drawn at a width.</summary>
	public bool Closed { get; init; }

	/// <summary>Its points, as x, y pairs one after another.</summary>
	public float[] Points { get; init; } = Array.Empty<float>();

	/// <summary>How many points it has.</summary>
	public int Count => Points.Length / 2;

	// How far from the square's edge a fitted shape stays, so its outline and glow aren't cut off
	private const float FitReach = 0.9f;

	// Freehand: how far apart points must be to count, how far off the line a point may be before it is kept (on the
	// square's scale, 2 across), and how many times corners are rounded
	private const float LeastStep = 0.004f;
	private const float Tolerance = 0.006f;
	private const int Smoothing = 2;

	/// <summary>A copy with every point moved by <paramref name="map"/>.</summary>
	public ShapeStroke Mapped( Func<float, float, (float X, float Y)> map )
	{
		var points = new float[ Points.Length ];
		for ( var i = 0; i + 1 < Points.Length; i += 2 )
			(points[ i ], points[ i + 1 ]) = map( Points[ i ], Points[ i + 1 ] );

		return new ShapeStroke { Closed = Closed, Points = points };
	}

	/// <summary><paramref name="strokes"/> flipped left to right.</summary>
	public static List<ShapeStroke> Mirrored( IEnumerable<ShapeStroke> strokes ) => Map( strokes, ( x, y ) => (-x, y) );

	/// <summary><paramref name="strokes"/> flipped top to bottom.</summary>
	public static List<ShapeStroke> Flipped( IEnumerable<ShapeStroke> strokes ) => Map( strokes, ( x, y ) => (x, -y) );

	/// <summary><paramref name="strokes"/> grown (or shrunk, below 1) by <paramref name="factor"/> about the middle.</summary>
	public static List<ShapeStroke> Scaled( IEnumerable<ShapeStroke> strokes, float factor ) => Map( strokes, ( x, y ) => (x * factor, y * factor) );

	/// <summary><paramref name="strokes"/> turned about the middle by <paramref name="degrees"/>, anticlockwise.</summary>
	public static List<ShapeStroke> Rotated( IEnumerable<ShapeStroke> strokes, float degrees )
	{
		var (sin, cos) = MathF.SinCos( degrees * MathF.PI / 180f );
		return Map( strokes, ( x, y ) => (x * cos - y * sin, x * sin + y * cos) );
	}

	/// <summary>
	/// <paramref name="strokes"/> moved to the middle of the square and sized to fill it, keeping their proportions; as
	/// they are when they have no size at all (a single dot).
	/// </summary>
	public static List<ShapeStroke> Fitted( IReadOnlyList<ShapeStroke> strokes )
	{
		float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
		foreach ( var stroke in strokes )
		{
			for ( var i = 0; i + 1 < stroke.Points.Length; i += 2 )
			{
				minX = MathF.Min( minX, stroke.Points[ i ] );
				maxX = MathF.Max( maxX, stroke.Points[ i ] );
				minY = MathF.Min( minY, stroke.Points[ i + 1 ] );
				maxY = MathF.Max( maxY, stroke.Points[ i + 1 ] );
			}
		}

		var extent = MathF.Max( maxX - minX, maxY - minY );
		if ( !(extent > 0f) )
			return new List<ShapeStroke>( strokes );

		var (middleX, middleY, scale) = ((minX + maxX) / 2f, (minY + maxY) / 2f, 2f * FitReach / extent);
		return Map( strokes, ( x, y ) => ((x - middleX) * scale, (y - middleY) * scale) );
	}

	/// <summary>
	/// A line drawn by hand as the mouse gave it (<paramref name="raw"/>, x, y pairs), tidied: points too close together
	/// dropped, corners rounded off, then only the points needed to keep its shape kept (Ramer-Douglas-Peucker), so a
	/// stroke of hundreds of mouse positions keeps a few dozen. <paramref name="closed"/> makes it close round on itself.
	/// </summary>
	public static ShapeStroke Freehand( float[] raw, bool closed )
	{
		var points = new List<float>();
		for ( var i = 0; i + 1 < raw.Length; i += 2 )
		{
			if ( !float.IsFinite( raw[ i ] ) || !float.IsFinite( raw[ i + 1 ] ) )
				continue;

			if ( points.Count >= 2 && Distance( points[ ^2 ], points[ ^1 ], raw[ i ], raw[ i + 1 ] ) < LeastStep )
				continue;

			points.Add( Math.Clamp( raw[ i ], -1f, 1f ) );
			points.Add( Math.Clamp( raw[ i + 1 ], -1f, 1f ) );
		}

		var smooth = points.ToArray();
		for ( var pass = 0; pass < Smoothing; pass++ )
			smooth = Rounded( smooth, closed );

		return new ShapeStroke { Closed = closed && smooth.Length >= 6, Points = Simplified( smooth, Tolerance ) };
	}

	// Chaikin's corner cutting: each segment's quarter and three-quarter points, the ends of an open line kept
	private static float[] Rounded( float[] points, bool closed )
	{
		var count = points.Length / 2;
		if ( count < 3 )
			return points;

		var rounded = new List<float>( points.Length * 2 );
		var segments = closed ? count : count - 1;
		if ( !closed )
		{
			rounded.Add( points[ 0 ] );
			rounded.Add( points[ 1 ] );
		}

		for ( var i = 0; i < segments; i++ )
		{
			var j = (i + 1) % count;
			float ax = points[ i * 2 ], ay = points[ i * 2 + 1 ], bx = points[ j * 2 ], by = points[ j * 2 + 1 ];
			rounded.Add( ax * 0.75f + bx * 0.25f );
			rounded.Add( ay * 0.75f + by * 0.25f );
			rounded.Add( ax * 0.25f + bx * 0.75f );
			rounded.Add( ay * 0.25f + by * 0.75f );
		}

		if ( !closed )
		{
			rounded.Add( points[ ^2 ] );
			rounded.Add( points[ ^1 ] );
		}

		return rounded.ToArray();
	}

	// Ramer-Douglas-Peucker: the ends kept, and between them the point furthest off the line through them when it is
	// further than tolerance, then each half the same way
	private static float[] Simplified( float[] points, float tolerance )
	{
		var count = points.Length / 2;
		if ( count < 3 )
			return points;

		var keep = new bool[ count ];
		keep[ 0 ] = keep[ count - 1 ] = true;
		var spans = new Stack<(int From, int To)>();
		spans.Push( (0, count - 1) );
		while ( spans.Count > 0 )
		{
			var (from, to) = spans.Pop();
			var (furthest, at) = (0f, -1);
			for ( var i = from + 1; i < to; i++ )
			{
				var off = DistanceToLine( points, i, from, to );
				if ( off > furthest )
					(furthest, at) = (off, i);
			}

			if ( at < 0 || furthest <= tolerance )
				continue;

			keep[ at ] = true;
			spans.Push( (from, at) );
			spans.Push( (at, to) );
		}

		var kept = new List<float>();
		for ( var i = 0; i < count; i++ )
		{
			if ( !keep[ i ] )
				continue;

			kept.Add( points[ i * 2 ] );
			kept.Add( points[ i * 2 + 1 ] );
		}

		return kept.ToArray();
	}

	private static float DistanceToLine( float[] points, int i, int from, int to )
	{
		float x = points[ i * 2 ], y = points[ i * 2 + 1 ];
		float ax = points[ from * 2 ], ay = points[ from * 2 + 1 ], bx = points[ to * 2 ], by = points[ to * 2 + 1 ];
		var (dx, dy) = (bx - ax, by - ay);
		var length = dx * dx + dy * dy;
		var along = length > 0f ? Math.Clamp( ((x - ax) * dx + (y - ay) * dy) / length, 0f, 1f ) : 0f;
		return Distance( ax + along * dx, ay + along * dy, x, y );
	}

	private static float Distance( float ax, float ay, float bx, float by ) => MathF.Sqrt( (bx - ax) * (bx - ax) + (by - ay) * (by - ay) );

	private static List<ShapeStroke> Map( IEnumerable<ShapeStroke> strokes, Func<float, float, (float, float)> map )
	{
		var mapped = new List<ShapeStroke>();
		foreach ( var stroke in strokes )
			mapped.Add( stroke.Mapped( map ) );

		return mapped;
	}
}
