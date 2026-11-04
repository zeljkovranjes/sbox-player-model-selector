using System;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// A tile's picture of a drawn shape (see <see cref="DrawnShape"/>): drawn from its lines as bodies show it (see
/// <see cref="ShapeTextures"/>), a plain circle until it is, and a sticker's animated fill going round as it does on a
/// body. Nothing is uploaded or downloaded for it: every machine draws it the same.
/// </summary>
public sealed class ShapeIcon : Panel
{
	// A rainbow sticker turns round the wheel this often a second, a pulsing one beats this often and this deep, at speed 1;
	// as on a body (see Sticker.Drive)
	private const float RainbowTurns = 0.5f;
	private const float PulseBeats = 1f;
	private const float PulseDepth = 0.5f;
	private const float MillisecondsPerSecond = 1000f;

	/// <summary>The shape pictured.</summary>
	[Parameter] public DrawnShape Shape { get; set; }

	private DrawnShape _shown;
	private DrawnShape _second;
	private int _generation = -1;

	// A finish shimmers while its tile is pointed at, as a sticker turned in the hand does: holo goes round the wheel,
	// foil and gold catch the light, glitter twinkles, a lenticular one swaps its looks this often
	private const float HoloTurns = 0.4f;
	private const float ShineBeats = 0.8f;
	private const float GlitterSteps = 12f;
	private const float LenticularSeconds = 0.8f;
	private static readonly Color Gold = new( 1f, 0.82f, 0.45f );

	public override void Tick()
	{
		base.Tick();

		var shape = Shape;
		if ( shape is null )
		{
			Style.BackgroundImage = null;
			return;
		}

		// Kept while it shows here; asked for again whenever the textures change, until it is drawn
		ShapeTextures.Pump();
		ShapeTextures.Count( shape.Content, 0f );
		var finish = shape.FinishIs;
		var pointed = finish != StickerFinish.Paper && (HasHovered || Parent?.HasHovered == true);
		var secondLook = pointed && finish == StickerFinish.Lenticular && RealTime.Now % (2f * LenticularSeconds) > LenticularSeconds;
		if ( !ReferenceEquals( shape, _shown ) )
			_second = shape.SecondLook();

		if ( !ReferenceEquals( shape, _shown ) || _generation != ShapeTextures.Generation || secondLook != _showsSecond )
		{
			_shown = shape;
			_showsSecond = secondLook;
			_generation = ShapeTextures.Generation;
			var first = ShapeTextures.TextureOf( shape ) ?? EffectAtlas.TextureOf( StandardShape.Circle );
			Style.BackgroundImage = secondLook && _second is not null ? ShapeTextures.TextureOf( _second ) ?? first : first;
		}

		var tint = shape.Sticker is { IsAnimated: true } sticker ? FillAt( sticker, RealTime.Now ) : Color.White;
		Style.BackgroundTint = pointed ? Shimmer( tint, finish, RealTime.Now ) : tint;
	}

	private bool _showsSecond;

	/// <summary><paramref name="tint"/> as a sticker of <paramref name="finish"/> shimmers <paramref name="seconds"/> in, turned in the hand.</summary>
	internal static Color Shimmer( Color tint, StickerFinish finish, float seconds )
	{
		switch ( finish )
		{
			case StickerFinish.Holo:
			{
				var film = new ColorHsv( seconds * HoloTurns % 1f * 360f, 0.6f, 1f, 1f ).ToColor();
				return Color.Lerp( tint, tint * film, 0.6f );
			}

			case StickerFinish.Gold:
				return tint * Gold * (0.9f + 0.25f * MathF.Sin( seconds * ShineBeats * MathF.Tau ));

			case StickerFinish.Glitter:
			{
				var twinkle = MathF.Abs( MathF.Sin( MathF.Floor( seconds * GlitterSteps ) * 12.9898f ) * 43758.5453f ) % 1f;
				return tint * (twinkle > 0.7f ? 1.25f : 0.95f);
			}

			case StickerFinish.Foil:
				return tint * (0.85f + 0.3f * MathF.Sin( seconds * ShineBeats * MathF.Tau ));

			default:
				return tint;
		}
	}

	/// <summary>The colour an animated sticker's fill is <paramref name="seconds"/> into its animation; white for one that isn't.</summary>
	internal static Color FillAt( Sticker sticker, float seconds )
	{
		var fill = Color.Parse( sticker.Fill ) ?? Color.White;
		var time = seconds * sticker.Speed;
		switch ( sticker.AnimationIs )
		{
			case Sticker.Motion.Rainbow:
				return new ColorHsv( time * RainbowTurns % 1f * 360f, 1f, 1f, 1f ).ToColor();

			case Sticker.Motion.Pulse:
			{
				var level = 1f - PulseDepth * (0.5f - 0.5f * MathF.Cos( time * PulseBeats * MathF.Tau ));
				return new Color( fill.r * level, fill.g * level, fill.b * level, 1f );
			}

			case Sticker.Motion.None:
				return Color.White;
		}

		// A cycle through the palette, the sticker one element of as many as the palette has colours
		var colours = sticker.CycleColours();
		var ms = (long)(time * MillisecondsPerSecond);
		if ( sticker.AnimationIs == Sticker.Motion.Cyln )
			return Color.Parse( colours[ ColorCycle.IsScannerHighlight( ms, 0, colours.Count ) ? 0 : 1 ] ) ?? Color.White;

		var kind = sticker.AnimationIs == Sticker.Motion.Cycl ? ColorCycle.Kind.Cycl : ColorCycle.Kind.Rain;
		var index = ColorCycle.PaletteIndex( kind, ms, 0, colours.Count, colours.Count );
		return index >= 0 ? Color.Parse( colours[ index ] ) ?? Color.White : Color.White;
	}
}
