using System;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// A beam layer (see <see cref="LayerKind.Beam"/>): a ribbon drawn through points it keeps, in index order, spread along
/// where the layer comes from (a line to another joint: a bolt between the hands; a ring: a portal's edge). Its colour
/// and width go from the layer's start to its end along it; its effect's particle function, when it has one, runs for each
/// point every frame and can move, colour, fade and size each (lightning that jitters, a helix that turns). Drawn as one
/// line object, rebuilt each frame without allocating. Made only by effect layers, never saved: public only as the
/// scene's components are.
/// </summary>
public sealed class EffectBeam : Component, Component.ExecuteInEditor
{
	// How far from the effect's place a particle function can put a point, each way (see Particle.Position)
	private const float PositionReach = 400f;

	// How far a particle function moves a point from its place, each way, in the layer's own units
	private const float MostOffset = 96f;

	private SceneLineObject _line;
	private float[] _places = Array.Empty<float>();
	private Vector3[] _drawn = Array.Empty<Vector3>();
	private Color[] _colours = Array.Empty<Color>();
	private float[] _widths = Array.Empty<float>();
	private int _count;
	private bool _isLine;
	private bool _toJoint;
	private float _spread;
	private Color _colour = Color.White;
	private Color _colourEnd = Color.White;
	private float _width = 1f;
	private float _widthEnd = 1f;
	private float _lifetime = 1f;
	private float _share = 1f;
	private float _started;
	private bool _additive = true;

	/// <summary>
	/// Sets it up as <paramref name="data"/> says: its points (<paramref name="places"/>, from
	/// <see cref="KeptPlaces.Of"/>), colours from <paramref name="colour"/> to <paramref name="colourEnd"/>, and widths
	/// sized by <paramref name="scale"/>.
	/// </summary>
	internal void Configure( EffectLayer data, Color colour, Color colourEnd, float scale, float[] places )
	{
		_places = places ?? Array.Empty<float>();
		_count = _places.Length / 3;
		if ( _drawn.Length < _count )
		{
			_drawn = new Vector3[ _count ];
			_colours = new Color[ _count ];
			_widths = new float[ _count ];
		}

		_isLine = data.ShapeIs == LayerShape.Line;
		_toJoint = data.ToJoint is not null;
		_spread = data.Spread;
		_colour = colour;
		_colourEnd = colourEnd;
		_width = data.Size * scale;
		_widthEnd = (data.SizeEnd ?? data.Size) * scale;
		_lifetime = data.Lifetime;
		_additive = !data.Solid;
		_started = RealTime.Now;
		if ( _line.IsValid() )
			UseLook();
	}

	/// <summary>Shows it while <paramref name="share"/> is above nothing.</summary>
	internal void Share( float share )
	{
		_share = share;
	}

	/// <summary>Takes its script's changes to its colours and widths (sized by <paramref name="scale"/>).</summary>
	internal void ApplyLive( ScriptMachine machine, int layer, float scale )
	{
		if ( machine.IsLiveChanged( layer, PropertyId.LayerColor ) )
			_colour = new Color( (float)machine.LiveValue( layer, PropertyId.LayerColor ), (float)machine.LiveValue( layer, PropertyId.LayerColor, 1 ), (float)machine.LiveValue( layer, PropertyId.LayerColor, 2 ) );

		if ( machine.IsLiveChanged( layer, PropertyId.LayerColorEnd ) )
			_colourEnd = new Color( (float)machine.LiveValue( layer, PropertyId.LayerColorEnd ), (float)machine.LiveValue( layer, PropertyId.LayerColorEnd, 1 ), (float)machine.LiveValue( layer, PropertyId.LayerColorEnd, 2 ) );

		if ( machine.IsLiveChanged( layer, PropertyId.LayerSize ) )
			_width = (float)machine.LiveValue( layer, PropertyId.LayerSize ) * scale;

		if ( machine.IsLiveChanged( layer, PropertyId.LayerSizeEnd ) )
			_widthEnd = (float)machine.LiveValue( layer, PropertyId.LayerSizeEnd ) * scale;
	}

	/// <summary>
	/// Draws it for this frame, its layer placed at <paramref name="place"/> (a line running to <paramref name="lineEnd"/>),
	/// shown this much (<paramref name="visible"/>, 0 to 1); <paramref name="run"/> runs layer
	/// <paramref name="layer"/>'s particle function for each point when it has one.
	/// </summary>
	internal void Place( Transform place, Vector3 lineEnd, float now, float visible, EffectScriptRun run, int layer )
	{
		if ( _count < 2 || !Active )
			return;

		var shown = visible * (_share > 0f ? 1f : 0f);
		if ( !_line.IsValid() )
		{
			_line = new SceneLineObject( Scene.SceneWorld );
			UseLook();
		}

		_line.RenderingEnabled = shown > 0f;
		if ( shown <= 0f )
			return;

		var life = _lifetime > 0f ? (now - _started) / _lifetime % 1f : 0f;
		var runs = run is not null && layer >= 0 && run.Machine.HasParticleFunction( layer );
		for ( var i = 0; i < _count; i++ )
		{
			var along = i / (float)(_count - 1);
			var at = i * 3;
			var origin = new Vector3( _places[ at ], _places[ at + 1 ], _places[ at + 2 ] );
			// A line runs to its end joint, or across the way it faces
			var position = !_isLine ? place.PointToWorld( origin )
				: _toJoint ? Vector3.Lerp( place.Position, lineEnd, _places[ at ] )
				: place.PointToWorld( Vector3.Lerp( Vector3.Backward * _spread, Vector3.Forward * _spread, _places[ at ] ) );
			var colour = Color.Lerp( _colour, _colourEnd, along );
			var width = MathX.Lerp( _width, _widthEnd, along );
			var alpha = 1f;
			if ( runs && run.CanRun )
				RunPoint( run, layer, i, life, now, origin, place, ref position, ref colour, ref alpha, ref width );

			_drawn[ i ] = position;
			_colours[ i ] = colour.WithAlpha( colour.a * alpha * shown );
			_widths[ i ] = Math.Clamp( width, 0f, EffectLayerObject.MaxDrawnSize );
		}

		_line.StartLine();
		for ( var i = 0; i < _count; i++ )
			_line.AddLinePoint( _drawn[ i ], _colours[ i ], _widths[ i ] );

		_line.EndLine();
	}

	// One point through the particle function, as a sprite would be: its index, life, seed (fixed by its index) and place
	private void RunPoint( EffectScriptRun run, int layer, int index, float life, float now, Vector3 origin, Transform place, ref Vector3 position,
		ref Color colour, ref float alpha, ref float width )
	{
		var io = run.IO;
		io.Set( ScriptInput.Index, index );
		io.Set( ScriptInput.Count, _count );
		io.Set( ScriptInput.Age, now - _started );
		io.Set( ScriptInput.Life, life );
		io.Set( ScriptInput.Seed, index * 0.6180339887 % 1 );
		io.Set( ScriptInput.OriginX, origin.x, origin.y, origin.z );
		var outputs = io.Outputs;
		(outputs[ 0 ], outputs[ 1 ], outputs[ 2 ], outputs[ 3 ]) = (colour.r, colour.g, colour.b, colour.a);
		outputs[ (int)ScriptOutput.Alpha ] = 1;
		outputs[ (int)ScriptOutput.Size ] = 1;
		outputs[ (int)ScriptOutput.Rotation ] = 0;
		(outputs[ (int)ScriptOutput.OffsetX ], outputs[ (int)ScriptOutput.OffsetY ], outputs[ (int)ScriptOutput.OffsetZ ]) = (0, 0, 0);
		(outputs[ (int)ScriptOutput.PositionX ], outputs[ (int)ScriptOutput.PositionY ], outputs[ (int)ScriptOutput.PositionZ ]) = (position.x, position.y, position.z);
		outputs[ (int)ScriptOutput.Frame ] = 0;
		io.OutputsSet = 0;
		if ( !run.RunParticle( layer ) )
			return;

		var set = io.OutputsSet;
		if ( (set & ScriptIO.ColorSet) != 0 )
			colour = new Color( (float)outputs[ 0 ], (float)outputs[ 1 ], (float)outputs[ 2 ], (float)outputs[ 3 ] );

		if ( (set & ScriptIO.AlphaSet) != 0 )
			alpha = (float)outputs[ (int)ScriptOutput.Alpha ];

		if ( (set & ScriptIO.SizeSet) != 0 )
			width *= (float)outputs[ (int)ScriptOutput.Size ];

		if ( (set & ScriptIO.PositionSet) != 0 )
		{
			var anchor = new Vector3( (float)io.Inputs[ (int)ScriptInput.AnchorPositionX ], (float)io.Inputs[ (int)ScriptInput.AnchorPositionY ],
				(float)io.Inputs[ (int)ScriptInput.AnchorPositionZ ] );
			position = new Vector3(
				Math.Clamp( (float)outputs[ (int)ScriptOutput.PositionX ], anchor.x - PositionReach, anchor.x + PositionReach ),
				Math.Clamp( (float)outputs[ (int)ScriptOutput.PositionY ], anchor.y - PositionReach, anchor.y + PositionReach ),
				Math.Clamp( (float)outputs[ (int)ScriptOutput.PositionZ ], anchor.z - PositionReach, anchor.z + PositionReach ) );
		}

		if ( (set & ScriptIO.OffsetSet) != 0 )
		{
			var offset = new Vector3(
				Math.Clamp( (float)outputs[ (int)ScriptOutput.OffsetX ], -MostOffset, MostOffset ),
				Math.Clamp( (float)outputs[ (int)ScriptOutput.OffsetY ], -MostOffset, MostOffset ),
				Math.Clamp( (float)outputs[ (int)ScriptOutput.OffsetZ ], -MostOffset, MostOffset ) );
			position += place.Rotation * offset * place.Scale.x;
		}
	}

	// Seen from every side, glowing over what is behind it (or covering it, solid), unlit, as the line renderer draws
	private void UseLook()
	{
		_line.Face = SceneLineObject.FaceMode.Camera;
		_line.Lighting = false;
		_line.Opaque = false;
		_line.Attributes.Set( "g_tColor", Texture.White );
		_line.Attributes.SetCombo( "D_BLEND", _additive ? 1 : 0 );
		_line.Attributes.SetCombo( "D_OPAQUE", 0 );
		_line.Flags.IsOpaque = false;
		_line.Flags.IsTranslucent = true;
		_line.Flags.CastShadows = false;
	}

	protected override void OnDisabled()
	{
		if ( _line.IsValid() )
			_line.RenderingEnabled = false;
	}

	protected override void OnDestroy()
	{
		_line?.Delete();
		_line = null;
	}
}
