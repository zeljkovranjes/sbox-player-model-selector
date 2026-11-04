using System;
using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Core.Script;
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
	private const float MillisecondsPerSecond = 1000f;

	// The most stickers' motions kept for the tiles showing them: past it they are made again as they are asked for
	private const int MostMotions = 64;

	// Each sticker's motion, made once for it (again when it changes in place, as the shape creator changes it)
	private sealed class Moving
	{
		public int Signature;
		public int Count;
		public MovingColour Colour;
	}

	private static readonly Dictionary<Sticker, Moving> Motions = new();

	/// <summary>The shape pictured.</summary>
	[Parameter] public DrawnShape Shape { get; set; }

	private DrawnShape _shown;
	private int _generation = -1;

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
		if ( !ReferenceEquals( shape, _shown ) || _generation != ShapeTextures.Generation )
		{
			_shown = shape;
			_generation = ShapeTextures.Generation;
			Style.BackgroundImage = ShapeTextures.TextureOf( shape ) ?? EffectAtlas.TextureOf( StandardShape.Circle );
		}

		Style.BackgroundTint = shape.Sticker is { IsAnimated: true } sticker ? FillAt( sticker, RealTime.Now ) : Color.White;
	}

	/// <summary>
	/// The colour an animated sticker's fill is <paramref name="seconds"/> into its animation, as its motion's script works
	/// it out (the sticker one element of as many as its palette has colours); white for one that isn't animated.
	/// </summary>
	internal static Color FillAt( Sticker sticker, float seconds )
	{
		var signature = SignatureOf( sticker );
		if ( !Motions.TryGetValue( sticker, out var moving ) || moving.Signature != signature )
		{
			if ( Motions.Count >= MostMotions )
				Motions.Clear();

			var cycle = sticker.CycleColours();
			moving = new Moving
			{
				Signature = signature, Count = cycle.Count,
				Colour = MovingColour.OfSticker( sticker.AnimationIs, cycle, sticker.Fill, ScriptPrograms.Cache, ScriptPrograms.Stack ),
			};

			Motions[ sticker ] = moving;
		}

		if ( moving.Colour is null )
			return Color.White;

		var (r, g, b) = moving.Colour.At( (long)(seconds * sticker.Speed * MillisecondsPerSecond), 0, moving.Count );
		return new Color( r, g, b, 1f );
	}

	// What its motion depends on, without allocating
	private static int SignatureOf( Sticker sticker )
	{
		var signature = HashCode.Combine( sticker.Animation, sticker.Fill );
		if ( sticker.Palette is { } palette )
		{
			foreach ( var colour in palette )
				signature = HashCode.Combine( signature, colour );
		}

		return signature;
	}
}
