using System;
using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>
/// What an emblem (a drawn shape made of a stack of layers, see <see cref="DrawnShape.Emblem"/>) is made of where it is:
/// each layer's lines on the emblem's square (see <see cref="StrokesOf"/>), drawn by <see cref="EmblemRaster"/>, and
/// together its outline and formation for sprites to come from. An eraser layer, and an eraser's strokes in a drawn
/// layer, cut away what is under them, so formation points there are dropped too. Also what is under a point, for the
/// editor to pick.
/// </summary>
public static class Emblem
{
	// The most layers and drawn shapes read from an emblem, kept or not: a few times what one keeps, so an emblem listing
	// thousands costs no more than a few
	private const int MostRead = EmblemLayer.MaxPerEmblem * 4;

	// How many places are tried for each point of a formation, so points an eraser drops leave enough to choose from
	private const int TriesPerPoint = 3;

	// How near an open line a point picks it, past half its width, on the emblem's square
	private const float PickReach = 0.03f;

	/// <summary>
	/// <paramref name="layers"/> within bounds, carrying the drawn shapes they name of <paramref name="parts"/> (each once,
	/// in <paramref name="kept"/>), or null when none is left: at most <see cref="EmblemLayer.MaxPerEmblem"/> layers, each a
	/// plain drawn shape (no sticker or emblem of its own) of at most <see cref="DrawnShape.MaxPoints"/> points and at
	/// most <see cref="DrawnShape.MaxEmblemPoints"/> points all together; a layer naming a shape past those, or one not
	/// carried, is left out. A name that was tampered with can't stand for another shape: a layer names the shape by
	/// the id worked out from what it is.
	/// </summary>
	public static List<EmblemLayer> Sanitize( List<EmblemLayer> layers, List<DrawnShape> parts, out List<DrawnShape> kept )
	{
		// Read by the reference each came with, and only made sound once a layer names it: one no layer names costs nothing
		var given = new Dictionary<string, DrawnShape>();
		var read = 0;
		foreach ( var part in parts ?? new List<DrawnShape>() )
		{
			if ( read++ >= MostRead )
				break;

			if ( part is not null && DrawnShape.IsReference( DrawnShape.ReferencePrefix + part.Id ) )
				given.TryAdd( DrawnShape.ReferencePrefix + part.Id, part );
		}

		var named = new Dictionary<string, DrawnShape>();
		var used = new List<DrawnShape>();
		var points = 0;
		string Resolve( string reference )
		{
			if ( !named.TryGetValue( reference, out var part ) )
			{
				if ( !given.TryGetValue( reference, out var raw ) )
					return null;

				// A plain shape, named for the editor's list: no sticker or emblem of its own
				part = new DrawnShape { Strokes = raw.Strokes, Name = raw.Name }.Sanitized();
				named[ reference ] = part;
				if ( part is not null )
					named.TryAdd( part.Reference, part );
			}

			if ( part is null )
				return null;

			if ( used.Exists( x => x.Id == part.Id ) )
				return part.Reference;

			if ( used.Count >= DrawnShape.MaxParts || points + part.PointCount > DrawnShape.MaxEmblemPoints )
				return null;

			used.Add( part );
			points += part.PointCount;
			return part.Reference;
		}

		var sanitized = new List<EmblemLayer>();
		read = 0;
		foreach ( var layer in layers ?? new List<EmblemLayer>() )
		{
			if ( sanitized.Count >= EmblemLayer.MaxPerEmblem || read++ >= MostRead )
				break;

			if ( layer?.Sanitized( Resolve ) is { } clean )
				sanitized.Add( clean );
		}

		kept = used.Count > 0 ? used : null;
		return sanitized.Count > 0 ? sanitized : null;
	}

	/// <summary>
	/// <paramref name="shape"/> as an emblem to change: an emblem as it is, and a shape or sticker made before emblems as
	/// an emblem of one layer filling it, coloured as the sticker was (white and plain for a plain shape), keeping its name
	/// and where it came from. Null for nothing to see.
	/// </summary>
	public static DrawnShape FromShape( DrawnShape shape )
	{
		if ( shape is null || shape.IsEmblem )
			return shape;

		var part = DrawnShape.From( shape.Decode(), shape.Name );
		if ( part is null )
			return null;

		var sticker = shape.Sticker;
		var layer = new EmblemLayer
		{
			Shape = part.Reference, Color = sticker?.IsAnimated == false ? sticker.Fill : "#ffffff", OutlineColor = sticker?.Outline ?? "#000000",
			OutlineWidth = sticker?.OutlineWidth ?? 0f, Animation = sticker?.Animation, Palette = sticker?.Palette, AnimationSpeed = sticker?.Speed ?? 1f,
		};
		return new DrawnShape
		{
			Name = shape.Name, ForkedFrom = shape.ForkedFrom, Emblem = new List<EmblemLayer> { layer }, Parts = new List<DrawnShape> { part },
		}.Sanitized();
	}

	/// <summary>
	/// The lines of layer <paramref name="layer"/> of <paramref name="emblem"/> where they are on it, flipped left to right
	/// for <paramref name="mirrored"/> (its other half, see <see cref="DrawnShape.Mirror"/>): an open line or an eraser's
	/// stroke as wide as it is drawn there. <paramref name="evenOdd"/> says whether its closed lines are filled together,
	/// so one inside another leaves a hole (a standard ring), rather than each on its own as a drawn shape's are.
	/// </summary>
	public static List<ShapeStroke> StrokesOf( DrawnShape emblem, EmblemLayer layer, bool mirrored, out bool evenOdd )
	{
		var strokes = new List<ShapeStroke>();
		evenOdd = false;
		if ( layer.StandardIs is { } standard )
		{
			evenOdd = true;
			var (closed, open) = StandardShapes.Outline( standard );
			foreach ( var points in closed )
				strokes.Add( Placed( layer, new ShapeStroke { Closed = true, Points = points }, mirrored ) );

			foreach ( var points in open )
				strokes.Add( Placed( layer, new ShapeStroke { Width = StandardShapes.LineWidth, Points = points }, mirrored ) );

			return strokes;
		}

		foreach ( var stroke in emblem.PartOf( layer.Shape )?.Decode() ?? new List<ShapeStroke>() )
			strokes.Add( Placed( layer, stroke, mirrored ) );

		return strokes;
	}

	// A stroke of a layer's shape where it is on the emblem, its width as it is drawn there
	private static ShapeStroke Placed( EmblemLayer layer, ShapeStroke stroke, bool mirrored )
	{
		var points = new float[ stroke.Points.Length ];
		for ( var i = 0; i + 1 < points.Length; i += 2 )
		{
			var (x, y) = layer.Place( stroke.Points[ i ], stroke.Points[ i + 1 ] );
			points[ i ] = mirrored ? -x : x;
			points[ i + 1 ] = y;
		}

		var width = stroke.Closed ? 0f : (stroke.Width > 0f ? stroke.Width : ShapeRaster.LineWidth) * layer.WidthScale;
		return new ShapeStroke { Closed = stroke.Closed, Erase = stroke.Erase, Width = width, Points = points };
	}

	/// <summary>
	/// The layers of <paramref name="emblem"/> as they show, bottom first, each with whether it is its other half
	/// (<see cref="DrawnShape.Mirror"/>): hidden ones left out.
	/// </summary>
	public static IEnumerable<(int Index, bool Mirrored)> Shown( DrawnShape emblem )
	{
		for ( var i = 0; i < (emblem.Emblem?.Count ?? 0); i++ )
		{
			if ( emblem.Emblem[ i ].Hidden )
				continue;

			yield return (i, false);
			if ( emblem.Mirror )
				yield return (i, true);
		}
	}

	/// <summary>
	/// The outlines of the layers <paramref name="emblem"/> shows (not its erasers), together, where they are on it:
	/// closed ones and open lines, as <see cref="DrawnShape.Outline"/> gives them.
	/// </summary>
	public static (List<float[]> Closed, List<float[]> Open) Outline( DrawnShape emblem )
	{
		var closed = new List<float[]>();
		var open = new List<float[]>();
		foreach ( var (index, mirrored) in Shown( emblem ) )
		{
			if ( emblem.Emblem[ index ].Eraser )
				continue;

			foreach ( var stroke in StrokesOf( emblem, emblem.Emblem[ index ], mirrored, out _ ) )
			{
				if ( !stroke.Erase )
					(stroke.Closed ? closed : open).Add( stroke.Points );
			}
		}

		return (closed, open);
	}

	/// <summary>
	/// <paramref name="count"/> points of <paramref name="shape"/> for sprites to come from, as
	/// <see cref="StandardShapes.FormationOf"/> lays them out: along its outline (an emblem's layers' outlines together),
	/// or with <paramref name="filled"/> all over it (each of an emblem's layers on its own, so where they overlap is no
	/// hole). Points where an eraser cut it away are dropped and the rest spread to make up the count; a shape cut away
	/// entirely is a single point in its middle.
	/// </summary>
	public static float[] Formation( DrawnShape shape, bool filled, int count = StandardShapes.FormationPoints )
	{
		count = Math.Max( 1, count );
		var cuts = Cuts( shape );
		if ( !shape.IsEmblem && cuts.Count == 0 )
		{
			var (closed, open) = shape.Outline();
			return StandardShapes.FormationOf( closed, open, filled, count );
		}

		var tries = count * TriesPerPoint;
		var candidates = new List<float[]>();
		if ( filled && shape.IsEmblem )
		{
			// Each layer on its own, in proportion to how many there are
			var layers = new List<(List<float[]> Closed, List<float[]> Open)>();
			foreach ( var (index, mirrored) in Shown( shape ) )
			{
				if ( shape.Emblem[ index ].Eraser )
					continue;

				var (closed, open) = (new List<float[]>(), new List<float[]>());
				foreach ( var stroke in StrokesOf( shape, shape.Emblem[ index ], mirrored, out _ ) )
				{
					if ( !stroke.Erase )
						(stroke.Closed ? closed : open).Add( stroke.Points );
				}

				if ( closed.Count + open.Count > 0 )
					layers.Add( (closed, open) );
			}

			foreach ( var (closed, open) in layers )
				candidates.Add( StandardShapes.FormationOf( closed, open, true, Math.Max( 1, tries / layers.Count ) ) );
		}
		else
		{
			var (closed, open) = shape.Outline();
			if ( closed.Count + open.Count > 0 )
				candidates.Add( StandardShapes.FormationOf( closed, open, filled, tries ) );
		}

		var kept = new List<float>();
		foreach ( var points in candidates )
		{
			for ( var i = 0; i + 2 < points.Length; i += 3 )
			{
				// Upright and facing forwards: the shape's x is the body's y, its y the body's z
				if ( !IsCut( cuts, points[ i + 1 ], points[ i + 2 ] ) )
				{
					kept.Add( points[ i + 1 ] );
					kept.Add( points[ i + 2 ] );
				}
			}
		}

		var formation = new float[ count * 3 ];
		var survivors = kept.Count / 2;
		for ( var i = 0; i < count && survivors > 0; i++ )
		{
			var at = (int)((long)i * survivors / count);
			formation[ i * 3 + 1 ] = kept[ at * 2 ];
			formation[ i * 3 + 2 ] = kept[ at * 2 + 1 ];
		}

		return formation;
	}

	// What cuts a shape away, where it is: a drawn shape's eraser strokes, or an emblem's erasers and its drawn layers'
	// eraser strokes; each closed outline or line with its width, and whether its closed lines go together (a ring)
	private static List<(List<ShapeStroke> Strokes, bool EvenOdd)> Cuts( DrawnShape shape )
	{
		var cuts = new List<(List<ShapeStroke>, bool)>();
		if ( !shape.IsEmblem )
		{
			var erased = shape.Decode().FindAll( x => x.Erase );
			if ( erased.Count > 0 )
				cuts.Add( (ToLines( erased ), false) );

			return cuts;
		}

		foreach ( var (index, mirrored) in Shown( shape ) )
		{
			var strokes = StrokesOf( shape, shape.Emblem[ index ], mirrored, out var evenOdd );
			if ( shape.Emblem[ index ].Eraser )
				cuts.Add( (strokes.FindAll( x => !x.Erase ), evenOdd) );
			else if ( strokes.FindAll( x => x.Erase ) is { Count: > 0 } erased )
				cuts.Add( (ToLines( erased ), false) );
		}

		return cuts;
	}

	// Eraser strokes as the lines they cut along
	private static List<ShapeStroke> ToLines( List<ShapeStroke> erased ) =>
		erased.ConvertAll( x => new ShapeStroke { Width = x.Width, Points = x.Points } );

	private static bool IsCut( List<(List<ShapeStroke> Strokes, bool EvenOdd)> cuts, float x, float y )
	{
		foreach ( var (strokes, evenOdd) in cuts )
		{
			if ( Covers( strokes, evenOdd, x, y, 0f ) )
				return true;
		}

		return false;
	}

	/// <summary>
	/// The topmost layer of <paramref name="emblem"/> under the point <paramref name="x"/>, <paramref name="y"/> (on its
	/// square) that is showing and not locked, or -1 for none.
	/// </summary>
	public static int LayerAt( DrawnShape emblem, float x, float y )
	{
		for ( var i = (emblem.Emblem?.Count ?? 0) - 1; i >= 0; i-- )
		{
			var layer = emblem.Emblem[ i ];
			if ( layer.Hidden || layer.Locked )
				continue;

			foreach ( var mirrored in emblem.Mirror ? new[] { false, true } : new[] { false } )
			{
				var strokes = StrokesOf( emblem, layer, mirrored, out var evenOdd );
				strokes.RemoveAll( s => s.Erase );
				if ( Covers( strokes, evenOdd, x, y, PickReach ) )
					return i;
			}
		}

		return -1;
	}

	/// <summary>
	/// The box the shape of <paramref name="layer"/> fills on its own square (before it is placed): the least and most
	/// across and up; the whole square for a shape with no points.
	/// </summary>
	public static (float MinX, float MinY, float MaxX, float MaxY) LocalBounds( DrawnShape emblem, EmblemLayer layer )
	{
		var (minX, minY, maxX, maxY) = (float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
		void Grow( float[] points, float half )
		{
			for ( var i = 0; i + 1 < points.Length; i += 2 )
				(minX, minY, maxX, maxY) = (MathF.Min( minX, points[ i ] - half ), MathF.Min( minY, points[ i + 1 ] - half ), MathF.Max( maxX, points[ i ] + half ), MathF.Max( maxY, points[ i + 1 ] + half ));
		}

		if ( layer.StandardIs is { } standard )
		{
			var (closed, open) = StandardShapes.Outline( standard );
			closed.ForEach( x => Grow( x, 0f ) );
			open.ForEach( x => Grow( x, StandardShapes.LineWidth / 2f ) );
		}
		else
		{
			foreach ( var stroke in emblem.PartOf( layer.Shape )?.Decode() ?? new List<ShapeStroke>() )
			{
				if ( !stroke.Erase )
					Grow( stroke.Points, stroke.Closed ? 0f : (stroke.Width > 0f ? stroke.Width : ShapeRaster.LineWidth) / 2f );
			}
		}

		return minX > maxX ? (-1f, -1f, 1f, 1f) : (minX, minY, maxX, maxY);
	}

	// Whether strokes cover a point: inside a closed one (all of them together, even-odd, or any one on its own), or
	// within half its width (and reach) of an open one
	private static bool Covers( List<ShapeStroke> strokes, bool evenOdd, float x, float y, float reach )
	{
		var inside = false;
		foreach ( var stroke in strokes )
		{
			if ( stroke.Closed )
			{
				var within = EffectShape.Contains( stroke.Points, x, y );
				if ( evenOdd )
					inside ^= within;
				else if ( within )
					return true;

				continue;
			}

			var half = (stroke.Width > 0f ? stroke.Width : ShapeRaster.LineWidth) / 2f + reach;
			if ( NearLine( stroke.Points, x, y, half ) )
				return true;
		}

		return inside;
	}

	private static bool NearLine( float[] points, float x, float y, float reach )
	{
		var count = points.Length / 2;
		for ( var i = 0; i < count; i++ )
		{
			var j = Math.Min( i + 1, count - 1 );
			float ax = points[ i * 2 ], ay = points[ i * 2 + 1 ], bx = points[ j * 2 ], by = points[ j * 2 + 1 ];
			var (dx, dy) = (bx - ax, by - ay);
			var length = dx * dx + dy * dy;
			var along = length > 0f ? Math.Clamp( ((x - ax) * dx + (y - ay) * dy) / length, 0f, 1f ) : 0f;
			var (px, py) = (ax + along * dx - x, ay + along * dy - y);
			if ( px * px + py * py <= reach * reach )
				return true;
		}

		return false;
	}
}
