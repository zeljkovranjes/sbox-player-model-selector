using System;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Sends an effect layer's sprites out from points worked out beforehand: a standard shape's formation (see
/// <see cref="Core.StandardShapes.Formation"/>), stepping round its points far apart each time so the whole shape shows
/// however few sprites are out, or spread evenly along a line
/// (across the way the layer faces, or to another joint, moved every frame). Points are in the layer's own space and
/// sizes; each sprite starts outwards from the middle at <see cref="Velocity"/>. Made only by effect layers, never saved:
/// public only as the scene's components are. Emits as the effect steps: no allocations.
/// </summary>
public sealed class EffectPointsEmitter : ParticleEmitter
{
	/// <summary>The points, as x, y, z one after another, in a cube from -1 to 1; scaled by <see cref="Scale"/>.</summary>
	internal float[] Points
	{
		get => _points;
		set
		{
			_points = value ?? Array.Empty<float>();
			_stride = StrideFor( _points.Length / 3 );
			_next = 0;
		}
	}

	/// <summary>How far a point at 1 is from the middle, in the layer's own units.</summary>
	internal float Scale { get; set; } = 1f;

	/// <summary>Whether sprites come from a line, <see cref="LineStart"/> to <see cref="LineEnd"/>, rather than the points.</summary>
	internal bool IsLine { get; set; }

	/// <summary>The ends of the line, in the layer's own space.</summary>
	internal Vector3 LineStart { get; set; }
	internal Vector3 LineEnd { get; set; }

	/// <summary>How fast each sprite starts outwards from the middle, in world units a second.</summary>
	internal float Velocity { get; set; }

	private float[] _points = Array.Empty<float>();
	private int _stride = 1;
	private int _next;
	private float _along;

	// About a golden share of the way round each time, and never a step that comes back before every point is used
	private const float StrideShare = 0.618f;

	public override bool Emit( ParticleEffect target )
	{
		Vector3 local;
		if ( IsLine )
		{
			// Spread evenly along it, each a golden share further than the last, so a burst fills it without gaps
			_along = (_along + StrideShare) % 1f;
			local = Vector3.Lerp( LineStart, LineEnd, _along );
		}
		else
		{
			var count = _points.Length / 3;
			if ( count == 0 )
				return false;

			var i = _next % count;
			_next = (i + _stride) % count;
			local = new Vector3( _points[ i * 3 ], _points[ i * 3 + 1 ], _points[ i * 3 + 2 ] ) * Scale;
		}

		var transform = WorldTransform;
		var particle = target.Emit( transform.PointToWorld( local ), Delta );
		if ( Velocity != 0f )
		{
			var outwards = local.IsNearlyZero() ? Vector3.Random.Normal : local.Normal;
			particle.Velocity += transform.Rotation * outwards * Velocity;
		}

		return true;
	}

	private static int StrideFor( int count )
	{
		var stride = Math.Max( 1, (int)MathF.Round( count * StrideShare ) );
		while ( stride > 1 && Gcd( stride, count ) != 1 )
			stride--;

		return stride;
	}

	private static int Gcd( int a, int b ) => b == 0 ? a : Gcd( b, a % b );
}
