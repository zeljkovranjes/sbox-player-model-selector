using System;
using Sandbox;

namespace PlayerModels.UI;

/// <summary>
/// The Advanced editor's camera: starts looking at the whole body and goes wherever it is moved, with no point it is
/// pulled towards, so any part of the model can be looked at up close. It stays near enough not to lose the model.
/// </summary>
internal sealed class WindowCamera
{
	private const float StartDistance = 120f;
	private const float StartHeight = 38f;
	private const float StartYaw = 180f;

	private const float MostPitch = 85f;
	private const float MostReach = 400f;
	private const float DegreesPerPixel = 0.25f;
	private const float UnitsPerPixel = 0.08f;
	private const float UnitsPerSecond = 40f;
	private const float FastFactor = 3f;

	private float _yaw;
	private float _pitch;

	public WindowCamera()
	{
		Reset();
	}

	/// <summary>Where it is.</summary>
	public Vector3 Position { get; private set; }

	/// <summary>Which way it looks.</summary>
	public Rotation Rotation => Rotation.From( _pitch, _yaw, 0f );

	/// <summary>Back to looking at the whole body.</summary>
	public void Reset()
	{
		Position = new Vector3( StartDistance, 0f, StartHeight );
		_yaw = StartYaw;
		_pitch = 0f;
	}

	/// <summary>Turns it by a mouse drag, in pixels.</summary>
	public void Look( float x, float y )
	{
		_yaw -= x * DegreesPerPixel;
		_pitch = Math.Clamp( _pitch + y * DegreesPerPixel, -MostPitch, MostPitch );
	}

	/// <summary>Slides it sideways and up or down by a mouse drag, in pixels: what is under the cursor stays under it.</summary>
	public void Pan( float x, float y )
	{
		MoveBy( (Rotation.Right * -x + Rotation.Up * y) * UnitsPerPixel );
	}

	/// <summary>Moves it straight ahead by <paramref name="distance"/>; negative moves it back.</summary>
	public void Dolly( float distance )
	{
		MoveBy( Rotation.Forward * distance );
	}

	/// <summary>Flies it for <paramref name="seconds"/>: <paramref name="move"/> is forward, left and up, each -1 to 1.</summary>
	public void Fly( Vector3 move, float seconds, bool fast )
	{
		if ( move.IsNearZeroLength )
			return;

		MoveBy( Rotation * move.Normal * UnitsPerSecond * (fast ? FastFactor : 1f) * seconds );
	}

	private void MoveBy( Vector3 change )
	{
		var moved = Position + change;
		Position = new Vector3(
			Math.Clamp( moved.x, -MostReach, MostReach ),
			Math.Clamp( moved.y, -MostReach, MostReach ),
			Math.Clamp( moved.z, -MostReach, MostReach ) );
	}
}
