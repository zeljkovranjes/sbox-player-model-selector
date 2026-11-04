using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

internal sealed partial class ScriptBuilder
{
	/// <summary>
	/// What the build made, within bounds: for an effect its layers (naming the drawn shapes made, which it carries) after
	/// the same sanitizer every look goes through; for a shape its strokes and emblem layers.
	/// </summary>
	public ScriptBuild Finish()
	{
		switch ( _program.Kind )
		{
			case ScriptKind.Shape:
			{
				var shape = Make( 0 );
				if ( shape is null )
					return Failed( "The shape has nothing to draw: add points with shape.Point( x, y ), or layers with shape.AddLayer( ... )" );

				return new ScriptBuild { Shape = shape, Emblem = Parts( _drafts[ 0 ] ), Program = _program };
			}

			case ScriptKind.Effect: return FinishEffect();
			default: return new ScriptBuild { Program = _program };
		}
	}

	private ScriptBuild FinishEffect()
	{
		if ( _layers.Count == 0 )
			return Failed( "An effect needs at least one layer: add one with Sprite(), Light() or Trail()" );

		var shapes = new List<DrawnShape>();
		var emblems = new Dictionary<string, IReadOnlyList<EmblemPart>>();
		foreach ( var layer in _layers )
		{
			if ( !Resolve( layer.Formation, shapes, emblems, out var formation ) || !Resolve( layer.Picture, shapes, emblems, out var picture ) )
				return Failed( Error );

			(layer.Formation, layer.Picture) = (formation, picture);
		}

		_effect.Layers = _layers;
		_effect.Shapes = shapes.Count > 0 ? shapes : null;
		var made = _effect.Sanitized();
		if ( made is null || made.Layers.Count != _layers.Count )
			return Failed( "The effect has a layer it can't show" );

		_layers = made.Layers;
		var running = new List<(int, PropertyId, int)>();
		foreach ( var (global, property, site) in _program.RunningSets )
			running.Add( ((int)_machine.Globals[ global ], property, site) );

		return new ScriptBuild
		{
			Effect = made, Emblems = emblems, Program = _program, Sites = _sites, SharedSites = _sharedSites,
			LayerSites = _layerSites.ToArray(), Running = running,
		};
	}

	// A layer's shape or picture once its drawn shapes are made: their references, each shape carried once
	private bool Resolve( string name, List<DrawnShape> shapes, Dictionary<string, IReadOnlyList<EmblemPart>> emblems, out string resolved )
	{
		resolved = name;
		if ( name is null || name.Length < 2 || name[ 0 ] != DraftMark && name[ 0 ] != DrawnShape.ReferencePrefix )
			return true;

		var index = DraftHandle( name ) - Handles.DraftBase;
		if ( index < 0 )
			return true;

		var shape = Make( index );
		if ( shape is null )
			return Fail( $"The shape \"{_drafts[ index ].Name}\" has nothing to draw" ) >= 0;

		resolved = shape.Reference;
		if ( !shapes.Exists( x => x.Id == shape.Id ) )
			shapes.Add( shape );

		if ( _drafts[ index ].Parts.Count > 0 )
			emblems[ shape.Id ] = Parts( _drafts[ index ] );

		return true;
	}

	// A draft as a drawn shape (an emblem without lines of its own drawn as all its layers' outlines); null for nothing
	private DrawnShape Make( int index )
	{
		var draft = _drafts[ index ];
		if ( draft.IsMade )
			return draft.Made;

		if ( draft.Pending.Count > 0 )
			EndStroke( Handles.DraftBase + index, false );

		var strokes = draft.Strokes;
		if ( strokes.Count == 0 && draft.Parts.Count > 0 )
		{
			var union = new Draft();
			foreach ( var part in draft.Parts )
				AddPartOutline( union, _parts[ part ] );

			strokes = union.Strokes;
		}

		draft.Made = strokes.Count > 0 ? DrawnShape.From( strokes, string.IsNullOrEmpty( draft.Name ) ? null : draft.Name, draft.Sticker?.Sanitized() ) : null;
		draft.IsMade = true;
		return draft.Made;
	}

	private void AddPartOutline( Draft union, EmblemPart part )
	{
		if ( part.Hidden || StrokesOf( part.ShapeHandle ) is not { } strokes )
			return;

		var flipped = new List<ShapeStroke>();
		foreach ( var stroke in strokes )
		{
			flipped.Add( stroke.Mapped( ( x, y ) => (part.FlipX ? -x : x, part.FlipY ? -y : y) ) );
		}

		// An emblem past the point cap keeps the outlines that fit; its layers stay whole
		AddStrokes( union, flipped, part.X, part.Y, part.ScaleX, part.ScaleY, part.Rotation );
		Error = null;
	}

	private IReadOnlyList<EmblemPart> Parts( Draft draft )
	{
		var parts = new List<EmblemPart>();
		foreach ( var index in draft.Parts )
		{
			var part = _parts[ index ];
			part.Shape = ShapeName( part.ShapeHandle, out var name ) && name[ 0 ] != DraftMark ? name
				: Make( part.ShapeHandle - Handles.DraftBase )?.Reference ?? nameof( StandardShape.Circle );

			parts.Add( part );
		}

		return parts;
	}

	private ScriptBuild Failed( string message ) => ScriptBuild.Failed( _program.Lines.Error( message, 0, 1 ) );

	/// <summary>Fills the machine's live layer block from the built layers: what the effect's functions read and change while it runs.</summary>
	public void FillLive()
	{
		var size = ScriptProperties.LiveBlockSize;
		_machine.Layers = _layers.Count;
		_machine.Live = new double[ _layers.Count * size ];
		_machine.LiveSet = new ulong[ _layers.Count ];
		for ( var layer = 0; layer < _layers.Count; layer++ )
		{
			foreach ( var property in ScriptProperties.All )
			{
				if ( property.Owner == TypeKind.Layer )
					GetLayer( property.Id, layer, _layers[ layer ], _machine.Live, layer * size + ScriptProperties.LiveOffset[ (int)property.Id ] );
			}
		}
	}
}
