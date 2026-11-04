using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// The Advanced editor's view, worked the way Blender's viewport is, over what its gizmo moves (see
/// <see cref="IGizmoTargets"/>: the items worn, or a weapon's props and stickers). Middle-drag orbits, Shift+middle pans, Ctrl+middle
/// and the wheel zoom (Alt+left orbits, for a mouse without a middle button); the numpad gives views and steps, Home
/// frames the body and numpad . the item. Clicking an item selects it; its gizmo moves, turns and sizes it, and so do
/// G, R and S, with X/Y/Z (Shift for a plane), typed numbers, Ctrl to snap and Shift for precision, left click or Enter to
/// keep and right click or Esc to cancel. Alt+G/R/S clear, Shift+D duplicates, Delete removes, Ctrl+Z and Ctrl+Shift+Z
/// undo and redo, and Shift+` walks (WASD, Q and E).
/// </summary>
internal sealed class EditorViewport
{
	private const int MostUndo = 50;
	private const float ViewStep = 15f;
	private const float ClickPixels = 4f;

	// Two clicks this close together (seconds) are a double click
	private const float DoubleClickSeconds = 0.4f;

	private readonly ModelPreview _stage;
	private readonly Func<IGizmoTargets> _targets;
	private RealTimeSince _sinceClick = 10f;

	private enum Navigation { None, Orbit, Pan, Zoom, Look }

	private Navigation _navigation;
	private Vector2 _lastMouse;
	private Vector2 _pressedAt;
	private bool _picking;
	private TransformGizmo.Handle _hovered;
	private TransformGizmo.Operation _operation;
	private string _before;
	private readonly List<string> _undo = new();
	private readonly List<string> _redo = new();

	/// <param name="stage">The preview filling the window.</param>
	/// <param name="targets">What the gizmo moves now (see <see cref="IGizmoTargets"/>).</param>
	public EditorViewport( ModelPreview stage, Func<IGizmoTargets> targets )
	{
		_stage = stage;
		_targets = targets;
	}

	private IGizmoTargets Targets => _targets();

	/// <summary>Called with the ray under the cursor when the view is double clicked (into and out of weapon mode).</summary>
	public Action<Ray> DoubleClicked { get; set; }

	/// <summary>Called with the ray under the cursor on a right click that moved nothing (a context menu).</summary>
	public Action<Ray> RightClicked { get; set; }

	/// <summary>The ray under the cursor now, for what is hovered; null off the view.</summary>
	public Ray? Hovered { get; private set; }

	/// <summary>Which of the gizmo's handles show.</summary>
	public TransformGizmo.Tool Tool { get; set; } = TransformGizmo.Tool.Transform;

	/// <summary>Whether WASD flies the view, and dragging looks around (Shift+`).</summary>
	public bool Walking { get; set; }

	/// <summary>What an operation under way is doing, or what last went wrong; empty otherwise.</summary>
	public string Status { get; private set; } = "";

	private WindowCamera View => _stage.View;

	private Vector2 Size => _stage.Box.Rect.Size;

	/// <summary>Moves the view and the operation under way with the mouse, and draws the gizmo. Every frame.</summary>
	public void Tick()
	{
		var mouse = _stage.MousePosition;
		var size = Size;
		if ( size.x <= 0f || size.y <= 0f )
			return;

		Walk();
		Navigate( mouse, size );
		Hovered = mouse.x >= 0f && mouse.y >= 0f && mouse.x <= size.x && mouse.y <= size.y ? RayAt( mouse ) : null;

		if ( !TryFrame( out var frame ) )
		{
			if ( _operation is not null )
				Cancel();

			return;
		}

		if ( _operation is not null )
		{
			var pose = TransformGizmo.Apply( _operation, frame, View, size, mouse, Held( "ctrl" ), Held( "shift" ) );
			Write( pose );
			Status = TransformGizmo.Describe( _operation, _operation.Start, pose );
		}
		else
		{
			_hovered = _navigation == Navigation.None ? TransformGizmo.HandleAt( Tool, frame, View, size, mouse ) : TransformGizmo.Handle.None;
		}

		TransformGizmo.Draw( _stage.Overlay, Tool, frame, View, _hovered, _operation );
	}

	/// <summary>Handles a mouse button going down on the view. True when it was used.</summary>
	public bool MouseDown( MouseButtons button )
	{
		var mouse = _stage.MousePosition;
		_lastMouse = mouse;

		if ( _operation is not null )
		{
			if ( button == MouseButtons.Left )
				Confirm();
			else if ( button == MouseButtons.Right )
				Cancel();

			return true;
		}

		if ( button == MouseButtons.Right && RightClicked is not null )
		{
			RightClicked( RayAt( mouse ) );
			return true;
		}

		if ( button == MouseButtons.Middle )
		{
			_navigation = Held( "shift" ) ? Navigation.Pan : Held( "ctrl" ) ? Navigation.Zoom : Navigation.Orbit;
			return true;
		}

		if ( button != MouseButtons.Left )
			return false;

		if ( Held( "alt" ) )
		{
			_navigation = Navigation.Orbit;
			return true;
		}

		if ( Walking )
		{
			_navigation = Navigation.Look;
			return true;
		}

		if ( _hovered != TransformGizmo.Handle.None && TryFrame( out _ ) && TryPose( out var pose ) )
		{
			Begin( TransformGizmo.Grab( _hovered, mouse, pose ) );
			return true;
		}

		_picking = true;
		_pressedAt = mouse;
		return true;
	}

	/// <summary>Handles a mouse button coming up.</summary>
	public void MouseUp( MouseButtons button )
	{
		if ( button == MouseButtons.Left && _operation is { FromHandle: true } )
			Confirm();

		if ( button == MouseButtons.Left && _picking )
		{
			_picking = false;
			if ( (_stage.MousePosition - _pressedAt).Length <= ClickPixels )
			{
				var ray = RayAt( _stage.MousePosition );
				if ( _sinceClick < DoubleClickSeconds && DoubleClicked is not null )
				{
					_sinceClick = 10f;
					DoubleClicked( ray );
				}
				else
				{
					_sinceClick = 0f;
					Targets.Select( Targets.At( ray ) );
				}
			}
		}

		_navigation = Navigation.None;
	}

	/// <summary>The wheel zooms.</summary>
	public void Wheel( float y )
	{
		View.Zoom( -y );
	}

	/// <summary>Handles a key while the view has focus. True when it was used, so nothing else gets it.</summary>
	public bool Key( ButtonEvent e )
	{
		if ( !e.Pressed )
			return false;

		var key = e.Button.ToLowerInvariant();
		return _operation is not null ? KeyDuringOperation( key, e ) : KeyAtRest( key, e );
	}

	/// <summary>Frames <paramref name="bounds"/>, seen from where the view is now.</summary>
	public void Frame( BBox bounds )
	{
		View.Frame( bounds.Center, MathF.Max( bounds.Size.Length * 0.5f, 2f ) );
	}

	/// <summary>Puts the view back on the whole body from the front.</summary>
	public void FrameAll()
	{
		if ( _stage.Body?.Renderer is { } body && body.IsValid() )
		{
			var bounds = body.Bounds;
			View.Frame( bounds.Center, bounds.Size.Length * 0.5f );
		}
		else
		{
			View.Reset();
		}
	}

	private bool KeyDuringOperation( string key, ButtonEvent e )
	{
		switch ( key )
		{
			case "x" or "y" or "z":
				var axis = key[ 0 ] - 'x';
				var plane = e.HasShift && _operation.Kind == TransformGizmo.Kind.Move;
				var same = _operation.Axis == axis && _operation.Plane == plane;
				_operation.Axis = same ? -1 : axis;
				_operation.Plane = !same && plane;
				return true;

			case "enter" or "pad_enter" or "kp_enter" or "space":
				Confirm();
				return true;

			case "escape":
				Cancel();
				return true;

			default:
				return TransformGizmo.Type( _operation, key );
		}
	}

	private bool KeyAtRest( string key, ButtonEvent e )
	{
		if ( e.HasCtrl && key == "z" )
		{
			if ( e.HasShift )
				Redo();
			else
				Undo();

			return true;
		}

		if ( e.HasCtrl && key == "y" )
		{
			Redo();
			return true;
		}

		if ( e.HasShift && key == "`" )
		{
			Walking = !Walking;
			return true;
		}

		if ( key is "home" )
		{
			FrameAll();
			return true;
		}

		if ( NumpadView( key, e.HasCtrl ) )
			return true;

		if ( key is "g" or "r" or "s" && !e.HasCtrl )
		{
			if ( e.HasAlt )
				Clear( key );
			else
				Start( key switch { "g" => TransformGizmo.Kind.Move, "r" => TransformGizmo.Kind.Rotate, _ => TransformGizmo.Kind.Scale } );

			return true;
		}

		if ( e.HasShift && key == "d" )
		{
			Duplicate();
			return true;
		}

		if ( key is "delete" or "del" )
		{
			Remove();
			return true;
		}

		if ( e.HasAlt && key == "a" )
		{
			Targets.Select( -1 );
			return true;
		}

		return false;
	}

	// The numpad's views, Blender's: 1 front, 3 right, 7 top (Ctrl for the other side), 2/4/6/8 steps, +/- zoom, . frames
	private bool NumpadView( string key, bool other )
	{
		var pad = key.StartsWith( "kp_" ) ? key[ 3.. ] : key.StartsWith( "pad_" ) ? key[ 4.. ] : null;
		switch ( pad )
		{
			case "1": View.View( other ? 0f : 180f, 0f ); return true;
			case "3": View.View( other ? -90f : 90f, 0f ); return true;
			case "7": View.View( 180f, other ? -89.5f : 89.5f ); return true;
			case "4": View.Step( ViewStep, 0f ); return true;
			case "6": View.Step( -ViewStep, 0f ); return true;
			case "8": View.Step( 0f, -ViewStep ); return true;
			case "2": View.Step( 0f, ViewStep ); return true;
			case "plus": View.Zoom( 1f ); return true;
			case "minus": View.Zoom( -1f ); return true;
			case "del" or "decimal" or "period":
				FrameSelected();
				return true;
			default:
				return false;
		}
	}

	private void FrameSelected()
	{
		if ( Targets.TryBounds( out var bounds ) )
			Frame( bounds );
		else
			FrameAll();
	}

	private void Start( TransformGizmo.Kind kind )
	{
		if ( !TryFrame( out _ ) || !TryPose( out var pose ) )
		{
			Status = Targets.NothingSelected;
			return;
		}

		Begin( new TransformGizmo.Operation { Kind = kind, StartMouse = _stage.MousePosition, Start = pose } );
	}

	private void Begin( TransformGizmo.Operation operation )
	{
		_operation = operation;
		_before = Snapshot();
		_hovered = TransformGizmo.Handle.None;
	}

	private void Confirm()
	{
		if ( _operation is null )
			return;

		_operation = null;
		Remember( _before );
		Status = "";
	}

	private void Cancel()
	{
		if ( _operation is null )
			return;

		Write( _operation.Start );
		_operation = null;
		Status = "";
	}

	// Alt+G, Alt+R, Alt+S: back to where it starts, unturned, at the size it starts at
	private void Clear( string key )
	{
		if ( !TryPose( out _ ) )
			return;

		Remember( Snapshot() );
		Targets.Clear( key );
	}

	private void Duplicate()
	{
		if ( !TryPose( out _ ) )
			return;

		Remember( Snapshot() );
		Targets.Duplicate();
	}

	private void Remove()
	{
		if ( !TryPose( out _ ) )
			return;

		Remember( Snapshot() );
		Targets.Remove();
	}

	private void Undo() => Step( _undo, _redo );

	private void Redo() => Step( _redo, _undo );

	private void Step( List<string> from, List<string> to )
	{
		if ( from.Count == 0 )
			return;

		to.Add( Snapshot() );
		Restore( from[ ^1 ] );
		from.RemoveAt( from.Count - 1 );
	}

	private void Remember( string before )
	{
		if ( before is null )
			return;

		_undo.Add( before );
		if ( _undo.Count > MostUndo )
			_undo.RemoveAt( 0 );

		_redo.Clear();
	}

	private string Snapshot() => Targets.Snapshot();

	private void Restore( string snapshot ) => Targets.Restore( snapshot );

	private bool TryPose( out TransformGizmo.Pose pose ) => Targets.TryPose( out pose );

	private bool TryFrame( out TransformGizmo.Frame frame ) => Targets.TryFrame( out frame );

	private void Write( in TransformGizmo.Pose pose ) => Targets.Write( pose );

	private Ray RayAt( Vector2 mouse )
	{
		var size = Size;
		return View.RayThrough( new Vector2( mouse.x / MathF.Max( size.x, 1f ), mouse.y / MathF.Max( size.y, 1f ) ), size.x / MathF.Max( size.y, 1f ) );
	}

	private void Navigate( Vector2 mouse, Vector2 size )
	{
		var drag = mouse - _lastMouse;
		_lastMouse = mouse;

		switch ( _navigation )
		{
			case Navigation.Orbit: View.Orbit( drag.x, drag.y ); break;
			case Navigation.Pan: View.Pan( drag.x, drag.y, size.y ); break;
			case Navigation.Zoom: View.ZoomByDrag( drag.y ); break;
			case Navigation.Look: View.Look( drag.x, drag.y ); break;
		}
	}

	// WASD flies while walking, Q and E down and up, Shift for speed; not while typing in a box
	private void Walk()
	{
		if ( !Walking || InputFocus.Current is TextEntry )
			return;

		var move = new Vector3(
			(Held( "w" ) ? 1f : 0f) - (Held( "s" ) ? 1f : 0f),
			(Held( "a" ) ? 1f : 0f) - (Held( "d" ) ? 1f : 0f),
			(Held( "e" ) ? 1f : 0f) - (Held( "q" ) ? 1f : 0f) );

		View.Fly( move, RealTime.Delta, Held( "shift" ) );
	}

	// A key held now. The game is kept from input while the window is up (see InputLock), and that keeps this from it too,
	// so it is lifted for the read
	private static bool Held( string key )
	{
		var suppressed = Input.Suppressed;
		Input.Suppressed = false;
		var held = Input.Keyboard.Down( key );
		Input.Suppressed = suppressed;
		return held;
	}
}
