using System;
using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>
/// A drawn shape (see <see cref="DrawnShape"/>) as pixels, for a sprite or a thumbnail: closed strokes filled, open
/// ones drawn <see cref="LineWidth"/> wide, edges smoothed over a pixel, and a sticker's outline round the outside. Each
/// pixel's distance is only worked out from the strokes near it, so even a shape of every point it may have is drawn in
/// a millisecond or two, and the same shape is the same pixels on every machine.
/// </summary>
public static class ShapeRaster
{
	/// <summary>How many pixels across a shape is drawn: a sprite covers a few hundred pixels of the screen at most.</summary>
	public const int Size = 128;

	/// <summary>How wide open strokes are drawn, on the shape's square (2 across).</summary>
	public const float LineWidth = 0.08f;

	// How many pixels an edge blurs over
	private const float SoftPixels = 1f;

	/// <summary>
	/// The room shapes are drawn in, kept from one drawing to the next by whoever draws many (see
	/// <see cref="Rgba"/>), so drawing a crowd's shapes makes no garbage to collect.
	/// </summary>
	public sealed class Canvas
	{
		internal float[] Edges = Array.Empty<float>();
		internal float[] Lines = Array.Empty<float>();
		internal float[] Distances = Array.Empty<float>();
		internal bool[] Inside = Array.Empty<bool>();
		internal byte[] Pixels = Array.Empty<byte>();
		internal readonly List<float> Crossings = new();

		// Room for a size by size drawing, made again only for another size
		internal void Fit( int size )
		{
			if ( Edges.Length == size * size )
				return;

			Edges = new float[ size * size ];
			Lines = new float[ size * size ];
			Distances = new float[ size * size ];
			Inside = new bool[ size * size ];
			Pixels = new byte[ size * size * 4 ];
		}
	}

	/// <summary>
	/// <paramref name="shape"/> as <paramref name="size"/> by <paramref name="size"/> pixels, RGBA row by row from the top:
	/// a plain shape white with the shape in its alpha (for a layer to colour); a sticker its fill and outline colours,
	/// the fill white when it is animated (for its animation to colour). Drawn in <paramref name="canvas"/> when one is
	/// given, and then the pixels are its own, until it draws the next.
	/// </summary>
	public static byte[] Rgba( DrawnShape shape, int size = Size, Canvas canvas = null )
	{
		canvas ??= new Canvas();
		var sticker = shape.Sticker;
		var outline = sticker?.OutlineWidth ?? 0f;
		var distances = Distances( shape.Decode(), size, outline, canvas );
		var fill = Colour( sticker is null || sticker.IsAnimated ? null : sticker.Fill );
		var edge = Colour( sticker?.Outline );
		var soft = 2f / size * SoftPixels;

		var pixels = canvas.Pixels;
		for ( var i = 0; i < distances.Length; i++ )
		{
			var inside = Covered( distances[ i ], soft );
			var outer = outline > 0f ? Covered( distances[ i ] - outline, soft ) : inside;
			var rim = outer - inside;
			for ( var channel = 0; channel < 3; channel++ )
			{
				var value = outer > 0f ? (fill[ channel ] * inside + edge[ channel ] * rim) / outer : fill[ channel ];
				pixels[ i * 4 + channel ] = (byte)(value * 255f + 0.5f);
			}

			pixels[ i * 4 + 3 ] = (byte)(outer * 255f + 0.5f);
		}

		return pixels;
	}

	/// <summary>
	/// How far each pixel's middle is outside the strokes (negative inside a closed one or within a line's width), row by
	/// row from the top; worked out up to <paramref name="reach"/> past them, and further away as far as can be.
	/// </summary>
	public static float[] Distances( List<ShapeStroke> strokes, int size, float reach, Canvas canvas = null )
	{
		canvas ??= new Canvas();
		canvas.Fit( size );
		var count = size * size;
		var edges = canvas.Edges;
		var lines = canvas.Lines;
		Array.Fill( edges, float.MaxValue );
		Array.Fill( lines, float.MaxValue );

		var unit = 2f / size;
		var halfWidth = LineWidth * 0.5f;
		var near = halfWidth + reach + 2f * unit * SoftPixels;
		foreach ( var stroke in strokes )
		{
			var points = stroke.Points;
			var corners = stroke.Count;
			var segments = stroke.Closed ? corners : Math.Max( 1, corners - 1 );
			for ( var s = 0; s < segments && corners > 0; s++ )
			{
				var t = Math.Min( (s + 1) % corners, corners - 1 );
				float ax = points[ s * 2 ], ay = points[ s * 2 + 1 ], bx = points[ t * 2 ], by = points[ t * 2 + 1 ];
				var firstColumn = Math.Max( 0, (int)MathF.Floor( (MathF.Min( ax, bx ) - near + 1f) / unit ) );
				var lastColumn = Math.Min( size - 1, (int)MathF.Ceiling( (MathF.Max( ax, bx ) + near + 1f) / unit ) );
				var firstRow = Math.Max( 0, (int)MathF.Floor( (1f - MathF.Max( ay, by ) - near) / unit ) );
				var lastRow = Math.Min( size - 1, (int)MathF.Ceiling( (1f - MathF.Min( ay, by ) + near) / unit ) );
				var field = stroke.Closed ? edges : lines;
				var less = stroke.Closed ? 0f : halfWidth;
				for ( var row = firstRow; row <= lastRow; row++ )
				{
					var y = 1f - (row + 0.5f) * unit;
					for ( var column = firstColumn; column <= lastColumn; column++ )
					{
						var x = (column + 0.5f) * unit - 1f;
						var distance = DistanceToSegment( x, y, ax, ay, bx, by ) - less;
						if ( distance < field[ row * size + column ] )
							field[ row * size + column ] = distance;
					}
				}
			}
		}

		// Inside a closed stroke (even-odd, so one crossing itself leaves out what it goes round twice): strokes join where
		// they overlap, as pictures' outlines do
		var inside = canvas.Inside;
		Array.Clear( inside );
		var crossings = canvas.Crossings;
		foreach ( var stroke in strokes )
		{
			if ( !stroke.Closed )
				continue;

			for ( var row = 0; row < size; row++ )
			{
				var y = 1f - (row + 0.5f) * unit;
				crossings.Clear();
				var points = stroke.Points;
				for ( int i = 0, j = stroke.Count - 1; i < stroke.Count; j = i++ )
				{
					float yi = points[ i * 2 + 1 ], yj = points[ j * 2 + 1 ];
					if ( (yi > y) != (yj > y) )
						crossings.Add( points[ i * 2 ] + (y - yi) * (points[ j * 2 ] - points[ i * 2 ]) / (yj - yi) );
				}

				crossings.Sort();
				for ( var k = 0; k + 1 < crossings.Count; k += 2 )
				{
					var from = Math.Max( 0, (int)MathF.Ceiling( (crossings[ k ] + 1f) / unit - 0.5f ) );
					var to = Math.Min( size - 1, (int)MathF.Floor( (crossings[ k + 1 ] + 1f) / unit - 0.5f ) );
					for ( var column = from; column <= to; column++ )
						inside[ row * size + column ] = true;
				}
			}
		}

		var distances = canvas.Distances;
		for ( var i = 0; i < count; i++ )
			distances[ i ] = MathF.Min( inside[ i ] ? -edges[ i ] : edges[ i ], lines[ i ] );

		return distances;
	}

	// How much a pixel this far outside an edge is covered: fully inside, none past the blur
	private static float Covered( float distance, float soft ) => Math.Clamp( 0.5f - distance / soft, 0f, 1f );

	private static float DistanceToSegment( float x, float y, float ax, float ay, float bx, float by )
	{
		var dx = bx - ax;
		var dy = by - ay;
		var length = dx * dx + dy * dy;
		var along = length > 0f ? Math.Clamp( ((x - ax) * dx + (y - ay) * dy) / length, 0f, 1f ) : 0f;
		var px = ax + along * dx - x;
		var py = ay + along * dy - y;
		return MathF.Sqrt( px * px + py * py );
	}

	// A colour as red, green and blue, 0 to 1; white for none
	private static float[] Colour( string hex )
	{
		if ( !PlayerLook.IsColour( hex ) )
			return new[] { 1f, 1f, 1f };

		var value = Convert.ToInt32( hex[ 1.. ], 16 );
		return new[] { (value >> 16 & 255) / 255f, (value >> 8 & 255) / 255f, (value & 255) / 255f };
	}
}
