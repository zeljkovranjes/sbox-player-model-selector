using System;
using System.Collections.Generic;
using PlayerModels.Core;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// The shape creator's drawing area: the shape so far drawn as it will look (see <see cref="ShapeRaster"/>), and the left
/// mouse button drawing a new stroke on it, tidied when the button is let go (see <see cref="ShapeStroke.Freehand"/>); with
/// the eraser, a stroke that cuts away what is under it, its size shown as a circle at the cursor ([ and ] change it while
/// the canvas has the focus). The square it shows is the shape's square, -1 to 1 each way.
/// </summary>
public sealed class ShapeCanvas : Panel
{
	// Drawn bigger than a sprite's texture: the canvas is a few hundred pixels across
	private const int CanvasPixels = 256;

	/// <summary>The strokes shown, as the creator has them.</summary>
	[Parameter] public IReadOnlyList<ShapeStroke> Strokes { get; set; }

	/// <summary>How a sticker colours them; null shows them plain white.</summary>
	[Parameter] public Sticker Sticker { get; set; }

	/// <summary>Whether new strokes close round on themselves, to be filled.</summary>
	[Parameter] public bool Closed { get; set; }

	/// <summary>Called with each stroke drawn, once it is tidied.</summary>
	[Parameter] public Action<ShapeStroke> Drawn { get; set; }

	/// <summary>Whether strokes are the eraser's, and how wide it is (on the square, 2 across).</summary>
	[Parameter] public bool Erasing { get; set; }
	[Parameter] public float EraserSize { get; set; }

	/// <summary>Called with the eraser's new size when [ or ] is pressed.</summary>
	[Parameter] public Action<float> Resized { get; set; }

	// How much [ and ] change the eraser's size
	private const float EraserStep = 0.02f;

	private Panel _cursor;

	private readonly List<float> _drawing = new();
	private readonly ShapeRaster.Canvas _scratch = new();
	private bool _isDrawing;
	private Texture _texture;
	private IReadOnlyList<ShapeStroke> _shownStrokes;
	private Sticker _shownSticker;
	private int _shownPoints = -1;

	public ShapeCanvas()
	{
		AddClass( "shape-canvas" );
		AcceptsFocus = true;
		_cursor = AddChild<Panel>( "eraser-cursor" );
	}

	public override void Tick()
	{
		base.Tick();

		// Let go anywhere ends the stroke: the canvas stays active while the button is held, wherever the mouse goes
		if ( _isDrawing && !HasActive )
			Finish();

		// An animated sticker's fill is drawn white, for its colour to move over it as on a body
		Style.BackgroundTint = Sticker is { IsAnimated: true } sticker ? ShapeIcon.FillAt( sticker, RealTime.Now ) : Color.White;

		PlaceCursor();

		// Drawn again only when what shows changed: the strokes, the sticker, or the stroke being drawn
		var points = _isDrawing ? _drawing.Count : -1;
		if ( ReferenceEquals( Strokes, _shownStrokes ) && ReferenceEquals( Sticker, _shownSticker ) && points == _shownPoints && _texture is not null )
			return;

		_shownStrokes = Strokes;
		_shownSticker = Sticker;
		_shownPoints = points;
		DrawPicture();
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		if ( e.MouseButton != MouseButtons.Left )
			return;

		// The keys ([ and ]) come here while the canvas was used last
		Focus();
		_isDrawing = true;
		_drawing.Clear();
		AddPoint( e.LocalPosition );
		e.StopPropagation();
	}

	public override void OnButtonEvent( ButtonEvent e )
	{
		if ( e.Pressed && e.Button is "[" or "]" )
		{
			Resized?.Invoke( EraserSize + (e.Button == "]" ? EraserStep : -EraserStep) );
			e.StopPropagation = true;
			return;
		}

		base.OnButtonEvent( e );
	}

	// The eraser's circle, as wide as it cuts, at the cursor while it is over the canvas
	private void PlaceCursor()
	{
		var size = Box.Rect.Size;
		var shown = Erasing && HasHovered && size.x > 0f;
		_cursor.Style.Display = shown ? DisplayMode.Flex : DisplayMode.None;
		if ( !shown )
			return;

		var scale = ScaleFromScreen;
		var diameter = EraserSize / 2f * size.x;
		var mouse = MousePosition;
		_cursor.Style.Width = Length.Pixels( diameter * scale );
		_cursor.Style.Height = Length.Pixels( diameter * scale );
		_cursor.Style.Left = Length.Pixels( (mouse.x - diameter / 2f) * scale );
		_cursor.Style.Top = Length.Pixels( (mouse.y - diameter / 2f) * scale );
	}

	protected override void OnMouseMove( MousePanelEvent e )
	{
		base.OnMouseMove( e );
		if ( _isDrawing )
			AddPoint( e.LocalPosition );
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );
		if ( _isDrawing )
			Finish();
	}

	public override void OnDeleted()
	{
		_texture?.Dispose();
		_texture = null;
		base.OnDeleted();
	}

	// Where the mouse is (from the canvas's top left corner, in pixels) on the shape's square
	private void AddPoint( Vector2 mouse )
	{
		var size = Box.Rect.Size;
		if ( size.x <= 0f || size.y <= 0f )
			return;

		_drawing.Add( Math.Clamp( mouse.x / size.x * 2f - 1f, -1f, 1f ) );
		_drawing.Add( Math.Clamp( 1f - mouse.y / size.y * 2f, -1f, 1f ) );
	}

	private void Finish()
	{
		_isDrawing = false;
		if ( _drawing.Count >= 2 && Erasing )
			Drawn?.Invoke( new ShapeStroke { Erase = true, Width = EraserSize, Points = ShapeStroke.Freehand( _drawing.ToArray(), false ).Points } );
		else if ( _drawing.Count >= 2 )
			Drawn?.Invoke( ShapeStroke.Freehand( _drawing.ToArray(), Closed ) );

		_drawing.Clear();
	}

	// The strokes and the one being drawn, as the shape will look, into the canvas's own texture
	private void DrawPicture()
	{
		var strokes = new List<ShapeStroke>( Strokes ?? Array.Empty<ShapeStroke>() );
		if ( _isDrawing && _drawing.Count >= 2 )
			strokes.Add( new ShapeStroke { Erase = Erasing, Width = Erasing ? EraserSize : 0f, Points = _drawing.ToArray() } );

		var shape = strokes.Count > 0 ? DrawnShape.From( strokes, sticker: Sticker ) : null;
		var pixels = shape is null ? new byte[ CanvasPixels * CanvasPixels * 4 ] : ShapeRaster.Rgba( shape, CanvasPixels, _scratch );
		if ( _texture is null )
		{
			_texture = Texture.Create( CanvasPixels, CanvasPixels ).WithName( "shape_canvas" ).WithDynamicUsage().WithData( pixels ).Finish();
			Style.BackgroundImage = _texture;
		}
		else
		{
			_texture.Update( pixels );
		}
	}
}
