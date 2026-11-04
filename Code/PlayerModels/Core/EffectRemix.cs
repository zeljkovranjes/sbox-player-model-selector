using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PlayerModels.Core;

/// <summary>
/// Effects made up for the player to try: a surprise put together from the layers of effects there are (the presets,
/// their own), within what the server's budget has left, and variations of an effect (its colours turned, sizes, amounts
/// and speeds nudged, a rhythm or a picture swapped) to pick from. The randomness is passed in, so the same seed makes
/// the same effects.
/// </summary>
public static class EffectRemix
{
	/// <summary>The most layers a surprise has.</summary>
	public const int MaxSurpriseLayers = 3;

	/// <summary>How many variations are offered.</summary>
	public const int VariationCount = 4;

	// How often a surprise is tried again before giving up when what it picked doesn't fit
	private const int Tries = 24;

	// How far a variation nudges each slider either way, and how much it turns the colours at least
	private const float Nudge = 0.4f;
	private const float LeastTurn = 0.12f;

	// The pictures a variation swaps between: bright ones that read alike, so smoke stays smoke and flames stay flames
	private static readonly string[] Swappable =
	{
		nameof( LayerPicture.Glow ), nameof( LayerPicture.Spark ), nameof( LayerPicture.Sparkle ), nameof( LayerPicture.Heart ),
		nameof( LayerPicture.Snowflake ), nameof( StandardShape.Star5 ), nameof( StandardShape.Star4 ), nameof( StandardShape.Diamond ),
	};

	private static readonly string[] Rhythms =
	{
		nameof( LayerRhythm.Steady ), nameof( LayerRhythm.Pulse ), nameof( LayerRhythm.Flicker ), nameof( LayerRhythm.Spin ), nameof( LayerRhythm.Jiggle ),
	};

	/// <summary>
	/// A new effect of one to <see cref="MaxSurpriseLayers"/> layers picked at random from <paramref name="sources"/>, each
	/// kept on the joint it came from, its colours turned at random, to wear beside <paramref name="worn"/> within
	/// <paramref name="budget"/> (its sprites thinned to fit); null when there is no room left, or nothing to pick from.
	/// </summary>
	public static Effect Surprise( Random random, IReadOnlyList<Effect> sources, IReadOnlyList<Effect> worn, EffectBudget budget )
	{
		var parts = new List<EffectLayer>();
		foreach ( var source in sources )
		{
			if ( source?.Sanitized() is not { } effect )
				continue;

			foreach ( var layer in effect.Layers )
			{
				// A drawn shape belongs to its effect: parts are made of the standard ones
				if ( layer.DrawnFormation is not null || layer.DrawnPicture is not null )
					continue;

				// Each part keeps the joint it showed on; a layer that came from an item or the weapon gets that joint
				var part = Copy( layer );
				part.Joint = effect.JointOf( layer ).ToString();
				part.Off = false;
				parts.Add( part );
			}
		}

		var (layersOn, lightsOn, layersWorn) = (0, 0, 0);
		foreach ( var effect in worn )
		{
			layersWorn += effect.Layers.Count;
			if ( effect.Off )
				continue;

			layersOn += effect.LayersOn();
			lightsOn += effect.Lights();
		}

		var room = Math.Min( Math.Min( budget.Layers - layersOn, Effect.MaxLookLayers - layersWorn ), MaxSurpriseLayers );
		if ( parts.Count == 0 || room <= 0 || worn.Count >= Effect.MaxWorn )
			return null;

		for ( var tried = 0; tried < Tries; tried++ )
		{
			var count = random.Next( 1, room + 1 );
			var picked = new List<EffectLayer>();
			var lights = lightsOn;
			for ( var i = 0; i < count; i++ )
			{
				var part = parts[ random.Next( parts.Count ) ];
				if ( part.KindIs == LayerKind.Light && lights >= budget.Lights )
					continue;

				lights += part.KindIs == LayerKind.Light ? 1 : 0;
				picked.Add( Copy( part ) );
			}

			if ( picked.Count == 0 )
				continue;

			// Every layer on its own joint, the effect on the first's, so it reads as one thing put together
			var joint = picked[ 0 ].Joint;
			foreach ( var layer in picked )
				layer.Joint = layer.Joint == joint ? null : layer.Joint;

			var surprise = new Effect
			{
				Name = "Surprise", Anchor = new EffectAnchor { Joint = joint }, Layers = picked, Hue = (float)random.NextDouble(),
			}.Sanitized();

			if ( surprise is null )
				continue;

			surprise.Amount = AmountToFit( surprise, worn, budget );
			if ( budget.Fits( With( worn, surprise ) ) )
				return surprise;
		}

		return null;
	}

	/// <summary>
	/// <see cref="VariationCount"/> variations of <paramref name="effect"/>, each with the same id and name: its colours
	/// turned round the wheel (each its own way), its size, amount and speed nudged, and, now and then, one layer's rhythm
	/// or picture swapped.
	/// </summary>
	public static List<Effect> Variations( Effect effect, Random random )
	{
		var variations = new List<Effect>();
		if ( effect?.Sanitized() is not { } original )
			return variations;

		for ( var i = 0; i < VariationCount; i++ )
		{
			var variation = Copy( original );

			// Spread round the wheel so no two look alike, and never the colours it has
			var turn = LeastTurn + (1f - 2f * LeastTurn) * (i + (float)random.NextDouble()) / VariationCount;
			variation.Hue = Fraction( original.Hue + turn );
			variation.Size = Slider( original.Size + Between( random, -Nudge, Nudge ) );
			variation.Amount = Slider( original.Amount + Between( random, -Nudge, Nudge ) );
			variation.Speed = Slider( original.Speed + Between( random, -Nudge, Nudge ) );

			var layer = variation.Layers[ random.Next( variation.Layers.Count ) ];
			if ( random.Next( 2 ) == 0 )
				layer.Rhythm = layer.KindIs == LayerKind.Light ? nameof( LayerRhythm.Pulse ) : Rhythms[ random.Next( Rhythms.Length ) ];
			else if ( Array.IndexOf( Swappable, layer.Picture ?? nameof( LayerPicture.Glow ) ) >= 0 )
				layer.Picture = Swappable[ random.Next( Swappable.Length ) ];

			// A rhythm picked without a beat would stand still
			if ( layer.RhythmIs != LayerRhythm.Steady && layer.RhythmSpeed <= 0f )
			{
				layer.RhythmSpeed = 1f;
				layer.RhythmAmount = layer.RhythmIs == LayerRhythm.Spin ? 1f : 0.4f;
			}

			variations.Add( variation.Sanitized() ?? original );
			variations[ ^1 ].Id = original.Id;
		}

		return variations;
	}

	// The amount slider that brings the effect's sprites within what the budget has left beside those worn
	private static float AmountToFit( Effect effect, IReadOnlyList<Effect> worn, EffectBudget budget )
	{
		var used = 0f;
		foreach ( var other in worn )
			used += other.Off ? 0f : other.LiveSprites();

		var sprites = effect.LiveSprites();
		var left = budget.Sprites - used;
		if ( sprites <= 0f || sprites <= left )
			return effect.Amount;

		// Amount multiplies sprites by AmountRange to its power: the power that makes them fit, no lower than the slider goes
		var factor = MathF.Max( left, 1f ) / sprites * effect.AmountFactor;
		return Slider( MathF.Log( factor ) / MathF.Log( Effect.AmountRange ) );
	}

	private static List<Effect> With( IReadOnlyList<Effect> worn, Effect effect )
	{
		var all = new List<Effect>( worn ) { effect };
		return all;
	}

	private static T Copy<T>( T value ) => JsonSerializer.Deserialize<T>( JsonSerializer.Serialize( value ) );

	private static float Between( Random random, float least, float most ) => least + (most - least) * (float)random.NextDouble();

	private static float Slider( float value ) => Math.Clamp( value, -1f, 1f );

	private static float Fraction( float value ) => value - MathF.Floor( value );
}
