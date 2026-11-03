using System;
using System.Globalization;
using Sandbox;

namespace PlayerModels.UI;

/// <summary>
/// The Advanced editor's gizmo, after Blender's: arrows to move an item along an axis, rings to turn it, boxes to size it,
/// and the transform an operation makes from the mouse (or a typed number) as it goes. Axes are the item's joint's
/// forward, left and up, as the sliders have them. An item is only ever sized as a whole.
/// </summary>
internal sealed class TransformGizmo
{
	/// <summary>Which handles show: the toolbar's tools.</summary>
	internal enum Tool { Select, Move, Rotate, Scale, Transform }

	/// <summary>What an operation changes.</summary>
	internal enum Kind { Move, Rotate, Scale }

	/// <summary>A handle of the gizmo.</summary>
	internal enum Handle { None, MoveX, MoveY, MoveZ, MoveView, RotateX, RotateY, RotateZ, RotateView, ScaleX, ScaleY, ScaleZ }

	/// <summary>Where an item's joint is and which way it faces (its axes), and how much the model is scaled there.</summary>
	internal readonly record struct Frame( Vector3 Pivot, Rotation Axes, float Scale );

	/// <summary>What an operation edits: an item's offset from its joint, its turn and its size.</summary>
	internal readonly record struct Pose( Vector3 Offset, Rotation Turn, float Size );

	// How big the gizmo is for how far away it is, and how near the cursor has to be to a handle
	private const float SizeForDistance = 0.16f;
	private const float GrabPixels = 12f;
	private const int RingSegments = 48;
	private const float ViewRing = 1.18f;

	// Ctrl snaps to these; Shift makes the mouse this much finer
	private const float SnapUnits = 1f;
	private const float SnapDegrees = 5f;
	private const float SnapScale = 0.1f;
	private const float Precise = 0.1f;

	private static readonly Color Hover = new( 1f, 0.85f, 0.2f );
	private static readonly Color Free = new( 0.95f, 0.95f, 0.95f );
	private static readonly Color[] AxisColors = { new( 1f, 0.3f, 0.3f ), new( 0.45f, 0.9f, 0.3f ), new( 0.3f, 0.55f, 1f ) };
	private static readonly string[] AxisNames = { "X", "Y", "Z" };

	/// <summary>An operation under way, started from a key (G, R, S) or by dragging a handle.</summary>
	internal sealed class Operation
	{
		public Kind Kind;
		public int Axis = -1;
		public bool Plane;
		public bool FromHandle;
		public Vector2 StartMouse;
		public Pose Start;
		public string Typed = "";
	}

	/// <summary>Which axis direction <paramref name="axis"/> (0 to 2) is in the world, for <paramref name="frame"/>.</summary>
	internal static Vector3 AxisOf( in Frame frame, int axis ) => axis switch
	{
		0 => frame.Axes.Forward,
		1 => frame.Axes.Left,
		_ => frame.Axes.Up,
	};

	/// <summary>How long the gizmo's arrows are, so it stays the same size on the window.</summary>
	internal static float SizeAt( in Frame frame, WindowCamera camera ) => (frame.Pivot - camera.Position).Length * SizeForDistance;

	/// <summary>The handle under <paramref name="mouse"/> (pixels on a window <paramref name="view"/> big), for <paramref name="tool"/>.</summary>
	internal static Handle HandleAt( Tool tool, in Frame frame, WindowCamera camera, Vector2 view, Vector2 mouse )
	{
		if ( tool == Tool.Select )
			return Handle.None;

		var aspect = view.x / view.y;
		var length = SizeAt( frame, camera );
		var center = Pixels( camera.Project( frame.Pivot, aspect ), view );
		var best = Handle.None;
		var nearest = GrabPixels;

		void Consider( Handle handle, float distance )
		{
			if ( distance >= nearest )
				return;

			nearest = distance;
			best = handle;
		}

		var moves = tool is Tool.Move or Tool.Transform;
		var turns = tool is Tool.Rotate or Tool.Transform;
		var sizes = tool is Tool.Scale or Tool.Transform;

		for ( var axis = 0; axis < 3; axis++ )
		{
			var direction = AxisOf( frame, axis );
			if ( moves )
				Consider( Handle.MoveX + axis, ToSegment( mouse, center, Pixels( camera.Project( frame.Pivot + direction * length, aspect ), view ) ) );

			if ( sizes )
			{
				var at = frame.Pivot + direction * length * (tool == Tool.Transform ? 0.7f : 1f);
				Consider( Handle.ScaleX + axis, (mouse - Pixels( camera.Project( at, aspect ), view )).Length );
			}

			if ( turns )
				Consider( Handle.RotateX + axis, ToRing( mouse, frame.Pivot, direction, length, camera, view ) );
		}

		var viewRadius = (Pixels( camera.Project( frame.Pivot + camera.Rotation.Up * length * ViewRing, aspect ), view ) - center).Length;
		if ( turns )
			Consider( Handle.RotateView, MathF.Abs( (mouse - center).Length - viewRadius ) );

		if ( moves )
			Consider( Handle.MoveView, MathF.Max( 0f, (mouse - center).Length - viewRadius * 0.15f ) );

		return best;
	}

	/// <summary>The operation a handle starts.</summary>
	internal static Operation Grab( Handle handle, Vector2 mouse, Pose start )
	{
		var operation = new Operation { StartMouse = mouse, Start = start, FromHandle = true };
		switch ( handle )
		{
			case >= Handle.MoveX and <= Handle.MoveZ:
				operation.Kind = Kind.Move;
				operation.Axis = handle - Handle.MoveX;
				break;
			case Handle.MoveView:
				operation.Kind = Kind.Move;
				break;
			case >= Handle.RotateX and <= Handle.RotateZ:
				operation.Kind = Kind.Rotate;
				operation.Axis = handle - Handle.RotateX;
				break;
			case Handle.RotateView:
				operation.Kind = Kind.Rotate;
				break;
			default:
				operation.Kind = Kind.Scale;
				operation.Axis = handle - Handle.ScaleX;
				break;
		}

		return operation;
	}

	/// <summary>
	/// The pose <paramref name="operation"/> makes with the mouse at <paramref name="mouse"/>: a typed number, when there
	/// is one, in place of the mouse. <paramref name="snap"/> (Ctrl) rounds it; <paramref name="precise"/> (Shift) slows the mouse.
	/// </summary>
	internal static Pose Apply( Operation operation, in Frame frame, WindowCamera camera, Vector2 view, Vector2 mouse, bool snap, bool precise )
	{
		var start = operation.Start;
		var aspect = view.x / view.y;
		var center = Pixels( camera.Project( frame.Pivot, aspect ), view );
		var drag = (mouse - operation.StartMouse) * (precise ? Precise : 1f);
		var typed = Typed( operation, out var number );

		switch ( operation.Kind )
		{
			case Kind.Move:
			{
				Vector3 world;
				if ( typed )
				{
					world = AxisOf( frame, Math.Max( operation.Axis, 0 ) ) * number * frame.Scale;
				}
				else if ( operation.Axis >= 0 && !operation.Plane )
				{
					var axis = AxisOf( frame, operation.Axis );
					var length = SizeAt( frame, camera );
					var along = Pixels( camera.Project( frame.Pivot + axis * length, aspect ), view ) - center;
					world = along.LengthSquared < 1f ? Vector3.Zero : axis * (Vector2.Dot( drag, along ) / along.LengthSquared * length);
				}
				else
				{
					var depth = MathF.Max( camera.Project( frame.Pivot, aspect ).z, 1f );
					var units = WindowCamera.UnitsPerPixel( depth, view.y );
					world = (camera.Rotation.Right * drag.x - camera.Rotation.Up * drag.y) * units;
					if ( operation.Axis >= 0 )
					{
						var excluded = AxisOf( frame, operation.Axis );
						world -= excluded * Vector3.Dot( world, excluded );
					}
				}

				var offset = start.Offset + frame.Axes.Inverse * world / MathF.Max( frame.Scale, 0.01f );
				if ( snap && !typed )
					offset = new Vector3( Round( offset.x, SnapUnits ), Round( offset.y, SnapUnits ), Round( offset.z, SnapUnits ) );

				return start with { Offset = offset };
			}

			case Kind.Rotate:
			{
				var axis = operation.Axis >= 0 ? AxisOf( frame, operation.Axis ) : camera.Rotation.Forward;
				float degrees;
				if ( typed )
				{
					degrees = number;
				}
				else
				{
					var from = operation.StartMouse - center;
					var to = mouse - center;
					var angle = MathF.Atan2( from.x * to.y - from.y * to.x, Vector2.Dot( from, to ) ) * 180f / MathF.PI;
					angle *= precise ? Precise : 1f;

					// Clockwise on the window is a turn about an axis pointing away from the viewer; about one pointing
					// at them, the other way
					degrees = Vector3.Dot( axis, camera.Rotation.Forward ) < 0f ? -angle : angle;
					if ( snap )
						degrees = Round( degrees, SnapDegrees );
				}

				var local = frame.Axes.Inverse * axis;
				return start with { Turn = Rotation.FromAxis( local, degrees ) * start.Turn };
			}

			default:
			{
				float factor;
				if ( typed )
				{
					factor = number;
				}
				else if ( operation.FromHandle && operation.Axis >= 0 )
				{
					var axis = AxisOf( frame, operation.Axis );
					var length = SizeAt( frame, camera );
					var along = Pixels( camera.Project( frame.Pivot + axis * length, aspect ), view ) - center;
					factor = along.LengthSquared < 1f ? 1f : 1f + Vector2.Dot( drag, along ) / along.LengthSquared;
				}
				else
				{
					var from = (operation.StartMouse - center).Length;
					factor = from < 1f ? 1f : (mouse - center).Length / from;
					factor = precise ? 1f + (factor - 1f) * Precise : factor;
				}

				var size = start.Size * MathF.Max( factor, 0.01f );
				return start with { Size = snap && !typed ? Round( size, SnapScale ) : size };
			}
		}
	}

	/// <summary>What the operation is doing, for the window to say.</summary>
	internal static string Describe( Operation operation, in Pose start, in Pose now )
	{
		var axis = operation.Axis < 0 ? "" : operation.Plane ? $" not along {AxisNames[ operation.Axis ]}" : $" along {AxisNames[ operation.Axis ]}";
		var typed = operation.Typed.Length > 0 ? $"  [{operation.Typed}]" : "";
		return operation.Kind switch
		{
			Kind.Move => $"Move{axis}: {(now.Offset - start.Offset).Length:0.0}{typed}",
			Kind.Rotate => $"Rotate{(operation.Axis < 0 ? " around the view" : $" around {AxisNames[ operation.Axis ]}")}{typed}",
			_ => $"Scale: {now.Size / MathF.Max( start.Size, 0.01f ):0.00}x{typed}",
		};
	}

	/// <summary>Draws the gizmo for <paramref name="tool"/>, the hovered handle lit; during an operation, its axis instead.</summary>
	internal static void Draw( DebugOverlaySystem overlay, Tool tool, in Frame frame, WindowCamera camera, Handle hovered, Operation operation )
	{
		var length = SizeAt( frame, camera );

		if ( operation is not null )
		{
			if ( operation.Axis >= 0 )
			{
				var axis = AxisOf( frame, operation.Axis );
				overlay.Line( frame.Pivot - axis * length * 40f, frame.Pivot + axis * length * 40f, AxisColors[ operation.Axis ], 0f, global::Transform.Zero, true );
			}

			return;
		}

		var moves = tool is Tool.Move or Tool.Transform;
		var turns = tool is Tool.Rotate or Tool.Transform;
		var sizes = tool is Tool.Scale or Tool.Transform;

		for ( var axis = 0; axis < 3; axis++ )
		{
			var direction = AxisOf( frame, axis );
			if ( moves )
			{
				var color = hovered == Handle.MoveX + axis ? Hover : AxisColors[ axis ];
				overlay.Line( frame.Pivot, frame.Pivot + direction * length, color, 0f, global::Transform.Zero, true );
				overlay.TaperedCylinder( frame.Pivot + direction * length * 0.86f, frame.Pivot + direction * length * 1.08f, length * 0.05f, 0f, color, 0f, global::Transform.Zero, true, 10 );
			}

			if ( sizes )
			{
				var color = hovered == Handle.ScaleX + axis ? Hover : AxisColors[ axis ];
				var tip = frame.Pivot + direction * length * (tool == Tool.Transform ? 0.7f : 1f);
				if ( !moves )
					overlay.Line( frame.Pivot, tip, color, 0f, global::Transform.Zero, true );

				overlay.Box( tip, Vector3.One * length * 0.08f, color, 0f, global::Transform.Zero, true );
			}

			if ( turns )
				Ring( overlay, frame.Pivot, direction, length, hovered == Handle.RotateX + axis ? Hover : AxisColors[ axis ] );
		}

		if ( turns )
			Ring( overlay, frame.Pivot, camera.Rotation.Forward, length * ViewRing, hovered == Handle.RotateView ? Hover : Free );

		if ( moves )
			Ring( overlay, frame.Pivot, camera.Rotation.Forward, length * ViewRing * 0.15f, hovered == Handle.MoveView ? Hover : Free );
	}

	/// <summary>Adds a key to what is typed during an operation: digits, a point and a minus. True when it took the key.</summary>
	internal static bool Type( Operation operation, string key )
	{
		var character = key switch
		{
			"." or "kp_del" or "pad_decimal" or "kp_decimal" or "pad_period" => ".",
			"-" or "kp_minus" or "pad_minus" => "-",
			_ when key.Length == 1 && char.IsDigit( key[ 0 ] ) => key,
			_ when key.StartsWith( "kp_" ) && key.Length == 4 && char.IsDigit( key[ 3 ] ) => key[ 3.. ],
			_ when key.StartsWith( "pad_" ) && key.Length == 5 && char.IsDigit( key[ 4 ] ) => key[ 4.. ],
			_ => null,
		};

		if ( key == "backspace" )
		{
			if ( operation.Typed.Length > 0 )
				operation.Typed = operation.Typed[ ..^1 ];

			return true;
		}

		if ( character is null )
			return false;

		// A minus flips the sign, as in Blender, wherever it is typed
		operation.Typed = character == "-"
			? operation.Typed.StartsWith( '-' ) ? operation.Typed[ 1.. ] : "-" + operation.Typed
			: operation.Typed + character;

		return true;
	}

	private static bool Typed( Operation operation, out float number )
	{
		number = 0f;
		return operation.Typed.Length > 0 && operation.Typed != "-"
			&& float.TryParse( operation.Typed, NumberStyles.Float, CultureInfo.InvariantCulture, out number );
	}

	private static Vector2 Pixels( Vector3 projected, Vector2 view ) => new( projected.x * view.x, projected.y * view.y );

	private static float Round( float value, float step ) => MathF.Round( value / step ) * step;

	private static float ToSegment( Vector2 point, Vector2 from, Vector2 to )
	{
		var along = to - from;
		var t = along.LengthSquared < 0.001f ? 0f : Math.Clamp( Vector2.Dot( point - from, along ) / along.LengthSquared, 0f, 1f );
		return (point - (from + along * t)).Length;
	}

	private static float ToRing( Vector2 mouse, Vector3 pivot, Vector3 axis, float radius, WindowCamera camera, Vector2 view )
	{
		var aspect = view.x / view.y;
		var (u, v) = Across( axis );
		var nearest = float.MaxValue;
		var previous = Pixels( camera.Project( pivot + u * radius, aspect ), view );

		for ( var i = 1; i <= RingSegments; i++ )
		{
			var angle = i * MathF.Tau / RingSegments;
			var next = Pixels( camera.Project( pivot + (u * MathF.Cos( angle ) + v * MathF.Sin( angle )) * radius, aspect ), view );
			nearest = MathF.Min( nearest, ToSegment( mouse, previous, next ) );
			previous = next;
		}

		return nearest;
	}

	private static void Ring( DebugOverlaySystem overlay, Vector3 pivot, Vector3 axis, float radius, Color color )
	{
		var (u, v) = Across( axis );
		var previous = pivot + u * radius;
		for ( var i = 1; i <= RingSegments; i++ )
		{
			var angle = i * MathF.Tau / RingSegments;
			var next = pivot + (u * MathF.Cos( angle ) + v * MathF.Sin( angle )) * radius;
			overlay.Line( previous, next, color, 0f, global::Transform.Zero, true );
			previous = next;
		}
	}

	// Two directions square to the axis and to each other: the plane its ring lies in
	private static (Vector3 U, Vector3 V) Across( Vector3 axis )
	{
		var other = MathF.Abs( axis.z ) < 0.9f ? Vector3.Up : Vector3.Forward;
		var u = Vector3.Cross( axis, other ).Normal;
		return (u, Vector3.Cross( axis, u ).Normal);
	}
}
