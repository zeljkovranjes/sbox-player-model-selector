using System;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Who wears a body's effects, as far as every client already knows them: their health (to tell when they are hurt),
/// where they aim (a shot goes from the weapon's muzzle that way), what a shot ignores (the shooter), and when the body
/// came to be, if that was a respawn or a death (a ragdoll). Made when a look goes on a body, never per frame; a body that
/// isn't a player's (a preview) has <see cref="None"/>.
/// </summary>
internal sealed class EffectWearer
{
	/// <summary>How long after its body appears a respawn or a death still counts as happening: a body built a little later still shows it.</summary>
	internal const float FreshSeconds = 2f;

	/// <summary>Nobody in particular: never hurt, aiming where the weapon points, never spawned or died.</summary>
	internal static readonly EffectWearer None = new();

	/// <summary>The wearer's health; null for a body that is never hurt.</summary>
	internal Func<float> Health { get; init; }

	/// <summary>Which way the wearer aims, as the network has it; null to shoot where the weapon points.</summary>
	internal Func<Vector3> Aim { get; init; }

	/// <summary>What a shot's trace passes through: the shooter, whole; null for the body's own object.</summary>
	internal GameObject Shooter { get; init; }

	/// <summary>When (<see cref="RealTime.Now"/>) the wearer spawned again, after dying; null when they didn't.</summary>
	internal float? RespawnedAt { get; init; }

	/// <summary>When (<see cref="RealTime.Now"/>) the body died, for a ragdoll; null for a living body.</summary>
	internal float? DiedAt { get; init; }

	/// <summary>Whether <paramref name="at"/> was at most <see cref="FreshSeconds"/> before <paramref name="now"/>.</summary>
	internal static bool IsFresh( float? at, float now ) => at is { } when && now - when <= FreshSeconds;
}
