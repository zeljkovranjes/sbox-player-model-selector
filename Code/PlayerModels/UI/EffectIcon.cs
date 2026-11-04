using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using PlayerModels.Engine;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// A tile's picture of an effect: the pictures of its first few sprite and trail layers in their colours, over one
/// another, each moving the way its layer does (pulsing, flickering, turning, rising or falling, a trail sweeping past),
/// and a colour cycle going through its colours as it does on the body, so the tile shows what it does.
/// </summary>
public sealed class EffectIcon : Panel
{
	// More than this and the tile is a blur
	private const int MostPictures = 3;

	private const float MillisecondsPerSecond = 1000f;

	/// <summary>The effect pictured.</summary>
	[Parameter] public Effect Effect { get; set; }

	// A picture whose colours cycle: its panel, its layer's mode, its colours and its place among the pictures
	private sealed record Cycling( Panel Picture, MovingColour Colour, int Index );

	private readonly List<Cycling> _cycling = new();
	private Effect _shown;

	// Pictures of drawn shapes the effect carries (see ShapeTextures): a circle until each is drawn, asked for again
	// whenever the textures change
	private readonly List<(Panel Picture, DrawnShape Shape)> _drawn = new();
	private int _generation = -1;

	public override void Tick()
	{
		base.Tick();

		if ( !ReferenceEquals( Effect, _shown ) )
			Show( Effect );

		if ( _drawn.Count > 0 )
			ShowDrawn();

		var ms = (long)(RealTime.Now * MillisecondsPerSecond);
		foreach ( var cycling in _cycling )
		{
			var (r, g, b) = cycling.Colour.At( ms, cycling.Index, MostPictures );
			cycling.Picture.Style.BackgroundTint = new Color( r, g, b, 1f );
		}
	}

	private void ShowDrawn()
	{
		ShapeTextures.Pump();
		foreach ( var (picture, shape) in _drawn )
		{
			ShapeTextures.Count( shape.Content, 0f );
			if ( shape.Sticker is { IsAnimated: true } sticker )
				picture.Style.BackgroundTint = ShapeIcon.FillAt( sticker, RealTime.Now );
		}

		if ( _generation == ShapeTextures.Generation )
			return;

		_generation = ShapeTextures.Generation;
		foreach ( var (picture, shape) in _drawn )
			picture.Style.BackgroundImage = ShapeTextures.TextureOf( shape ) ?? EffectAtlas.TextureOf( StandardShape.Circle );
	}

	private void Show( Effect effect )
	{
		_shown = effect;
		_cycling.Clear();
		_drawn.Clear();
		_generation = -1;
		DeleteChildren( true );
		if ( effect is null )
			return;

		var shown = 0;
		foreach ( var layer in effect.Layers )
		{
			if ( layer.KindIs == LayerKind.Light || shown >= MostPictures )
				continue;

			var picture = AddChild<Panel>( $"picture picture-{shown} {MotionOf( layer )}" );
			if ( effect.Shapes?.Find( x => x.Id == layer.DrawnPicture ) is { } drawn )
				_drawn.Add( (picture, drawn) );
			else
				picture.Style.BackgroundImage = EffectAtlas.TextureOf( layer );

			picture.Style.BackgroundTint = Color.Parse( layer.Color ) ?? Color.White;

			// A tile's pictures count as the cycle's elements; mixed colours scroll as rain does, to show they change
			var mode = layer.ColorModeIs == LayerColorMode.Mix ? LayerColorMode.Rain : layer.ColorModeIs;
			if ( mode != LayerColorMode.Fixed && MovingColour.Of( PresetScripts.ColorModeScript( mode ), layer.CycleColours(), ScriptPrograms.Cache, ScriptPrograms.Stack ) is { } colour )
				_cycling.Add( new Cycling( picture, colour, shown ) );

			shown++;
		}
	}

	// The animation a layer's picture plays on the tile (see EffectsEditor.razor.scss)
	private static string MotionOf( EffectLayer layer )
	{
		if ( layer.KindIs == LayerKind.Trail )
			return "sweep";

		return layer.RhythmIs switch
		{
			LayerRhythm.Pulse => "pulse",
			LayerRhythm.Flicker => "flicker",
			LayerRhythm.Spin => "spin",
			_ when layer.Gravity < 0f => "rise",
			_ when layer.Gravity > 0f => "fall",
			_ => "glow",
		};
	}
}
