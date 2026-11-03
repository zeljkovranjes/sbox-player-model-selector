using System;
using System.Collections.Generic;
using System.Text.Json;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// The Advanced editor's view, worked the way Blender's viewport is. Middle-drag orbits, Shift+middle pans, Ctrl+middle
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

	private readonly ModelPreview _stage;
	private readonly Func<PlayerLook> _look;
	private readonly Action _changed;
	private readonly Func<int> _selected;
	private readonly Action<int> _select;

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
	/// <param name="look">The look being edited now.</param>
	/// <param name="changed">Told after the look's items change.</param>
	/// <param name="selected">The item selected, by its place in the look; -1 for none.</param>
	/// <param name="select">Selects an item, or none with -1.</param>
	public EditorViewport( ModelPreview stage, Func<PlayerLook> look, Action changed, Func<int> selected, Action<int> select )
	{
		_stage = stage;
		_look = look;
		_changed = changed;
		_selected = selected;
		_select = select;
	}

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
				_select( ItemAt( _stage.MousePosition ) );
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
			_select( -1 );
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
		var rack = _stage.Body?.Accessories;
		if ( rack is not null && rack.TryGetPlacement( _selected(), out var placement ) && placement.Renderer.IsValid() )
		{
			var bounds = placement.Renderer.Bounds;
			View.Frame( bounds.Center, MathF.Max( bounds.Size.Length * 0.5f, 2f ) );
		}
		else
		{
			FrameAll();
		}
	}

	private void Start( TransformGizmo.Kind kind )
	{
		if ( !TryFrame( out _ ) || !TryPose( out var pose ) )
		{
			Status = "Select an item first: click it, or pick it on the Accessories tab.";
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

	// Alt+G, Alt+R, Alt+S: back to where the joint holds it, unturned, at the size it starts at
	private void Clear( string key )
	{
		if ( Selected is not { } accessory )
			return;

		Remember( Snapshot() );
		if ( key == "g" )
			accessory.Forward = accessory.Left = accessory.Up = 0f;
		else if ( key == "r" )
			accessory.Pitch = accessory.Yaw = accessory.Roll = 0f;
		else
			accessory.Scale = 1f;

		Clamp( accessory );
		_changed?.Invoke();
	}

	private void Duplicate()
	{
		var look = _look();
		if ( Selected is not { } accessory || look.Accessories.Count >= Accessory.MaxWorn )
			return;

		Remember( Snapshot() );
		look.Accessories.Add( JsonSerializer.Deserialize<Accessory>( JsonSerializer.Serialize( accessory ) ) );
		_changed?.Invoke();
		_select( look.Accessories.Count - 1 );
	}

	private void Remove()
	{
		var look = _look();
		var index = _selected();
		if ( index < 0 || index >= look.Accessories.Count )
			return;

		Remember( Snapshot() );
		look.Accessories.RemoveAt( index );
		_select( -1 );
		_changed?.Invoke();
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

	private string Snapshot() => JsonSerializer.Serialize( _look()?.Accessories ?? new List<Accessory>() );

	private void Restore( string snapshot )
	{
		var look = _look();
		if ( look is null )
			return;

		look.Accessories = JsonSerializer.Deserialize<List<Accessory>>( snapshot ) ?? new List<Accessory>();
		if ( _selected() >= look.Accessories.Count )
			_select( -1 );

		_changed?.Invoke();
	}

	private Accessory Selected
	{
		get
		{
			var look = _look();
			var index = _selected();
			return look is not null && index >= 0 && index < look.Accessories.Count ? look.Accessories[ index ] : null;
		}
	}

	private bool TryPose( out TransformGizmo.Pose pose )
	{
		pose = default;
		if ( Selected is not { } accessory )
			return false;

		pose = new TransformGizmo.Pose( new Vector3( accessory.Forward, accessory.Left, accessory.Up ),
			Rotation.From( accessory.Pitch, accessory.Yaw, accessory.Roll ), accessory.Scale );
		return true;
	}

	// Where the selected item's joint is now, for the gizmo; none while its item is loading
	private bool TryFrame( out TransformGizmo.Frame frame )
	{
		frame = default;
		var rack = _stage.Body?.Accessories;
		if ( rack is null || Selected is null || !rack.TryGetPlacement( _selected(), out var placement ) )
			return false;

		frame = new TransformGizmo.Frame( placement.Pivot, placement.Heading, placement.Scale );
		return true;
	}

	private void Write( in TransformGizmo.Pose pose )
	{
		if ( Selected is not { } accessory )
			return;

		accessory.Forward = pose.Offset.x;
		accessory.Left = pose.Offset.y;
		accessory.Up = pose.Offset.z;

		var angles = pose.Turn.Angles();
		accessory.Pitch = angles.pitch;
		accessory.Yaw = angles.yaw;
		accessory.Roll = angles.roll;
		accessory.Scale = pose.Size;

		Clamp( accessory );
		_changed?.Invoke();
	}

	// Within what every client lets the item do on this model, as the sliders are
	private void Clamp( Accessory accessory )
	{
		var rack = _stage.Body?.Accessories;
		var body = _stage.Body?.Renderer?.Model;
		if ( rack is null || !rack.TryGetPlacement( _selected(), out var placement ) )
			return;

		var fitted = accessory.FittedTo( AccessoryCatalog.HeightOf( body ), placement.Size, placement.Follows );
		accessory.Forward = fitted.Forward;
		accessory.Left = fitted.Left;
		accessory.Up = fitted.Up;
		accessory.Pitch = fitted.Pitch;
		accessory.Yaw = fitted.Yaw;
		accessory.Roll = fitted.Roll;
		accessory.Scale = fitted.Scale;
	}

	// The item under the cursor, the nearest one hit; -1 for none
	private int ItemAt( Vector2 mouse )
	{
		var rack = _stage.Body?.Accessories;
		var size = Size;
		if ( rack is null || size.y <= 0f )
			return -1;

		var ray = View.RayThrough( new Vector2( mouse.x / size.x, mouse.y / size.y ), size.x / size.y );
		var nearest = float.MaxValue;
		var found = -1;
		for ( var i = 0; i < rack.Count; i++ )
		{
			if ( !rack.TryGetPlacement( i, out var placement ) || !placement.Renderer.IsValid() )
				continue;

			if ( placement.Renderer.Bounds.Trace( ray, 10000f, out var distance ) && distance < nearest )
			{
				nearest = distance;
				found = i;
			}
		}

		return found;
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
