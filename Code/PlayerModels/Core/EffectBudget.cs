using System;
using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>
/// How much of their effects a player shows: how many layers and lights, and how many sprites out at once. The host
/// picks one for everyone on their server (see <see cref="Level"/>), and each player can lower it for what they see of
/// others (see <see cref="Quality"/>). Past it the later layers and lights of a look wait and its sprites thin out;
/// nothing is taken from the look itself, so all of it shows again once the budget allows.
/// </summary>
public readonly record struct EffectBudget( int Layers, int Lights, int Sprites )
{
	/// <summary>
	/// The budgets a host can pick. Normal first: it is what a selector kept by a hot reload reads, as it was made
	/// before the budget was.
	/// </summary>
	public enum Level { Normal, Low, High, Custom }

	/// <summary>
	/// How much of others' effects a player sees. High first, which is as much as the host allows: a player who never
	/// set it sees that.
	/// </summary>
	public enum Quality { High, Normal, Low, Off }

	/// <summary>What <see cref="Normal"/> allows, as constants for defaults.</summary>
	public const int NormalLayers = 16;
	public const int NormalLights = 4;
	public const int NormalSprites = 400;

	/// <summary>
	/// The budgets a host picks from; a custom one is within <see cref="Most"/>. Normal fits any three presets worn
	/// together.
	/// </summary>
	public static readonly EffectBudget Low = new( 6, 1, 150 );
	public static readonly EffectBudget Normal = new( NormalLayers, NormalLights, NormalSprites );
	public static readonly EffectBudget High = new( 24, 6, 1000 );
	public static readonly EffectBudget Most = new( Effect.MaxLookLayers, 8, 2000 );

	/// <summary>No effects at all.</summary>
	public static readonly EffectBudget None = new( 0, 0, 0 );

	/// <summary>The budget <paramref name="level"/> means; a custom one is the numbers given, within <see cref="Most"/>.</summary>
	public static EffectBudget Of( Level level, int layers, int lights, int sprites ) => level switch
	{
		Level.Low => Low,
		Level.High => High,
		Level.Custom => new EffectBudget( layers, lights, sprites ).Within(),
		_ => Normal,
	};

	/// <summary>The most a player who picked <paramref name="quality"/> sees of anyone's effects.</summary>
	public static EffectBudget Of( Quality quality ) => quality switch
	{
		Quality.Off => None,
		Quality.Low => Low,
		Quality.Normal => Normal,
		_ => Most,
	};

	/// <summary>This budget with each number between none and <see cref="Most"/>'s.</summary>
	public EffectBudget Within() => new( Math.Clamp( Layers, 0, Most.Layers ), Math.Clamp( Lights, 0, Most.Lights ), Math.Clamp( Sprites, 0, Most.Sprites ) );

	/// <summary>The smaller of this budget and <paramref name="other"/>, number by number.</summary>
	public EffectBudget Min( EffectBudget other ) => new( Math.Min( Layers, other.Layers ), Math.Min( Lights, other.Lights ), Math.Min( Sprites, other.Sprites ) );

	/// <summary>Whether <paramref name="effects"/> show whole within this budget: the switched off ones (and layers) don't count.</summary>
	public bool Fits( IReadOnlyList<Effect> effects )
	{
		var layers = 0;
		var lights = 0;
		var sprites = 0f;
		foreach ( var effect in effects )
		{
			if ( effect.Off )
				continue;

			layers += effect.LayersOn();
			lights += effect.Lights();
			sprites += effect.LiveSprites();
		}

		return layers <= Layers && lights <= Lights && sprites <= Sprites;
	}

	/// <summary>
	/// Which layers of <paramref name="effects"/> wait past this budget, by effect and then layer: layers past the budget's,
	/// first to last, and lights past its lights. Switched off effects and layers never wait (they don't show anyway).
	/// </summary>
	public bool[][] HeldBack( IReadOnlyList<Effect> effects )
	{
		var held = new bool[ effects.Count ][];
		var layers = 0;
		var lights = 0;
		for ( var e = 0; e < effects.Count; e++ )
		{
			var effect = effects[ e ];
			held[ e ] = new bool[ effect.Layers.Count ];
			if ( effect.Off )
				continue;

			for ( var l = 0; l < effect.Layers.Count; l++ )
			{
				var layer = effect.Layers[ l ];
				if ( layer.Off )
					continue;

				var isLight = layer.KindIs == LayerKind.Light;
				if ( layers >= Layers || isLight && lights >= Lights )
				{
					held[ e ][ l ] = true;
					continue;
				}

				layers++;
				lights += isLight ? 1 : 0;
			}
		}

		return held;
	}

	/// <summary>
	/// Trims <paramref name="effects"/> (changed in place, so pass a copy) to this budget: the layers that wait (see
	/// <see cref="HeldBack"/>) are left out, and if the sprites still come to more than its sprites, every layer has fewer
	/// in proportion.
	/// </summary>
	public void Trim( List<Effect> effects )
	{
		var held = HeldBack( effects );
		for ( var e = 0; e < effects.Count; e++ )
		{
			for ( var l = effects[ e ].Layers.Count - 1; l >= 0; l-- )
			{
				if ( held[ e ][ l ] )
					effects[ e ].Layers.RemoveAt( l );
			}
		}

		var sprites = 0f;
		foreach ( var effect in effects )
			sprites += effect.Off ? 0f : effect.LiveSprites();

		if ( sprites <= Sprites )
			return;

		var thinning = Sprites / sprites;
		foreach ( var effect in effects )
		{
			foreach ( var layer in effect.Layers )
			{
				if ( layer.KindIs == LayerKind.Sprite )
					layer.Amount *= thinning;
			}
		}
	}

	/// <summary>
	/// Shares out <paramref name="total"/> between bodies that want <paramref name="wanted"/> each, nearest first: each
	/// gets all it wants while there is enough, so the furthest go without first. <paramref name="shares"/> gets the part
	/// of what it wants each gets, 0 to 1. Runs for every body, a few times a second: no allocations.
	/// </summary>
	public static void Share( ReadOnlySpan<float> wanted, float total, Span<float> shares )
	{
		var left = total;
		for ( var i = 0; i < wanted.Length; i++ )
		{
			var given = Math.Clamp( left, 0f, wanted[ i ] );
			shares[ i ] = wanted[ i ] > 0f ? given / wanted[ i ] : 1f;
			left -= given;
		}
	}
}
