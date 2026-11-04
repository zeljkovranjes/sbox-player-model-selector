using System;
using System.Collections.Generic;
using PlayerModels.Core;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// The emblem creator's canvas (see <see cref="EmblemEditor"/>): the emblem drawn as bodies show it (see
/// <see cref="EmblemRaster"/>, the same pixels), its moving layers moving, and over it the boxes of the layers selected
/// with their handles (corners and sides to scale, a knob above to turn), the centre guides while a layer snaps to the
/// middle, the stroke being drawn, and the eraser's circle at the cursor. It only shows and reports: where the mouse is
/// pressed, dragged, let go and wheeled on the emblem's square (-1 to 1, y up), and the keys pressed while it has the
/// focus; the editor decides what they do.
/// </summary>
public sealed class EmblemCanvas : Panel
{
	/// <summary>Which handle of a layer's box is where (see <see cref="HandlesOf"/>): four corners, four sides, the knob.</summary>
	public const int Corners = 4;
	public const int Sides = 4;
	public const int Knob = 8;
	public const int HandleCount = 9;

	// How far above its box a layer's turning knob sits, and how near a handle the mouse picks it, on the square
	private const float KnobReach = 0.14f;
	internal const float HandleReach = 0.045f;

	// How the overlay is drawn, in pixels of the picture
	private const int HandlePixels = 3;
	private const int KnobPixels = 4;
	private static readonly Color32 BoxColour = new( 80, 170, 255 );
	private static readonly Color32 GuideColour = new( 255, 64, 200 );
	private static readonly Color32 CursorColour = new( 255, 255, 255 );

	/// <summary>The emblem shown; null for an empty canvas.</summary>
	[Parameter] public DrawnShape Emblem { get; set; }

	/// <summary>The layers selected, by their place in the stack: their boxes show, the last one's with handles.</summary>
	[Parameter] public IReadOnlyList<int> Selected { get; set; }

	/// <summary>The centre guides showing: 1 the up-and-down one, 2 the across one.</summary>
	[Parameter] public int Guides { get; set; }

	/// <summary>How wide the eraser is (its circle shows at the cursor); 0 for no eraser.</summary>
	[Parameter] public float Eraser { get; set; }

	/// <summary>The stroke being drawn, x, y pairs on the square; null for none.</summary>
	[Parameter] public IReadOnlyList<float> Pending { get; set; }

	/// <summary>Called with where the mouse was pressed, and whether it was the right button.</summary>
	[Parameter] public Action<Vector2, bool> Pressed { get; set; }

	/// <summary>Called with where the mouse is as it is dragged, and where it was let go.</summary>
	[Parameter] public Action<Vector2> Dragged { get; set; }
	[Parameter] public Action<Vector2> Released { get; set; }

	/// <summary>Called with how far the wheel turned over the canvas (up is negative).</summary>
	[Parameter] public Action<float> Wheeled { get; set; }

	/// <summary>Called with each key pressed while the canvas has the focus; true when it was used.</summary>
	[Parameter] public Func<ButtonEvent, bool> Keyed { get; set; }

	private readonly EmblemRaster.Workspace _room = new();
	private Texture _texture;
	private byte[] _shown = Array.Empty<byte>();
	private DrawnShape _drawn;
	private EmblemRaster.Moving _moving;
	private bool _dragging;
	private Vector2? _mouse;
	private int _overlay;

	public EmblemCanvas()
	{
		AddClass( "shape-canvas" );
		AcceptsFocus = true;
	}

	/// <summary>
	/// Where the handles of <paramref name="layer"/>'s box are on the emblem's square: its corners (bottom left, bottom
	/// right, top right, top left), the middles of its sides (bottom, right, top, left), and the knob above it.
	/// </summary>
	public static Vector2[] HandlesOf( DrawnShape emblem, EmblemLayer layer )
	{
		var (minX, minY, maxX, maxY) = Core.Emblem.LocalBounds( emblem, layer );
		var (midX, midY) = ((minX + maxX) / 2f, (minY + maxY) / 2f);
		var local = new (float, float)[] { (minX, minY), (maxX, minY), (maxX, maxY), (minX, maxY), (midX, minY), (maxX, midY), (midX, maxY), (minX, midY) };
		var handles = new Vector2[ HandleCount ];
		for ( var i = 0; i < local.Length; i++ )
		{
			var (x, y) = layer.Place( local[ i ].Item1, local[ i ].Item2 );
			handles[ i ] = new Vector2( x, y );
		}

		var (cx, cy) = layer.Place( midX, midY );
		var up = handles[ 6 ] - new Vector2( cx, cy );
		handles[ Knob ] = handles[ 6 ] + (up.Length > 0.0001f ? up.Normal : new Vector2( 0f, 1f )) * KnobReach;
		return handles;
	}

	public override void Tick()
	{
		base.Tick();

		if ( _dragging && !HasActive )
			Finish( _mouse ?? Vector2.Zero );

		if ( !ReferenceEquals( Emblem, _drawn ) )
		{
			_drawn = Emblem;
			_moving = null;
			if ( Emblem?.IsEmblem == true )
				EmblemRaster.Rgba( Emblem, EmblemRaster.Size, _room, out _moving );

			_overlay = 0;
		}

		var overlay = HashCode.Combine( _drawn, Selected?.Count > 0 ? string.Join( ",", Selected ) : "", Guides, Eraser, _mouse, Pending?.Count );
		if ( overlay == _overlay && _moving is null && _texture is not null )
			return;

		_overlay = overlay;
		Draw();
	}

	private void Draw()
	{
		var size = EmblemRaster.Size;
		if ( _shown.Length != size * size * 4 )
			_shown = new byte[ size * size * 4 ];

		if ( _drawn?.IsEmblem == true )
		{
			if ( _moving is not null )
				EmblemRaster.Animate( _moving, (long)(RealTime.Now * 1000f), _room, size );

			Array.Copy( _room.Pixels, _shown, _shown.Length );
		}
		else
		{
			Array.Clear( _shown );
		}

		DrawOverlay( size );
		if ( _texture is null )
		{
			_texture = Texture.Create( size, size ).WithName( "emblem_canvas" ).WithDynamicUsage().WithData( _shown ).Finish();
			Style.BackgroundImage = _texture;
		}
		else
		{
			_texture.Update( _shown );
		}
	}

	private void DrawOverlay( int size )
	{
		if ( (Guides & 1) != 0 )
			Line( new Vector2( 0f, -1f ), new Vector2( 0f, 1f ), GuideColour, size );

		if ( (Guides & 2) != 0 )
			Line( new Vector2( -1f, 0f ), new Vector2( 1f, 0f ), GuideColour, size );

		var layers = _drawn?.Emblem;
		for ( var s = 0; s < (Selected?.Count ?? 0) && layers is not null; s++ )
		{
			var index = Selected[ s ];
			if ( index < 0 || index >= layers.Count )
				continue;

			var handles = HandlesOf( _drawn, layers[ index ] );
			for ( var i = 0; i < Corners; i++ )
				Line( handles[ i ], handles[ (i + 1) % Corners ], BoxColour, size );

			// Handles on a layer selected alone; several are scaled and turned with the wheel
			if ( Selected.Count != 1 )
				continue;

			for ( var i = 0; i < Corners + Sides; i++ )
				Square( handles[ i ], HandlePixels, size );

			Line( handles[ 6 ], handles[ Knob ], BoxColour, size );
			Circle( handles[ Knob ], KnobPixels * 2f / size, BoxColour, size );
		}

		if ( Pending is { Count: >= 2 } pending )
		{
			for ( var i = 2; i + 1 < pending.Count; i += 2 )
				Line( new Vector2( pending[ i - 2 ], pending[ i - 1 ] ), new Vector2( pending[ i ], pending[ i + 1 ] ), CursorColour, size );
		}

		if ( Eraser > 0f && _mouse is { } mouse )
			Circle( mouse, Eraser / 2f, CursorColour, size );
	}

	// A point on the square as a pixel of the picture
	private static (int Column, int Row) Pixel( Vector2 at, int size ) => ((int)MathF.Floor( (at.x + 1f) / 2f * size ), (int)MathF.Floor( (1f - at.y) / 2f * size ));

	private void Put( int column, int row, Color32 colour, int size )
	{
		if ( column < 0 || row < 0 || column >= size || row >= size )
			return;

		var at = (row * size + column) * 4;
		(_shown[ at ], _shown[ at + 1 ], _shown[ at + 2 ], _shown[ at + 3 ]) = (colour.r, colour.g, colour.b, 255);
	}

	private void Line( Vector2 from, Vector2 to, Color32 colour, int size )
	{
		var (ax, ay) = Pixel( from, size );
		var (bx, by) = Pixel( to, size );
		var steps = Math.Max( 1, Math.Max( Math.Abs( bx - ax ), Math.Abs( by - ay ) ) );
		for ( var i = 0; i <= steps; i++ )
			Put( ax + (bx - ax) * i / steps, ay + (by - ay) * i / steps, colour, size );
	}

	private void Square( Vector2 at, int half, int size )
	{
		var (column, row) = Pixel( at, size );
		for ( var y = -half; y <= half; y++ )
		{
			for ( var x = -half; x <= half; x++ )
				Put( column + x, row + y, Math.Abs( x ) == half || Math.Abs( y ) == half ? BoxColour : CursorColour, size );
		}
	}

	private void Circle( Vector2 at, float radius, Color32 colour, int size )
	{
		var steps = Math.Clamp( (int)(radius * size * 4f), 16, 256 );
		var last = at + new Vector2( radius, 0f );
		for ( var i = 1; i <= steps; i++ )
		{
			var angle = i * MathF.Tau / steps;
			var next = at + new Vector2( MathF.Cos( angle ), MathF.Sin( angle ) ) * radius;
			Line( last, next, colour, size );
			last = next;
		}
	}

	// Where the mouse is (from the canvas's top left corner, in pixels) on the emblem's square
	private Vector2 OnSquare( Vector2 mouse )
	{
		var size = Box.Rect.Size;
		return size.x <= 0f || size.y <= 0f ? Vector2.Zero : new Vector2( mouse.x / size.x * 2f - 1f, 1f - mouse.y / size.y * 2f );
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		if ( e.MouseButton is not (MouseButtons.Left or MouseButtons.Right) )
			return;

		// The keys come here while the canvas was used last, until a box is clicked
		Focus();
		var at = OnSquare( e.LocalPosition );
		_mouse = at;
		_dragging = e.MouseButton == MouseButtons.Left;
		Pressed?.Invoke( at, e.MouseButton == MouseButtons.Right );
		e.StopPropagation();
	}

	protected override void OnMouseMove( MousePanelEvent e )
	{
		base.OnMouseMove( e );
		_mouse = OnSquare( e.LocalPosition );
		if ( _dragging )
			Dragged?.Invoke( _mouse.Value );
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );
		if ( _dragging )
			Finish( OnSquare( e.LocalPosition ) );
	}

	protected override void OnMouseOut( MousePanelEvent e )
	{
		base.OnMouseOut( e );
		if ( !_dragging )
			_mouse = null;
	}

	private void Finish( Vector2 at )
	{
		_dragging = false;
		Released?.Invoke( at );
	}

	// Not passed on: the wheel over the canvas scales the layers selected, it doesn't scroll the panel
	public override void OnMouseWheel( Vector2 value )
	{
		Wheeled?.Invoke( value.y );
	}

	public override void OnButtonEvent( ButtonEvent e )
	{
		if ( e.Pressed && Keyed?.Invoke( e ) == true )
		{
			e.StopPropagation = true;
			return;
		}

		base.OnButtonEvent( e );
	}

	public override void OnDeleted()
	{
		_texture?.Dispose();
		_texture = null;
		base.OnDeleted();
	}
}
