using System;
using System.Collections.Generic;
using PlayerModels.Core.Script;

namespace PlayerModels.Core;

/// <summary>
/// An emblem (see <see cref="DrawnShape.Emblem"/>) as pixels, <see cref="Size"/> across (more than a sticker's, for its
/// detail): its layers drawn from the top of the stack down, each filled, outlined and as see-through as it says, each
/// eraser cutting away every layer below it, and the mirrored half too when it has one. One layer is drawn a step (see
/// <see cref="Job"/>), so a crowd's emblems are drawn a little a frame. Layers whose colour moves are kept apart as how
/// much of each pixel they colour (see <see cref="Moving"/>), so the picture is put together again for each moment with
/// a few sums a pixel and never drawn again; how each one's colour moves is its motion's script, as a sticker's (see
/// <see cref="MovingColour"/>). The same emblem is the same pixels on every machine.
/// </summary>
public static class EmblemRaster
{
	/// <summary>How many pixels across an emblem is drawn.</summary>
	public const int Size = 256;

	// How many pixels an edge blurs over
	private const float SoftPixels = 1f;

	// The most pixels an emblem's moving layers keep apart, as times a whole picture's: layers past that keep their own
	// colour, still
	private const int MostMovingPictures = 4;

	/// <summary>
	/// The room emblems are drawn in, kept from one to the next by whoever draws many, so drawing a crowd's makes no
	/// garbage to collect but the pictures themselves. One emblem at a time is drawn in it.
	/// </summary>
	public sealed class Workspace
	{
		internal readonly ShapeRaster.Canvas Canvas = new();
		internal float[] Through = Array.Empty<float>();
		internal float[] Red = Array.Empty<float>();
		internal float[] Green = Array.Empty<float>();
		internal float[] Blue = Array.Empty<float>();
		internal float[] Alpha = Array.Empty<float>();
		internal float[] Scratch = Array.Empty<float>();

		/// <summary>The picture last drawn here, RGBA row by row from the top: its own until the next is drawn.</summary>
		public byte[] Pixels = Array.Empty<byte>();

		/// <summary>The moving part last put together (see <see cref="Moving.Compose"/>), RGBA, its box's width a row.</summary>
		public byte[] Composed = Array.Empty<byte>();

		internal void Fit( int size )
		{
			if ( Through.Length == size * size )
				return;

			Through = new float[ size * size ];
			Red = new float[ size * size ];
			Green = new float[ size * size ];
			Blue = new float[ size * size ];
			Alpha = new float[ size * size ];
			Scratch = new float[ size * size * 3 ];
			Pixels = new byte[ size * size * 4 ];
			Composed = new byte[ size * size * 4 ];
		}
	}

	/// <summary>
	/// The part of an emblem's picture its moving layers colour: a box of it (<see cref="X"/>, <see cref="Y"/>,
	/// <see cref="Width"/> by <see cref="Height"/> pixels), what the still layers put there, and how much each moving layer
	/// colours each of its pixels.
	/// </summary>
	public sealed class Moving
	{
		public int X, Y, Width, Height;

		// What the still layers put in the box, premultiplied, and how covered each pixel is: four bytes a pixel
		internal byte[] Still;
		internal readonly List<Layer> Layers = new();

		internal sealed class Layer
		{
			public int X, Y, Width, Height;
			public byte[] Weights;

			// How its colour moves, worked out by its motion's script; null (its own colour, still) when that doesn't run
			public MovingColour Colour;
			public float Speed;
			public float Red, Green, Blue;
			public int Element, Count;
		}

		/// <summary>The bytes it keeps.</summary>
		public long Bytes
		{
			get
			{
				long bytes = Still.Length;
				foreach ( var layer in Layers )
					bytes += layer.Weights.Length;

				return bytes;
			}
		}

		/// <summary>
		/// The box as it is <paramref name="ms"/> milliseconds in, RGBA into <paramref name="room"/>'s
		/// <see cref="Workspace.Composed"/> (its width a row). Every frame it shows: no allocations.
		/// </summary>
		public void Compose( long ms, Workspace room )
		{
			var area = Width * Height;
			var sums = room.Scratch;
			for ( var i = 0; i < area; i++ )
			{
				sums[ i * 3 ] = Still[ i * 4 ] / 255f;
				sums[ i * 3 + 1 ] = Still[ i * 4 + 1 ] / 255f;
				sums[ i * 3 + 2 ] = Still[ i * 4 + 2 ] / 255f;
			}

			foreach ( var layer in Layers )
			{
				ColourAt( layer, ms, out var red, out var green, out var blue );
				for ( var row = 0; row < layer.Height; row++ )
				{
					var into = (layer.Y - Y + row) * Width + (layer.X - X);
					for ( var column = 0; column < layer.Width; column++ )
					{
						var weight = layer.Weights[ row * layer.Width + column ] / 255f;
						if ( weight <= 0f )
							continue;

						var at = (into + column) * 3;
						sums[ at ] += red * weight;
						sums[ at + 1 ] += green * weight;
						sums[ at + 2 ] += blue * weight;
					}
				}
			}

			var pixels = room.Composed;
			for ( var i = 0; i < area; i++ )
			{
				var alpha = Still[ i * 4 + 3 ];
				Straight( sums[ i * 3 ], sums[ i * 3 + 1 ], sums[ i * 3 + 2 ], alpha / 255f, pixels, i * 4 );
				pixels[ i * 4 + 3 ] = alpha;
			}
		}
	}

	/// <summary>
	/// An emblem being drawn, a layer a <see cref="Step"/>, in a <see cref="Workspace"/> it has to itself until it is
	/// <see cref="Done"/>: then the picture is the workspace's <see cref="Workspace.Pixels"/>, and its moving part, if
	/// any, <see cref="Moving"/>.
	/// </summary>
	public sealed class Job
	{
		private readonly DrawnShape _emblem;
		private readonly int _size;
		private readonly Workspace _room;
		private readonly ScriptCache _scripts;
		private readonly ScriptStack _stack;
		private readonly int[] _elements;
		private readonly int[] _counts;
		private readonly List<Moving.Layer> _moving = new();
		private int _next;
		private long _movingLeft;

		/// <summary>The emblem drawn.</summary>
		public DrawnShape Emblem => _emblem;

		/// <summary>Whether it is drawn.</summary>
		public bool Done { get; private set; }

		/// <summary>Its moving part once it is drawn; null when no layer of it moves.</summary>
		public Moving Moving { get; private set; }

		/// <summary>
		/// Starts drawing <paramref name="emblem"/> <paramref name="size"/> pixels across in <paramref name="room"/>, its
		/// moving layers' scripts compiled by <paramref name="scripts"/> and run on <paramref name="stack"/>.
		/// </summary>
		public Job( DrawnShape emblem, int size, Workspace room, ScriptCache scripts, ScriptStack stack )
		{
			_emblem = emblem;
			_size = size;
			_room = room;
			_scripts = scripts;
			_stack = stack;
			room.Fit( size );
			Array.Fill( room.Through, 1f );
			Array.Clear( room.Red );
			Array.Clear( room.Green );
			Array.Clear( room.Blue );
			Array.Clear( room.Alpha );
			_movingLeft = (long)MostMovingPictures * size * size;

			// Layers moving the same way are its elements, in turn up the stack, as letters of a clan tag were
			var layers = emblem.Emblem ?? new List<EmblemLayer>();
			_elements = new int[ layers.Count ];
			_counts = new int[ layers.Count ];
			var perMotion = new Dictionary<Sticker.Motion, int>();
			for ( var i = 0; i < layers.Count; i++ )
			{
				if ( !Moves( layers[ i ] ) )
					continue;

				var motion = layers[ i ].AnimationIs;
				_elements[ i ] = perMotion.GetValueOrDefault( motion );
				perMotion[ motion ] = _elements[ i ] + 1;
			}

			for ( var i = 0; i < layers.Count; i++ )
				_counts[ i ] = Moves( layers[ i ] ) ? perMotion[ layers[ i ].AnimationIs ] : 0;
		}

		private static bool Moves( EmblemLayer layer ) => !layer.Hidden && !layer.Eraser && layer.AnimationIs != Sticker.Motion.None;

		/// <summary>Draws the next layer down, or finishes; true once it is done.</summary>
		public bool Step()
		{
			if ( Done )
				return true;

			var layers = _emblem.Emblem ?? new List<EmblemLayer>();
			if ( _next < layers.Count )
			{
				var index = layers.Count - 1 - _next++;
				Draw( index, layers[ index ] );
				return false;
			}

			Finish();
			Done = true;
			return true;
		}

		private void Draw( int index, EmblemLayer layer )
		{
			// A lenticular sticker's picture is its first look; its second is drawn on its own (see DrawnShape.SecondLook)
			if ( layer.Hidden || layer.Side == EmblemLayer.SecondLook && _emblem.FinishIs == StickerFinish.Lenticular )
				return;

			var outline = layer.Eraser ? 0f : layer.OutlineWidth;
			var soft = 2f / _size * SoftPixels;
			var copies = new List<(List<ShapeStroke> Strokes, bool EvenOdd, ShapeRaster.Area Area)>();
			foreach ( var mirrored in _emblem.Mirror ? new[] { false, true } : new[] { false } )
			{
				var strokes = Core.Emblem.StrokesOf( _emblem, layer, mirrored, out var evenOdd );
				var area = AreaOf( strokes, outline + 2f * soft );
				if ( !area.IsEmpty )
					copies.Add( (strokes, evenOdd, area) );
			}

			if ( copies.Count == 0 )
				return;

			var moving = Moves( layer ) ? Start( index, layer, copies ) : null;
			var (red, green, blue) = Rgb( layer.Color );
			var (rimRed, rimGreen, rimBlue) = Rgb( layer.OutlineColor );
			var opacity = layer.Opacity;
			var size = _size;
			var room = _room;
			foreach ( var (strokes, evenOdd, area) in copies )
			{
				var distances = ShapeRaster.Distances( strokes, size, outline, room.Canvas, area, evenOdd );
				for ( var row = area.FirstRow; row <= area.LastRow; row++ )
				{
					for ( var column = area.FirstColumn; column <= area.LastColumn; column++ )
					{
						var i = row * size + column;
						var fill = ShapeRaster.Covered( distances[ i ], soft );
						var outer = outline > 0f ? ShapeRaster.Covered( distances[ i ] - outline, soft ) : fill;
						var covered = outer * opacity;
						if ( covered <= 0f )
							continue;

						var through = room.Through[ i ];
						room.Through[ i ] = through * (1f - covered);
						if ( layer.Eraser )
							continue;

						var filled = fill * opacity * through;
						var rim = (outer - fill) * opacity * through;
						if ( moving is not null )
						{
							var at = (row - moving.Y) * moving.Width + column - moving.X;
							moving.Weights[ at ] = (byte)Math.Min( 255f, moving.Weights[ at ] + filled * 255f + 0.5f );
						}
						else
						{
							room.Red[ i ] += red * filled;
							room.Green[ i ] += green * filled;
							room.Blue[ i ] += blue * filled;
						}

						room.Red[ i ] += rimRed * rim;
						room.Green[ i ] += rimGreen * rim;
						room.Blue[ i ] += rimBlue * rim;
						room.Alpha[ i ] += filled + rim;
					}
				}
			}
		}

		// A moving layer's weights over the box both its halves cover, while the emblem has room for them
		private Moving.Layer Start( int index, EmblemLayer layer, List<(List<ShapeStroke> Strokes, bool EvenOdd, ShapeRaster.Area Area)> copies )
		{
			var (firstRow, lastRow, firstColumn, lastColumn) = (int.MaxValue, int.MinValue, int.MaxValue, int.MinValue);
			foreach ( var (_, _, area) in copies )
				(firstRow, lastRow, firstColumn, lastColumn) = (Math.Min( firstRow, area.FirstRow ), Math.Max( lastRow, area.LastRow ), Math.Min( firstColumn, area.FirstColumn ), Math.Max( lastColumn, area.LastColumn ));

			var (width, height) = (lastColumn - firstColumn + 1, lastRow - firstRow + 1);
			if ( (long)width * height > _movingLeft )
				return null;

			_movingLeft -= (long)width * height;
			var (red, green, blue) = Rgb( layer.Color );
			var moving = new Moving.Layer
			{
				X = firstColumn, Y = firstRow, Width = width, Height = height, Weights = new byte[ width * height ],
				Colour = MovingColour.OfSticker( layer.AnimationIs, layer.CycleColours(), layer.Color, _scripts, _stack ),
				Speed = layer.AnimationSpeed, Red = red, Green = green, Blue = blue, Element = _elements[ index ], Count = _counts[ index ],
			};
			_moving.Add( moving );
			return moving;
		}

		// The pixels near enough the strokes to be touched: their box grown by half a line's width and reach
		private ShapeRaster.Area AreaOf( List<ShapeStroke> strokes, float reach )
		{
			var (minX, minY, maxX, maxY) = (float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
			foreach ( var stroke in strokes )
			{
				if ( stroke.Erase )
					continue;

				var half = stroke.Closed ? 0f : (stroke.Width > 0f ? stroke.Width : ShapeRaster.LineWidth) / 2f;
				for ( var i = 0; i + 1 < stroke.Points.Length; i += 2 )
					(minX, minY, maxX, maxY) = (MathF.Min( minX, stroke.Points[ i ] - half ), MathF.Min( minY, stroke.Points[ i + 1 ] - half ), MathF.Max( maxX, stroke.Points[ i ] + half ), MathF.Max( maxY, stroke.Points[ i + 1 ] + half ));
			}

			if ( minX > maxX )
				return new ShapeRaster.Area( 0, -1, 0, -1 );

			var unit = 2f / _size;
			return new ShapeRaster.Area(
				Math.Max( 0, (int)MathF.Floor( (1f - maxY - reach) / unit ) ), Math.Min( _size - 1, (int)MathF.Ceiling( (1f - minY + reach) / unit ) ),
				Math.Max( 0, (int)MathF.Floor( (minX - reach + 1f) / unit ) ), Math.Min( _size - 1, (int)MathF.Ceiling( (maxX + reach + 1f) / unit ) ) );
		}

		// The picture as the still layers left it, then the moving ones put in as they are at the start
		private void Finish()
		{
			var room = _room;
			var count = _size * _size;
			for ( var i = 0; i < count; i++ )
			{
				Straight( room.Red[ i ], room.Green[ i ], room.Blue[ i ], room.Alpha[ i ], room.Pixels, i * 4 );

				room.Pixels[ i * 4 + 3 ] = (byte)(Math.Clamp( room.Alpha[ i ], 0f, 1f ) * 255f + 0.5f);
			}

			if ( _moving.Count == 0 )
				return;

			var (firstRow, lastRow, firstColumn, lastColumn) = (int.MaxValue, int.MinValue, int.MaxValue, int.MinValue);
			foreach ( var layer in _moving )
				(firstRow, lastRow, firstColumn, lastColumn) = (Math.Min( firstRow, layer.Y ), Math.Max( lastRow, layer.Y + layer.Height - 1 ), Math.Min( firstColumn, layer.X ), Math.Max( lastColumn, layer.X + layer.Width - 1 ));

			var moving = new Moving { X = firstColumn, Y = firstRow, Width = lastColumn - firstColumn + 1, Height = lastRow - firstRow + 1 };
			moving.Layers.AddRange( _moving );
			moving.Still = new byte[ moving.Width * moving.Height * 4 ];
			for ( var row = 0; row < moving.Height; row++ )
			{
				for ( var column = 0; column < moving.Width; column++ )
				{
					var i = (moving.Y + row) * _size + moving.X + column;
					var at = (row * moving.Width + column) * 4;
					moving.Still[ at ] = (byte)(Math.Clamp( room.Red[ i ], 0f, 1f ) * 255f + 0.5f);
					moving.Still[ at + 1 ] = (byte)(Math.Clamp( room.Green[ i ], 0f, 1f ) * 255f + 0.5f);
					moving.Still[ at + 2 ] = (byte)(Math.Clamp( room.Blue[ i ], 0f, 1f ) * 255f + 0.5f);
					moving.Still[ at + 3 ] = (byte)(Math.Clamp( room.Alpha[ i ], 0f, 1f ) * 255f + 0.5f);
				}
			}

			Moving = moving;
			moving.Compose( 0, room );
			Blit( moving, room, _size );
		}
	}

	/// <summary>
	/// <paramref name="emblem"/> drawn whole, <paramref name="size"/> pixels across, in <paramref name="room"/> (its moving
	/// layers' scripts by <paramref name="scripts"/>, on <paramref name="stack"/>): its <see cref="Workspace.Pixels"/>, and
	/// its moving part in <paramref name="moving"/> (null for none).
	/// </summary>
	public static byte[] Rgba( DrawnShape emblem, int size, Workspace room, ScriptCache scripts, ScriptStack stack, out Moving moving )
	{
		var job = new Job( emblem, size, room, scripts, stack );
		while ( !job.Step() )
		{
		}

		moving = job.Moving;
		return room.Pixels;
	}

	/// <summary>
	/// Puts <paramref name="moving"/>, as it is <paramref name="ms"/> milliseconds in, into <paramref name="room"/>'s
	/// <see cref="Workspace.Pixels"/> (a picture <paramref name="size"/> across): for a picture shown whole.
	/// </summary>
	public static void Animate( Moving moving, long ms, Workspace room, int size )
	{
		moving.Compose( ms, room );
		Blit( moving, room, size );
	}

	private static void Blit( Moving moving, Workspace room, int size )
	{
		for ( var row = 0; row < moving.Height; row++ )
			Array.Copy( room.Composed, row * moving.Width * 4, room.Pixels, ((moving.Y + row) * size + moving.X) * 4, moving.Width * 4 );
	}

	// A moving layer's colour ms milliseconds in, the way a sticker's moves on a weapon: its motion's script, the layers
	// moving the same way its elements (as letters of a clan tag were); its own colour when the script doesn't run
	private static void ColourAt( Moving.Layer layer, long ms, out float red, out float green, out float blue )
	{
		if ( layer.Colour is { } colour )
			(red, green, blue) = colour.At( (long)(ms * layer.Speed), layer.Element, Math.Max( 1, layer.Count ) );
		else
			(red, green, blue) = (layer.Red, layer.Green, layer.Blue);
	}

	// Premultiplied colour as straight colour bytes; white where nothing covers, so a blurred edge never darkens
	private static void Straight( float red, float green, float blue, float alpha, byte[] into, int at )
	{
		if ( alpha <= 0f )
		{
			into[ at ] = into[ at + 1 ] = into[ at + 2 ] = 255;
			return;
		}

		into[ at ] = (byte)(Math.Clamp( red / alpha, 0f, 1f ) * 255f + 0.5f);
		into[ at + 1 ] = (byte)(Math.Clamp( green / alpha, 0f, 1f ) * 255f + 0.5f);
		into[ at + 2 ] = (byte)(Math.Clamp( blue / alpha, 0f, 1f ) * 255f + 0.5f);
	}

	// A #rrggbb colour as red, green and blue, 0 to 1; white for anything else
	private static (float, float, float) Rgb( string hex )
	{
		if ( !PlayerLook.IsColour( hex ) )
			return (1f, 1f, 1f);

		var value = 0;
		for ( var i = 1; i < 7; i++ )
			value = value << 4 | HexDigit( hex[ i ] );

		return ((value >> 16 & 255) / 255f, (value >> 8 & 255) / 255f, (value & 255) / 255f);
	}

	private static int HexDigit( char c ) => c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;
}
