using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PlayerModels.Core.Script;

namespace PlayerModels.Core;

/// <summary>
/// The effects the Effects page offers to start from: the stock presets, each written as a script (see
/// <see cref="PresetScripts"/>), built from it and carrying it, so Edit as code opens exactly that script and every machine
/// builds the preset again from it. Made on the citizen, in its sizes: a body of another size gets them in proportion,
/// and joints a model lacks are stood in for, so each shows on any model. Also what an effect made from scratch starts as.
/// </summary>
public static class EffectPresets
{
	/// <summary>Every preset, in the order they are offered. Never worn as they are: see <see cref="Copy"/>.</summary>
	public static IReadOnlyList<Effect> All { get; } = PresetScripts.Gallery();

	/// <summary>The presets for a weapon: those that come from the weapon held, offered first in a weapon's design.</summary>
	public static IEnumerable<Effect> ForWeapons => All.Where( x => x.Anchor.KindIs == AnchorKind.HeldWeapon );

	/// <summary>A copy of <paramref name="preset"/> to wear beside <paramref name="worn"/>, with an id none of them has.</summary>
	public static Effect Copy( Effect preset, IReadOnlyList<Effect> worn )
	{
		var copy = JsonSerializer.Deserialize<Effect>( JsonSerializer.Serialize( preset ) ).Sanitized();
		copy.Id = Effect.NewId( worn );
		return copy;
	}

	/// <summary>What a new effect made from scratch is called, then a number none worn has.</summary>
	public const string NewName = "My effect";

	/// <summary>
	/// An effect to build from scratch, to wear beside <paramref name="worn"/>: "My effect" and a number none of them has,
	/// from the chest, with one layer of sparkles so something shows from the start (see <see cref="StartingLayer"/>).
	/// </summary>
	public static Effect New( IReadOnlyList<Effect> worn )
	{
		var number = 1;
		while ( Named( worn, $"{NewName} {number}" ) )
			number++;

		var effect = new Effect
		{
			Name = $"{NewName} {number}", Anchor = new EffectAnchor { Joint = nameof( Effect.Joint.Chest ) },
			Layers = new List<EffectLayer> { StartingLayer( LayerKind.Sprite ) },
		}.Sanitized();

		effect.Id = Effect.NewId( worn );
		return effect;
	}

	/// <summary>A layer of <paramref name="kind"/> as one is added: something that shows at once, to change from there.</summary>
	public static EffectLayer StartingLayer( LayerKind kind ) => kind switch
	{
		LayerKind.Light => new EffectLayer { Kind = nameof( LayerKind.Light ), Color = "#ffd27a", Size = 60f, Amount = 1f },
		LayerKind.Trail => new EffectLayer { Kind = nameof( LayerKind.Trail ), Color = "#8af4ff", ColorEnd = "#2a5aff", Size = 3f, SizeEnd = 0.5f, Lifetime = 0.4f },
		_ => new EffectLayer
		{
			Kind = nameof( LayerKind.Sprite ), Picture = nameof( LayerPicture.Sparkle ), Shape = nameof( LayerShape.Sphere ), Spread = 3f, Color = "#ffd27a",
			Size = 2f, SizeEnd = 0.5f, Speed = 10f, Lifetime = 0.8f, Amount = 20f,
		},
	};

	private static bool Named( IReadOnlyList<Effect> worn, string name )
	{
		for ( var i = 0; i < worn.Count; i++ )
		{
			if ( worn[ i ]?.Name == name )
				return true;
		}

		return false;
	}
}
