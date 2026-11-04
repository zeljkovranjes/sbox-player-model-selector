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
			case ScriptKind.Weapon: return FinishWeapon();
			default: return new ScriptBuild { Program = _program };
		}
	}

	private ScriptBuild FinishEffect()
	{
		if ( _layers.Count == 0 )
			return Failed( "An effect needs at least one layer: add one with Sprite(), Light(), Trail(), Beam() or Ghost()" );

		var shapes = new List<DrawnShape>();
		var emblems = new Dictionary<string, IReadOnlyList<EmblemPart>>();
		foreach ( var layer in _layers )
		{
			if ( !Resolve( layer.Formation, shapes, emblems, out var formation ) || !Resolve( layer.Picture, shapes, emblems, out var picture ) )
				return Failed( Error );

			(layer.Formation, layer.Picture) = (formation, picture);
			for ( var i = 0; i < (layer.Pictures?.Count ?? 0); i++ )
			{
				if ( !Resolve( layer.Pictures[ i ], shapes, emblems, out var each ) )
					return Failed( Error );

				layer.Pictures[ i ] = each;
			}
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

		if ( draft.Own is { } own && !draft.Edited && draft.Strokes.Count == draft.OwnStrokes && draft.Parts.Count == 0
			&& draft.FadeFrom == own.FadeFrom && draft.FadeTo == own.FadeFrom + own.FadeArc )
		{
			(draft.Made, draft.IsMade) = (own, true);
			return own;
		}

		var strokes = draft.Strokes;
		if ( draft.Parts.Count > 0 )
		{
			// Made (as nothing) first: a layer drawing the emblem itself draws a circle rather than going round forever
			draft.IsMade = true;
			draft.Made = MakeEmblem( draft );
			return draft.Made;
		}

		draft.Made = strokes.Count > 0 ? DrawnShape.From( strokes, string.IsNullOrEmpty( draft.Name ) ? null : draft.Name, draft.Sticker?.Sanitized() ) : null;
		if ( draft.FadeFrom != draft.FadeTo )
			draft.Made = draft.Made?.Faded( draft.FadeFrom, draft.FadeTo );

		draft.IsMade = true;
		return draft.Made;
	}

	// A draft with layers as an emblem: a sticker of its layers (lines drawn into it a layer under them all), carrying the
	// drawn shapes they draw, mirrored and printed as it says; null when none of it draws anything
	private DrawnShape MakeEmblem( Draft draft )
	{
		var layers = new List<EmblemLayer>();
		var drawn = new List<DrawnShape>();
		if ( draft.Strokes.Count > 0 && DrawnShape.From( draft.Strokes ) is { } lines )
		{
			drawn.Add( lines );
			layers.Add( new EmblemLayer { Shape = lines.Reference } );
		}

		foreach ( var part in Parts( draft ) )
		{
			layers.Add( part.ToLayer() );
			if ( DrawnShape.IsReference( part.Shape ) && !drawn.Exists( x => x.Reference == part.Shape ) && Made( part.Shape ) is { } shape )
				drawn.Add( shape );
		}

		return new DrawnShape
		{
			Name = string.IsNullOrEmpty( draft.Name ) ? null : draft.Name, Emblem = layers, Parts = drawn.Count > 0 ? drawn : null, Mirror = draft.Mirror,
			Finish = draft.Finish,
		}.Sanitized();
	}

	// The drawn shape the build made with this reference; null for none
	private DrawnShape Made( string reference )
	{
		foreach ( var draft in _drafts )
		{
			if ( draft.IsMade && draft.Made?.Reference == reference )
				return draft.Made;
		}

		return null;
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
