using System.Collections.Generic;
using PlayerModels.Core.Script;

namespace PlayerModels.Tests.StressEffects;

/// <summary>
/// One effect found online, written by hand in the effect language to stress it (not a stock preset): where it comes
/// from, what it is, and its script. An effect, or a shape (a weapon skin, a sign) with the sticker script that colours
/// it (<see cref="Sticker"/>), run per tile or point.
/// </summary>
internal sealed record StressEffect( string Name, string Source, string Description, string Script, ScriptKind Kind = ScriptKind.Effect, string Sticker = null )
{
	/// <summary>Its name as a file name: "Orbiting rune circle" is "orbiting-rune-circle".</summary>
	public string FileName => Name.ToLowerInvariant().Replace( ' ', '-' ).Replace( "/", "" );

	public override string ToString() => Name;
}

/// <summary>The stress effects, one file each beside this one.</summary>
internal static partial class StressScripts
{
	public static IReadOnlyList<StressEffect> All => new[]
	{
		RuneCircle, LightningArcs, Tornado, GalaxySwirl, PortalRing, EnergyShield, Footprints, ParticleWings, GhostTrail,
		MatrixRain, Heartbeat, DnaHelix, Fireworks, InkSplatter, KillCounter, HexPlasmaSkin, Marquee, BeatPulse,
		SnakeOfFollowers, FireflyFlock,
		AnimeSlash, FlyingSlash, CrossSlash, GhostBlade, WhirlwindSlash, ClassicSlash,
	};

	public static StressEffect Named( string name )
	{
		foreach ( var effect in All )
		{
			if ( effect.Name == name )
				return effect;
		}

		return null;
	}
}
