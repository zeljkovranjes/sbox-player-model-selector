using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// The stock effects written in the effect language: the scripts ARE the presets, embedded here so there is nothing to
/// load, readable and commented so a player who opens one learns from it. Each exposes a few <c>[Parameter]</c> options.
/// They are written flat, one statement per field at the top, so the visual editor can still change every field in
/// place. <see cref="Gallery"/> builds the presets from them.
/// </summary>
public static partial class PresetScripts
{
	/// <summary>Every stock effect's script, by its name, in the order the gallery offers them.</summary>
	public static IReadOnlyList<(string Name, string Script)> All { get; } = new[]
	{
		("Glowing eyes", GlowingEyes), ("Fire hands", FireHands), ("Sparkle halo", SparkleHalo), ("Holy aura", HolyAura),
		("Dark aura", DarkAura), ("Hearts", Hearts), ("Snow cloud", SnowCloud), ("Rain cloud", RainCloud),
		("Electric body", ElectricBody), ("Magic orbs", MagicOrbs), ("Flame head", FlameHead), ("Lantern hand", LanternHand),
		("Speed trail", SpeedTrail), ("Sword slash", SwordSlash), ("Confetti burst", ConfettiBurst), ("Rainbow", Rainbow),
	};

	/// <summary>
	/// The presets, each built from its script; a script that doesn't build is left out (the tests keep every one
	/// building).
	/// </summary>
	public static List<Effect> Gallery()
	{
		var effects = new List<Effect>();
		foreach ( var (_, script) in All )
		{
			var compiled = ScriptCompiler.Compile( script );
			if ( compiled.Succeeded && new ScriptMachine( compiled.Program ).Build() is { Succeeded: true } build )
				effects.Add( build.Effect );
		}

		return effects;
	}
}
