using PlayerModels.Core;

namespace PlayerModels.Engine;

// What a moment brings with it for a script's event (see EffectScriptRun): how fast the body fell when it landed, how
// much health it lost when hurt, how much speed it lost when it hit something hard
internal sealed partial class EffectSignals
{
	private float _fallSpeed;
	private float _landSpeed;
	private float _damage;
	private float _impactLost;
	private float _impactSpeed;

	/// <summary>Someone this body shot or hit died a moment after (see <see cref="EffectRack"/>): a kill, at most as often as any moment.</summary>
	internal void Killed( float now ) => Happened( LayerWhen.OnKill, true, now );

	/// <summary>The number a moment brings, for its script event's argument: a landing's fall speed, a hurt's damage, an impact's speed; 0 for the rest.</summary>
	internal float AmountOf( LayerWhen when ) => when switch
	{
		LayerWhen.OnLand => _landSpeed,
		LayerWhen.OnHurt => _damage,
		LayerWhen.OnImpact => _impactSpeed,
		_ => 0f,
	};
}
