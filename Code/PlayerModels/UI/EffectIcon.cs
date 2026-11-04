using System.Collections.Generic;
using PlayerModels.Core;
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
	private sealed record Cycling( Panel Picture, LayerColorMode Mode, Color[] Colours, int Index );

	private readonly List<Cycling> _cycling = new();
	private Effect _shown;

	public override void Tick()
	{
		base.Tick();

		if ( !ReferenceEquals( Effect, _shown ) )
			Show( Effect );

		var ms = (long)(RealTime.Now * MillisecondsPerSecond);
		foreach ( var cycling in _cycling )
			cycling.Picture.Style.BackgroundTint = ColourOf( cycling, ms );
	}

	private void Show( Effect effect )
	{
		_shown = effect;
		_cycling.Clear();
		DeleteChildren( true );
		if ( effect is null )
			return;

		var shown = 0;
		foreach ( var layer in effect.Layers )
		{
			if ( layer.KindIs == LayerKind.Light || shown >= MostPictures )
				continue;

			var picture = AddChild<Panel>( $"picture picture-{shown} {MotionOf( layer )}" );
			picture.Style.BackgroundImage = EffectAtlas.TextureOf( layer );
			picture.Style.BackgroundTint = Color.Parse( layer.Color ) ?? Color.White;

			if ( layer.ColorModeIs != LayerColorMode.Fixed )
			{
				var colours = layer.CycleColours();
				var parsed = new Color[ colours.Count ];
				for ( var i = 0; i < colours.Count; i++ )
					parsed[ i ] = Color.Parse( colours[ i ] ) ?? Color.White;

				_cycling.Add( new Cycling( picture, layer.ColorModeIs, parsed, shown ) );
			}

			shown++;
		}
	}

	// The colour the cycle gives this picture now, the pictures counted as its elements
	private static Color ColourOf( Cycling cycling, long ms )
	{
		if ( cycling.Mode == LayerColorMode.Cyln )
			return ColorCycle.IsScannerHighlight( ms, cycling.Index, MostPictures ) ? cycling.Colours[ 0 ] : cycling.Colours[ 1 ];

		var kind = cycling.Mode == LayerColorMode.Cycl ? ColorCycle.Kind.Cycl : ColorCycle.Kind.Rain;
		var index = ColorCycle.PaletteIndex( kind, ms, cycling.Index, MostPictures, cycling.Colours.Length );
		return index >= 0 ? cycling.Colours[ index ] : Color.White;
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
