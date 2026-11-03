using System;
using Sandbox;

namespace PlayerModels.UI;

/// <summary>
/// The Advanced editor's camera, moved the way Blender's viewport is: it turns around the point it looks at (orbit),
/// slides that point across the view (pan), moves nearer or further (zoom), or flies (walk). It works out where a point
/// shows on the window, and what a spot on the window looks at, for picking items and using the gizmo.
/// </summary>
internal sealed class WindowCamera
{
	/// <summary>The lens, by the window's height.</summary>
	public const float FieldOfView = 41f;

	private const float StartDistance = 120f;
	private const float StartHeight = 38f;
	private const float StartYaw = 180f;

	private const float MostPitch = 89.5f;
	private const float MostReach = 400f;
	// Never nearer than this: inside the model, orbiting swings the camera through its body and items
	private const float LeastDistance = 15f;
	private const float MostDistance = 400f;
	private const float DegreesPerPixel = 0.3f;
	private const float ZoomPerStep = 1.1f;
	private const float ZoomPerPixel = 0.006f;
	private const float UnitsPerSecond = 40f;
	private const float FastFactor = 3f;

	private float _yaw;
	private float _pitch;

	public WindowCamera()
	{
		Reset();
	}

	/// <summary>The point it turns around.</summary>
	public Vector3 Focus { get; private set; }

	/// <summary>How far it is from that point.</summary>
	public float Distance { get; private set; }

	/// <summary>Which way it looks.</summary>
	public Rotation Rotation => Rotation.From( _pitch, _yaw, 0f );

	/// <summary>Where it is.</summary>
	public Vector3 Position => Focus - Rotation.Forward * Distance;

	/// <summary>Back to looking at the whole body from the front.</summary>
	public void Reset()
	{
		Focus = new Vector3( 0f, 0f, StartHeight );
		Distance = StartDistance;
		_yaw = StartYaw;
		_pitch = 0f;
	}

	/// <summary>Turns it around its point by a mouse drag, in pixels.</summary>
	public void Orbit( float x, float y )
	{
		_yaw -= x * DegreesPerPixel;
		_pitch = Math.Clamp( _pitch + y * DegreesPerPixel, -MostPitch, MostPitch );
	}

	/// <summary>Turns it where it stands by a mouse drag, in pixels, the way walking looks around.</summary>
	public void Look( float x, float y )
	{
		var position = Position;
		_yaw -= x * DegreesPerPixel;
		_pitch = Math.Clamp( _pitch + y * DegreesPerPixel, -MostPitch, MostPitch );
		Focus = Clamped( position + Rotation.Forward * Distance );
	}

	/// <summary>Turns it around its point by degrees: the numpad's steps.</summary>
	public void Step( float yaw, float pitch )
	{
		_yaw += yaw;
		_pitch = Math.Clamp( _pitch + pitch, -MostPitch, MostPitch );
	}

	/// <summary>Looks from a set way at its point: the numpad's views.</summary>
	public void View( float yaw, float pitch )
	{
		_yaw = yaw;
		_pitch = Math.Clamp( pitch, -MostPitch, MostPitch );
	}

	/// <summary>
	/// Slides its point by a mouse drag of <paramref name="x"/>, <paramref name="y"/> pixels on a window
	/// <paramref name="height"/> pixels tall: what is at the point stays under the cursor.
	/// </summary>
	public void Pan( float x, float y, float height )
	{
		var units = UnitsPerPixel( Distance, height );
		Focus = Clamped( Focus + (Rotation.Right * -x + Rotation.Up * y) * units );
	}

	/// <summary>
	/// Moves nearer its point by a mouse wheel step; negative moves away. One step at most each time, as some wheels send
	/// several for one notch.
	/// </summary>
	public void Zoom( float steps )
	{
		var step = Math.Clamp( steps, -1f, 1f );
		Distance = Math.Clamp( Distance * MathF.Pow( ZoomPerStep, -step ), LeastDistance, MostDistance );
	}

	/// <summary>Moves nearer its point by a mouse drag up, in pixels; down moves away.</summary>
	public void ZoomByDrag( float y )
	{
		Distance = Math.Clamp( Distance * MathF.Exp( y * ZoomPerPixel ), LeastDistance, MostDistance );
	}

	/// <summary>Looks at <paramref name="center"/> from far enough to see <paramref name="radius"/> around it.</summary>
	public void Frame( Vector3 center, float radius )
	{
		Focus = Clamped( center );
		Distance = Math.Clamp( radius / Tan * 1.2f, LeastDistance, MostDistance );
	}

	/// <summary>Flies it for <paramref name="seconds"/>: <paramref name="move"/> is forward, left and up, each -1 to 1.</summary>
	public void Fly( Vector3 move, float seconds, bool fast )
	{
		if ( move.IsNearZeroLength )
			return;

		Focus = Clamped( Focus + Rotation * move.Normal * UnitsPerSecond * (fast ? FastFactor : 1f) * seconds );
	}

	/// <summary>
	/// Where <paramref name="world"/> shows on a window <paramref name="aspect"/> wide for its height: x and y from 0 to 1
	/// across and down it, z how far ahead it is (0 or less when behind).
	/// </summary>
	public Vector3 Project( Vector3 world, float aspect )
	{
		var local = Rotation.Inverse * (world - Position);
		if ( local.x <= 0.01f )
			return new Vector3( 0.5f, 0.5f, local.x );

		return new Vector3( 0.5f - local.y / (local.x * Tan * aspect) * 0.5f, 0.5f - local.z / (local.x * Tan) * 0.5f, local.x );
	}

	/// <summary>The ray through <paramref name="spot"/> on the window, x and y from 0 to 1 across and down it.</summary>
	public Ray RayThrough( Vector2 spot, float aspect )
	{
		var direction = new Vector3( 1f, (0.5f - spot.x) * 2f * Tan * aspect, (0.5f - spot.y) * 2f * Tan );
		return new Ray( Position, (Rotation * direction).Normal );
	}

	/// <summary>How far one pixel reaches at <paramref name="depth"/> on a window <paramref name="height"/> pixels tall.</summary>
	public static float UnitsPerPixel( float depth, float height )
	{
		return height <= 0f ? 0f : 2f * depth * Tan / height;
	}

	private static float Tan => MathF.Tan( FieldOfView * 0.5f * MathF.PI / 180f );

	private static Vector3 Clamped( Vector3 point )
	{
		return new Vector3(
			Math.Clamp( point.x, -MostReach, MostReach ),
			Math.Clamp( point.y, -MostReach, MostReach ),
			Math.Clamp( point.z, -MostReach, MostReach ) );
	}
}
