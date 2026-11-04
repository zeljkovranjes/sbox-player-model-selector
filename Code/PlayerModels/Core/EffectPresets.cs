using System.Collections.Generic;
using System.Text.Json;
using PlayerModels.Core.Script;

namespace PlayerModels.Core;

/// <summary>
/// The effects the Effects page offers to start from: the stock presets, each written as a script (see
/// <see cref="PresetScripts"/>), built from it and carrying it, so Edit as code opens exactly that script and every machine
/// builds the preset again from it. Made on the citizen, in its sizes: a body of another size gets them in proportion,
/// and joints a model lacks are stood in for, so each shows on any model.
/// </summary>
public static class EffectPresets
{
	/// <summary>Every preset, in the order they are offered. Never worn as they are: see <see cref="Copy"/>.</summary>
	public static IReadOnlyList<Effect> All { get; } = PresetScripts.Gallery();

	/// <summary>A copy of <paramref name="preset"/> to wear beside <paramref name="worn"/>, with an id none of them has.</summary>
	public static Effect Copy( Effect preset, IReadOnlyList<Effect> worn )
	{
		var copy = JsonSerializer.Deserialize<Effect>( JsonSerializer.Serialize( preset ) ).Sanitized();
		copy.Id = Effect.NewId( worn );
		return copy;
	}
}
