using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>Drawing shapes while building: points and lines, text, other shapes stamped in, fitting, all within the point cap.</summary>
internal sealed partial class ScriptBuilder
{
	private Draft DraftOf( int handle )
	{
		var index = handle - Handles.DraftBase;
		if ( index >= 0 && index < _drafts.Count && !_drafts[ index ].IsMade )
			return _drafts[ index ];

		Fail( "Only a shape made with Shape.New (or Shape.Text, Shapes.Mine) can be drawn into or styled: a standard shape is fixed" );
		return null;
	}

	private static int PointsOf( Draft draft )
	{
		var points = draft.Pending.Count / 2;
		foreach ( var stroke in draft.Strokes )
			points += stroke.Count;

		return points;
	}

	private bool Room( Draft draft, int points )
	{
		if ( PointsOf( draft ) + points > DrawnShape.MaxPoints )
			return Fail( $"A shape has at most {DrawnShape.MaxPoints} points" ) >= 0;

		if ( draft.Strokes.Count >= DrawnShape.MaxStrokes )
			return Fail( $"A shape has at most {DrawnShape.MaxStrokes} lines" ) >= 0;

		return true;
	}

	private bool Point( int handle, double x, double y )
	{
		if ( DraftOf( handle ) is not { } draft || !Room( draft, 1 ) )
			return false;

		draft.Pending.Add( Coordinate( x ) );
		draft.Pending.Add( Coordinate( y ) );
		return true;
	}

	private static float Coordinate( double value ) => (float)Math.Clamp( ScriptMath.Safe( value ), -1, 1 );

	private bool Points( int handle, int at, int count )
	{
		if ( DraftOf( handle ) is not { } draft || !Room( draft, count / 2 ) )
			return false;

		for ( var i = 0; i + 1 < count; i += 2 )
		{
			draft.Pending.Add( Coordinate( _machine.Arena[ at + i ] ) );
			draft.Pending.Add( Coordinate( _machine.Arena[ at + i + 1 ] ) );
		}

		return true;
	}

	private bool EndStroke( int handle, bool closed )
	{
		if ( DraftOf( handle ) is not { } draft )
			return false;

		if ( draft.Pending.Count > 0 )
		{
			if ( !Room( draft, 0 ) )
				return false;

			draft.Strokes.Add( new ShapeStroke { Closed = closed && draft.Pending.Count >= 6, Points = draft.Pending.ToArray() } );
			draft.Pending.Clear();
		}

		return true;
	}

	private bool Fit( int handle )
	{
		if ( DraftOf( handle ) is not { } draft || !EndStroke( handle, false ) )
			return false;

		var fitted = ShapeStroke.Fitted( draft.Strokes );
		draft.Strokes.Clear();
		draft.Strokes.AddRange( fitted );
		return true;
	}

	private bool Fade( int handle, double from, double to )
	{
		if ( DraftOf( handle ) is not { } draft )
			return false;

		(draft.FadeFrom, draft.FadeTo) = ((float)ScriptMath.Safe( from ), (float)ScriptMath.Safe( to ));
		return true;
	}

	private bool Letter( int handle, string text, double x, double y, double size, double angle )
	{
		// Hershey text is fitted to the square (2 across): size 2 keeps it that size
		return DraftOf( handle ) is { } draft && AddStrokes( draft, HersheyFont.Strokes( text ), x, y, size / 2, size / 2, angle );
	}

	private bool Stamp( int handle, int other, double x, double y, double size, double angle )
	{
		if ( DraftOf( handle ) is not { } draft )
			return false;

		var strokes = StrokesOf( other );
		return strokes is not null && AddStrokes( draft, strokes, x, y, size, size, angle );
	}

	// The strokes of a shape handle: a standard shape's outlines, or a drawn shape's lines
	private List<ShapeStroke> StrokesOf( int handle )
	{
		if ( handle >= 0 && handle < Handles.DraftBase && Enum.IsDefined( typeof( StandardShape ), handle ) )
		{
			var (closed, open) = StandardShapes.Outline( (StandardShape)handle );
			var strokes = new List<ShapeStroke>();
			foreach ( var points in closed )
				strokes.Add( new ShapeStroke { Closed = true, Points = points } );

			foreach ( var points in open )
				strokes.Add( new ShapeStroke { Points = points } );

			return strokes;
		}

		var index = handle - Handles.DraftBase;
		if ( index >= 0 && index < _drafts.Count )
			return _drafts[ index ].IsMade ? _drafts[ index ].Made.Decode() : new List<ShapeStroke>( _drafts[ index ].Strokes );

		Fail( "That isn't a shape" );
		return null;
	}

	// Adds strokes scaled, turned (degrees, anticlockwise) and moved, within the shape's caps
	private bool AddStrokes( Draft draft, List<ShapeStroke> strokes, double x, double y, double scaleX, double scaleY, double angle )
	{
		var (sin, cos) = Math.SinCos( ScriptMath.Safe( angle ) * Math.PI / 180 );
		foreach ( var stroke in strokes )
		{
			if ( !Room( draft, stroke.Count ) )
				return false;

			var points = new float[ stroke.Points.Length ];
			for ( var i = 0; i + 1 < points.Length; i += 2 )
			{
				var px = stroke.Points[ i ] * scaleX;
				var py = stroke.Points[ i + 1 ] * scaleY;
				points[ i ] = Coordinate( x + px * cos - py * sin );
				points[ i + 1 ] = Coordinate( y + px * sin + py * cos );
			}

			draft.Strokes.Add( new ShapeStroke { Closed = stroke.Closed, Points = points } );
		}

		return true;
	}
}
