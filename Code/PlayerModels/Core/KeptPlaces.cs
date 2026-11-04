using System;

namespace PlayerModels.Core;

/// <summary>
/// Where the sprites a layer keeps out sit (see <see cref="EffectLayer.Keep"/>), and a beam's points: spread evenly along
/// where the layer's sprites come from, in index order, so a particle function can move each by its index (runes round a
/// ring, orbs one after another along a line). Places are in the layer's own space (forwards, left, up), as x, y, z one
/// after another; a line to another joint is given as how far along it (0 to 1) in x, as its end moves every frame.
/// Worked out once, when the layer is set up.
/// </summary>
public static class KeptPlaces
{
	// How far round each place is from the last on a ball: the golden angle, so they spread evenly however many
	private const float GoldenAngle = 2.39996323f;

	// All around the body: an upright ball, this much narrower than it is tall (as the emitter sends them)
	private const float AroundBodyWidth = 0.35f;

	/// <summary>
	/// <paramref name="count"/> places spread along <paramref name="shape"/> reaching <paramref name="spread"/>: a
	/// standard or drawn shape's from <paramref name="formation"/> (its points in a cube from -1 to 1, see
	/// <see cref="StandardShapes.Formation"/>, asked for with <paramref name="count"/> points). A cone opens
	/// <paramref name="angle"/> degrees.
	/// </summary>
	public static float[] Of( LayerShape shape, float spread, int count, float angle = 0f, float[] formation = null )
	{
		count = Math.Max( 0, count );
		var places = new float[ count * 3 ];
		for ( var i = 0; i < count; i++ )
		{
			var (x, y, z) = Place( shape, spread, i, count, angle, formation );
			places[ i * 3 ] = x;
			places[ i * 3 + 1 ] = y;
			places[ i * 3 + 2 ] = z;
		}

		return places;
	}

	private static (float X, float Y, float Z) Place( LayerShape shape, float spread, int i, int count, float angle, float[] formation )
	{
		var along = count <= 1 ? 0.5f : i / (float)(count - 1);
		switch ( shape )
		{
			case LayerShape.Ring:
			{
				// Flat round the way the layer faces, as the ring emitter sends them
				var turn = i / (float)Math.Max( 1, count ) * MathF.Tau;
				return (MathF.Cos( turn ) * spread, MathF.Sin( turn ) * spread, 0f);
			}

			case LayerShape.Sphere or LayerShape.AroundBody:
			{
				var (x, y, z) = OnBall( i, count );
				var width = shape == LayerShape.AroundBody ? AroundBodyWidth : 1f;
				return (x * spread * width, y * spread * width, z * spread);
			}

			case LayerShape.Box:
				return ((Halton( i + 1, 2 ) * 2f - 1f) * spread, (Halton( i + 1, 3 ) * 2f - 1f) * spread, (Halton( i + 1, 5 ) * 2f - 1f) * spread);

			case LayerShape.Cone:
			{
				// Out along the cone, turning round its middle as they go
				var reach = along * spread;
				var wide = reach * MathF.Tan( Math.Clamp( angle, 0f, 89f ) * MathF.PI / 180f );
				var turn = i * GoldenAngle;
				return (reach, MathF.Cos( turn ) * wide, MathF.Sin( turn ) * wide);
			}

			case LayerShape.Line:
				return (along, 0f, 0f);

			case LayerShape.Shape when formation is { Length: >= 3 }:
			{
				var points = formation.Length / 3;
				var k = count >= points ? i % points : (int)((long)i * points / Math.Max( 1, count ));
				return (formation[ k * 3 ] * spread, formation[ k * 3 + 1 ] * spread, formation[ k * 3 + 2 ] * spread);
			}

			default:
				return (0f, 0f, 0f);
		}
	}

	// Place i of count on a ball of 1, spread evenly (a Fibonacci lattice)
	private static (float X, float Y, float Z) OnBall( int i, int count )
	{
		if ( count <= 1 )
			return (0f, 0f, 0f);

		var z = 1f - (i + 0.5f) / count * 2f;
		var round = MathF.Sqrt( Math.Max( 0f, 1f - z * z ) );
		var turn = i * GoldenAngle;
		return (MathF.Cos( turn ) * round, MathF.Sin( turn ) * round, z);
	}

	// The low-discrepancy sequence of base b: spreads points in a box more evenly than random
	private static float Halton( int index, int b )
	{
		var result = 0f;
		var fraction = 1f;
		while ( index > 0 )
		{
			fraction /= b;
			result += fraction * (index % b);
			index /= b;
		}

		return result;
	}
}
