using System;
using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>
/// The standard shapes an effect can take: flat ones (round, polygons, stars and crosses, symbols, lines and curves) and
/// solids. Saved by name, like an effect's other choices.
/// </summary>
public enum StandardShape
{
	Circle, Ellipse, Semicircle, Arc, Ring, Crescent,
	Triangle, RightTriangle, Square, Rectangle, RoundedRectangle, Diamond, Parallelogram, Trapezoid, Kite, Pentagon, Hexagon, Octagon,
	Star4, Star5, Star6, Star8, Plus, Cross,
	Heart, Teardrop,
	Line, SineWave, Zigzag, Spiral, Infinity, Helix,
	Sphere, Hemisphere, Cube, Cylinder, Cone, Pyramid, Torus, Capsule,
}

/// <summary>
/// What the standard shapes (see <see cref="StandardShape"/>) are made of, in code rather than pictures, on a square
/// from -1 to 1 each way (y up), or a cube for the solids. Each can be a formation sprites come from (along its outline
/// or all over it; over a solid's surface or all through it, see <see cref="Formation"/>) and what a sprite looks like
/// (see <see cref="Picture"/>, a solid by its silhouette). Every client works out the same points and pictures from the
/// same few numbers, so nothing is downloaded, and they are worked out when a look changes, never each frame.
/// </summary>
public static class StandardShapes
{
	/// <summary>How many points a formation has: enough that its outline reads at any size an effect is.</summary>
	public const int FormationPoints = 96;

	// How thick a solid's surface is when points are spread over it, on the cube's scale
	private const float Shell = 0.035f;

	// How many places are tried for each point inside a shape or on a solid's surface before giving up on it
	private const int TriesPerPoint = 600;

	// How lines are drawn when the shape is what a sprite looks like
	private const float LineWidth = 0.14f;
	private const float Softness = 0.05f;
	private const float Glow = 0.25f;
	private const float GlowReach = 0.12f;

	/// <summary>Whether <paramref name="shape"/> is a solid: its formation is in three dimensions.</summary>
	public static bool IsSolid( StandardShape shape ) => shape >= StandardShape.Sphere;

	/// <summary>Whether <paramref name="shape"/> is a line with no inside to fill: its formation is always along it.</summary>
	public static bool IsOpen( StandardShape shape ) => shape is StandardShape.Arc or StandardShape.Line or StandardShape.SineWave
		or StandardShape.Zigzag or StandardShape.Spiral or StandardShape.Helix;

	/// <summary>
	/// <paramref name="shape"/> as a picture for sprites: its outlines filled and its lines drawn, a solid by its
	/// silhouette.
	/// </summary>
	public static EffectShape Picture( StandardShape shape )
	{
		// A shape with a hole is drawn as a thick line round it: a picture's outlines join, they don't cut each other
		if ( shape is StandardShape.Ring or StandardShape.Torus )
		{
			var (width, height, thickness) = shape == StandardShape.Ring ? (0.735f, 0.735f, 0.37f) : (0.685f, 0.355f, 0.42f);
			var round = Ellipse( 0f, 0f, width, height );
			return new EffectShape { Lines = { Joined( round, new[] { round[ 0 ], round[ 1 ] } ) }, Width = thickness, Softness = Softness, Glow = Glow, GlowReach = GlowReach };
		}

		var (closed, open) = Outline( shape );
		return new EffectShape { Fills = closed, Lines = open, Width = LineWidth, Softness = Softness, Glow = Glow, GlowReach = GlowReach };
	}

	/// <summary>
	/// <paramref name="count"/> points of <paramref name="shape"/> to place sprites at, as x, y, z one after another in a
	/// cube from -1 to 1 (x forwards, y to the left, z up): a flat shape stands upright facing forwards, read from the
	/// front. Along its outline, or with <paramref name="filled"/> all over its inside; a solid over its surface, or with
	/// <paramref name="filled"/> all through it. Spread evenly, and the same on every client.
	/// </summary>
	public static float[] Formation( StandardShape shape, bool filled, int count = FormationPoints )
	{
		count = Math.Max( 1, count );
		if ( shape == StandardShape.Helix )
			return HelixPoints( count );

		if ( IsSolid( shape ) )
			return SolidPoints( shape, filled, count );

		var (closed, open) = Outline( shape );
		return FormationOf( closed, open, filled, count );
	}

	/// <summary>
	/// <paramref name="count"/> points of a flat shape made of <paramref name="closed"/> outlines and <paramref name="open"/>
	/// lines (each x, y pairs on the square), standing upright facing forwards as <see cref="Formation"/>'s are: along them,
	/// or with <paramref name="filled"/> all over the closed outlines' inside. What a drawn shape (see
	/// <see cref="DrawnShape"/>) sends sprites out from.
	/// </summary>
	public static float[] FormationOf( List<float[]> closed, List<float[]> open, bool filled, int count = FormationPoints )
	{
		count = Math.Max( 1, count );
		var flat = filled && closed.Count > 0 ? Inside( closed, count ) : Along( closed, open, count );

		// Upright, facing forwards: the shape's right is the body's left, as someone in front sees it
		var points = new float[ count * 3 ];
		for ( var i = 0; i < count; i++ )
		{
			points[ i * 3 + 1 ] = flat[ i * 2 ];
			points[ i * 3 + 2 ] = flat[ i * 2 + 1 ];
		}

		return points;
	}

	/// <summary>
	/// The outlines of <paramref name="shape"/>, closed ones (filled, even-odd where they overlap) and open lines, each its
	/// points as x, y pairs one after another; a solid's silhouette from the front and a little above.
	/// </summary>
	public static (List<float[]> Closed, List<float[]> Open) Outline( StandardShape shape )
	{
		var closed = new List<float[]>();
		var open = new List<float[]>();
		switch ( shape )
		{
			case StandardShape.Circle: closed.Add( EffectShape.Circle( 0f, 0f, 0.9f ) ); break;
			case StandardShape.Ellipse: closed.Add( Ellipse( 0f, 0f, 0.95f, 0.6f ) ); break;
			case StandardShape.Semicircle or StandardShape.Hemisphere: closed.Add( ArcPoints( 0f, -0.45f, 0.95f, 0f, MathF.PI ) ); break;
			case StandardShape.Arc: open.Add( ArcPoints( 0f, -0.35f, 0.9f, Degrees( 20f ), Degrees( 160f ) ) ); break;
			case StandardShape.Ring: closed.Add( EffectShape.Circle( 0f, 0f, 0.92f ) ); closed.Add( EffectShape.Circle( 0f, 0f, 0.55f ) ); break;
			case StandardShape.Crescent: closed.Add( Crescent( 0.9f, 0.4f, 0.8f ) ); break;
			case StandardShape.Triangle: closed.Add( Regular( 3, 1f, 0f ) ); break;
			case StandardShape.RightTriangle: closed.Add( new[] { -0.8f, -0.8f, 0.8f, -0.8f, -0.8f, 0.8f } ); break;
			case StandardShape.Square: closed.Add( Box( 0.8f, 0.8f ) ); break;
			case StandardShape.Rectangle: closed.Add( Box( 0.95f, 0.55f ) ); break;
			case StandardShape.RoundedRectangle: closed.Add( RoundedBox( 0.92f, 0.62f, 0.28f ) ); break;
			case StandardShape.Diamond: closed.Add( new[] { 0f, 0.95f, 0.62f, 0f, 0f, -0.95f, -0.62f, 0f } ); break;
			case StandardShape.Parallelogram: closed.Add( new[] { -0.95f, -0.5f, 0.45f, -0.5f, 0.95f, 0.5f, -0.45f, 0.5f } ); break;
			case StandardShape.Trapezoid: closed.Add( new[] { -0.95f, -0.55f, 0.95f, -0.55f, 0.5f, 0.55f, -0.5f, 0.55f } ); break;
			case StandardShape.Kite: closed.Add( new[] { 0f, 0.95f, 0.62f, 0.35f, 0f, -0.95f, -0.62f, 0.35f } ); break;
			case StandardShape.Pentagon: closed.Add( Regular( 5, 0.95f, 0f ) ); break;
			case StandardShape.Hexagon or StandardShape.Cube: closed.Add( Regular( 6, 0.95f, 0f ) ); break;
			case StandardShape.Octagon: closed.Add( Regular( 8, 0.95f, MathF.PI / 8f ) ); break;
			case StandardShape.Star4: closed.Add( EffectShape.Star( 4, 0.95f, 0.32f, 0f ) ); break;
			case StandardShape.Star5: closed.Add( EffectShape.Star( 5, 0.95f, 0.4f, 0f ) ); break;
			case StandardShape.Star6: closed.Add( EffectShape.Star( 6, 0.95f, 0.5f, 0f ) ); break;
			case StandardShape.Star8: closed.Add( EffectShape.Star( 8, 0.95f, 0.55f, 0f ) ); break;
			case StandardShape.Plus: closed.Add( Plus( 0.9f, 0.28f, 0f ) ); break;
			case StandardShape.Cross: closed.Add( Plus( 0.95f, 0.26f, MathF.PI / 4f ) ); break;
			case StandardShape.Heart: closed.Add( Shifted( EffectShape.Heart( 0.95f ), 0f, 0.18f ) ); break;
			case StandardShape.Teardrop: closed.Add( EffectShape.Teardrop( 0.5f, -0.4f, 1.3f ) ); break;
			case StandardShape.Line: open.Add( new[] { -0.9f, 0f, 0.9f, 0f } ); break;
			case StandardShape.SineWave: open.Add( Wave() ); break;
			case StandardShape.Zigzag: open.Add( Zigzag() ); break;
			case StandardShape.Spiral: open.Add( Spiral() ); break;
			case StandardShape.Infinity: closed.Add( Lemniscate( 0.95f ) ); break;
			case StandardShape.Helix: open.Add( HelixFromFront() ); break;
			case StandardShape.Sphere: closed.Add( EffectShape.Circle( 0f, 0f, 0.9f ) ); break;
			case StandardShape.Cylinder: closed.Add( Cylinder() ); break;
			case StandardShape.Cone: closed.Add( Cone() ); break;
			case StandardShape.Pyramid: closed.Add( new[] { 0f, 0.92f, 0.9f, -0.5f, 0.1f, -0.9f, -0.9f, -0.55f } ); break;
			case StandardShape.Torus: closed.Add( Ellipse( 0f, 0f, 0.95f, 0.55f ) ); closed.Add( Ellipse( 0f, 0.05f, 0.42f, 0.16f ) ); break;
			case StandardShape.Capsule: closed.Add( Stadium( 0.42f, 0.5f ) ); break;
		}

		return (closed, open);
	}

	/// <summary>
	/// How far a point is outside <paramref name="shape"/>, a solid (negative inside): what its formation is spread over
	/// or through. Not exact for every solid, but close enough near its surface to spread points evenly.
	/// </summary>
	public static float DistanceOutside( StandardShape shape, float x, float y, float z )
	{
		switch ( shape )
		{
			case StandardShape.Sphere:
				return Length( x, y, z ) - 0.95f;

			case StandardShape.Hemisphere:
			{
				var lifted = z + 0.45f;
				return MathF.Max( Length( x, y, lifted ) - 0.95f, -lifted );
			}

			case StandardShape.Cube:
			{
				const float half = 0.75f;
				float qx = MathF.Abs( x ) - half, qy = MathF.Abs( y ) - half, qz = MathF.Abs( z ) - half;
				return Length( MathF.Max( qx, 0f ), MathF.Max( qy, 0f ), MathF.Max( qz, 0f ) ) + MathF.Min( MathF.Max( qx, MathF.Max( qy, qz ) ), 0f );
			}

			case StandardShape.Cylinder:
			{
				float dx = Length( x, y, 0f ) - 0.7f, dz = MathF.Abs( z ) - 0.9f;
				return MathF.Min( MathF.Max( dx, dz ), 0f ) + Length( MathF.Max( dx, 0f ), MathF.Max( dz, 0f ), 0f );
			}

			case StandardShape.Cone:
			{
				// Apex up at 0.9, a base 0.8 across at -0.9; the side's distance is scaled by how steep it is
				const float slope = 0.8f / 1.8f;
				var side = (Length( x, y, 0f ) - slope * (0.9f - z)) / MathF.Sqrt( 1f + slope * slope );
				return MathF.Max( side, MathF.Max( -0.9f - z, z - 0.9f ) );
			}

			case StandardShape.Pyramid:
			{
				const float slope = 0.8f / 1.7f;
				var side = (MathF.Max( MathF.Abs( x ), MathF.Abs( y ) ) - slope * (0.9f - z)) / MathF.Sqrt( 1f + slope * slope );
				return MathF.Max( side, MathF.Max( -0.8f - z, z - 0.9f ) );
			}

			case StandardShape.Torus:
				return Length( Length( x, y, 0f ) - 0.62f, z, 0f ) - 0.3f;

			case StandardShape.Capsule:
				return Length( x, y, z - Math.Clamp( z, -0.5f, 0.5f ) ) - 0.45f;

			default:
				return float.MaxValue;
		}
	}

	// Evenly along every outline and line by length, the closed ones round to their start
	private static float[] Along( List<float[]> closed, List<float[]> open, int count )
	{
		var total = 0f;
		foreach ( var contour in closed )
			total += LengthOf( contour, true );

		foreach ( var line in open )
			total += LengthOf( line, false );

		var points = new float[ count * 2 ];
		var spacing = total / count;
		var next = 0;
		var walked = 0f;

		void Walk( float[] contour, bool isClosed )
		{
			var corners = contour.Length / 2;
			var segments = isClosed ? corners : corners - 1;
			for ( var i = 0; i < segments && next < count; i++ )
			{
				var j = (i + 1) % corners;
				float ax = contour[ i * 2 ], ay = contour[ i * 2 + 1 ], bx = contour[ j * 2 ], by = contour[ j * 2 + 1 ];
				var length = Length( bx - ax, by - ay, 0f );

				// Each point sits half a spacing into its stretch, so a closed outline's last and first are a spacing apart
				while ( next < count && (next + 0.5f) * spacing <= walked + length )
				{
					var along = length > 0f ? ((next + 0.5f) * spacing - walked) / length : 0f;
					points[ next * 2 ] = ax + (bx - ax) * along;
					points[ next * 2 + 1 ] = ay + (by - ay) * along;
					next++;
				}

				walked += length;
			}
		}

		foreach ( var contour in closed )
			Walk( contour, true );

		foreach ( var line in open )
			Walk( line, false );

		// Rounding can leave the last point or two: they go on the very end
		for ( ; next < count; next++ )
		{
			var last = open.Count > 0 ? open[ ^1 ] : closed[ ^1 ];
			points[ next * 2 ] = last[ open.Count > 0 ? last.Length - 2 : 0 ];
			points[ next * 2 + 1 ] = last[ open.Count > 0 ? last.Length - 1 : 1 ];
		}

		return points;
	}

	// Spread over the inside (even-odd, so a ring's hole stays empty), in a sequence that fills it evenly
	private static float[] Inside( List<float[]> closed, int count )
	{
		float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
		foreach ( var contour in closed )
		{
			for ( var i = 0; i < contour.Length; i += 2 )
			{
				minX = MathF.Min( minX, contour[ i ] );
				maxX = MathF.Max( maxX, contour[ i ] );
				minY = MathF.Min( minY, contour[ i + 1 ] );
				maxY = MathF.Max( maxY, contour[ i + 1 ] );
			}
		}

		var points = new float[ count * 2 ];
		var found = 0;
		for ( var tried = 1; found < count && tried < count * TriesPerPoint; tried++ )
		{
			var x = minX + (maxX - minX) * Halton( tried, 2 );
			var y = minY + (maxY - minY) * Halton( tried, 3 );
			var inside = false;
			foreach ( var contour in closed )
				inside ^= EffectShape.Contains( contour, x, y );

			if ( !inside )
				continue;

			points[ found * 2 ] = x;
			points[ found * 2 + 1 ] = y;
			found++;
		}

		return found == count ? points : Along( closed, new List<float[]>(), count );
	}

	// Over a solid's surface (a thin shell round it) or all through it, in a sequence that spreads them evenly
	private static float[] SolidPoints( StandardShape shape, bool filled, int count )
	{
		var points = new float[ count * 3 ];
		var found = 0;
		for ( var tried = 1; found < count && tried < count * TriesPerPoint; tried++ )
		{
			var x = Halton( tried, 2 ) * 2f - 1f;
			var y = Halton( tried, 3 ) * 2f - 1f;
			var z = Halton( tried, 5 ) * 2f - 1f;
			var outside = DistanceOutside( shape, x, y, z );
			if ( filled ? outside > 0f : MathF.Abs( outside ) > Shell )
				continue;

			points[ found * 3 ] = x;
			points[ found * 3 + 1 ] = y;
			points[ found * 3 + 2 ] = z;
			found++;
		}

		return points;
	}

	// Three turns round an upright axis, bottom to top
	private const float HelixTurns = 3f;
	private const float HelixRadius = 0.55f;
	private const float HelixHalfHeight = 0.9f;

	private static float[] HelixPoints( int count )
	{
		var points = new float[ count * 3 ];
		for ( var i = 0; i < count; i++ )
		{
			var t = count > 1 ? i / (count - 1f) : 0.5f;
			var angle = t * HelixTurns * MathF.Tau;
			points[ i * 3 ] = MathF.Cos( angle ) * HelixRadius;
			points[ i * 3 + 1 ] = MathF.Sin( angle ) * HelixRadius;
			points[ i * 3 + 2 ] = -HelixHalfHeight + 2f * HelixHalfHeight * t;
		}

		return points;
	}

	// The helix as seen from the front: a wave up and down
	private static float[] HelixFromFront()
	{
		const int corners = 72;
		var points = new float[ corners * 2 ];
		for ( var i = 0; i < corners; i++ )
		{
			var t = i / (corners - 1f);
			points[ i * 2 ] = MathF.Sin( t * HelixTurns * MathF.Tau ) * HelixRadius;
			points[ i * 2 + 1 ] = -HelixHalfHeight + 2f * HelixHalfHeight * t;
		}

		return points;
	}

	// The radical inverse of index in base: a sequence that fills a range evenly however far it goes
	private static float Halton( int index, int radix )
	{
		var result = 0f;
		var fraction = 1f / radix;
		for ( var i = index; i > 0; i /= radix )
		{
			result += fraction * (i % radix);
			fraction /= radix;
		}

		return result;
	}

	private static float LengthOf( float[] contour, bool closed )
	{
		var corners = contour.Length / 2;
		var length = 0f;
		for ( var i = 0; i < (closed ? corners : corners - 1); i++ )
		{
			var j = (i + 1) % corners;
			length += Length( contour[ j * 2 ] - contour[ i * 2 ], contour[ j * 2 + 1 ] - contour[ i * 2 + 1 ], 0f );
		}

		return length;
	}

	private static float Length( float x, float y, float z ) => MathF.Sqrt( x * x + y * y + z * z );

	private static float Degrees( float degrees ) => degrees * MathF.PI / 180f;

	private static float[] Ellipse( float x, float y, float width, float height )
	{
		var points = new float[ EffectShape.CirclePoints * 2 ];
		for ( var i = 0; i < EffectShape.CirclePoints; i++ )
		{
			var angle = i * MathF.Tau / EffectShape.CirclePoints;
			points[ i * 2 ] = x + MathF.Cos( angle ) * width;
			points[ i * 2 + 1 ] = y + MathF.Sin( angle ) * height;
		}

		return points;
	}

	// A circle's edge from one angle to another (radians, either way round), both ends included
	private static float[] ArcPoints( float x, float y, float radius, float from, float to, float height = -1f )
	{
		var corners = Math.Max( 2, (int)MathF.Ceiling( MathF.Abs( to - from ) / MathF.Tau * EffectShape.CirclePoints ) + 1 );
		var points = new float[ corners * 2 ];
		for ( var i = 0; i < corners; i++ )
		{
			var angle = from + (to - from) * i / (corners - 1f);
			points[ i * 2 ] = x + MathF.Cos( angle ) * radius;
			points[ i * 2 + 1 ] = y + MathF.Sin( angle ) * (height < 0f ? radius : height);
		}

		return points;
	}

	private static float[] Joined( params float[][] parts )
	{
		var length = 0;
		foreach ( var part in parts )
			length += part.Length;

		var joined = new float[ length ];
		var at = 0;
		foreach ( var part in parts )
		{
			Array.Copy( part, 0, joined, at, part.Length );
			at += part.Length;
		}

		return joined;
	}

	private static float[] Shifted( float[] points, float x, float y )
	{
		for ( var i = 0; i < points.Length; i += 2 )
		{
			points[ i ] += x;
			points[ i + 1 ] += y;
		}

		return points;
	}

	// A regular polygon with a corner at the top (turned by turn, radians), moved to sit in the middle top to bottom
	private static float[] Regular( int sides, float radius, float turn )
	{
		var points = new float[ sides * 2 ];
		float top = float.MinValue, bottom = float.MaxValue;
		for ( var i = 0; i < sides; i++ )
		{
			var angle = MathF.PI / 2f + turn + i * MathF.Tau / sides;
			points[ i * 2 ] = MathF.Cos( angle ) * radius;
			points[ i * 2 + 1 ] = MathF.Sin( angle ) * radius;
			top = MathF.Max( top, points[ i * 2 + 1 ] );
			bottom = MathF.Min( bottom, points[ i * 2 + 1 ] );
		}

		return Shifted( points, 0f, -(top + bottom) / 2f );
	}

	private static float[] Box( float halfWidth, float halfHeight ) =>
		new[] { -halfWidth, -halfHeight, halfWidth, -halfHeight, halfWidth, halfHeight, -halfWidth, halfHeight };

	private static float[] RoundedBox( float halfWidth, float halfHeight, float corner )
	{
		float right = halfWidth - corner, top = halfHeight - corner;
		return Joined(
			ArcPoints( right, -top, corner, -MathF.PI / 2f, 0f ),
			ArcPoints( right, top, corner, 0f, MathF.PI / 2f ),
			ArcPoints( -right, top, corner, MathF.PI / 2f, MathF.PI ),
			ArcPoints( -right, -top, corner, MathF.PI, MathF.PI * 1.5f ) );
	}

	// A plus of arms reach long and half thick across, turned by turn (radians)
	private static float[] Plus( float reach, float half, float turn )
	{
		var points = new[]
		{
			-half, reach, half, reach, half, half, reach, half, reach, -half, half, -half,
			half, -reach, -half, -reach, -half, -half, -reach, -half, -reach, half, -half, half,
		};

		var (sin, cos) = MathF.SinCos( turn );
		for ( var i = 0; i < points.Length; i += 2 )
		{
			var (x, y) = (points[ i ], points[ i + 1 ]);
			points[ i ] = x * cos - y * sin;
			points[ i + 1 ] = x * sin + y * cos;
		}

		return points;
	}

	// A circle of radius outer with a circle of radius inner taken out of it, the inner one offset to the right
	private static float[] Crescent( float outer, float offset, float inner )
	{
		// Where the two circles cross
		var crossX = (offset * offset + outer * outer - inner * inner) / (2f * offset);
		var crossY = MathF.Sqrt( MathF.Max( outer * outer - crossX * crossX, 0f ) );
		var outerAngle = MathF.Atan2( crossY, crossX );
		var innerAngle = MathF.Atan2( crossY, crossX - offset );

		// Round the left of the big circle, then back along the left of the small one
		return Joined(
			ArcPoints( 0f, 0f, outer, outerAngle, MathF.Tau - outerAngle ),
			ArcPoints( offset, 0f, inner, MathF.Tau - innerAngle, innerAngle ) );
	}

	private static float[] Wave()
	{
		const int corners = 40;
		var points = new float[ corners * 2 ];
		for ( var i = 0; i < corners; i++ )
		{
			var t = i / (corners - 1f);
			points[ i * 2 ] = -0.95f + 1.9f * t;
			points[ i * 2 + 1 ] = 0.45f * MathF.Sin( t * 3f * MathF.PI );
		}

		return points;
	}

	private static float[] Zigzag()
	{
		const int corners = 8;
		var points = new float[ corners * 2 ];
		for ( var i = 0; i < corners; i++ )
		{
			points[ i * 2 ] = -0.95f + 1.9f * i / (corners - 1f);
			points[ i * 2 + 1 ] = i % 2 == 0 ? -0.4f : 0.4f;
		}

		return points;
	}

	private static float[] Spiral()
	{
		const int corners = 120;
		const float turns = 3f;
		var points = new float[ corners * 2 ];
		for ( var i = 0; i < corners; i++ )
		{
			var t = i / (corners - 1f);
			var radius = 0.08f + 0.85f * t;
			var angle = t * turns * MathF.Tau;
			points[ i * 2 ] = MathF.Cos( angle ) * radius;
			points[ i * 2 + 1 ] = MathF.Sin( angle ) * radius;
		}

		return points;
	}

	// Bernoulli's lemniscate: an eight on its side
	private static float[] Lemniscate( float reach )
	{
		const int corners = 64;
		var points = new float[ corners * 2 ];
		for ( var i = 0; i < corners; i++ )
		{
			var t = i * MathF.Tau / corners;
			var (sin, cos) = MathF.SinCos( t );
			var scale = reach / (1f + sin * sin);
			points[ i * 2 ] = cos * scale;
			points[ i * 2 + 1 ] = sin * cos * scale;
		}

		return points;
	}

	// Seen from the front and a little above: straight sides and round ends
	private static float[] Cylinder()
	{
		const float half = 0.6f, top = 0.62f, lip = 0.22f;
		return Joined(
			ArcPoints( 0f, -top, half, MathF.PI, MathF.Tau, lip ),
			ArcPoints( 0f, top, half, 0f, MathF.PI, lip ) );
	}

	private static float[] Cone()
	{
		const float half = 0.68f, bottom = 0.62f, lip = 0.22f;
		return Joined( new[] { 0f, 0.9f }, ArcPoints( 0f, -bottom, half, 0f, -MathF.PI, lip ) );
	}

	// Upright: straight sides between two round ends
	private static float[] Stadium( float radius, float half ) => Joined(
		ArcPoints( 0f, half, radius, 0f, MathF.PI ),
		ArcPoints( 0f, -half, radius, MathF.PI, MathF.Tau ) );
}
