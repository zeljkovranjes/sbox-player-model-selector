namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A streak of light from the muzzle to where each shot hits, gone in a blink.</summary>
	public const string BulletTracer = """
		-- Bullet tracer: a streak of light from the weapon's muzzle to where each shot hits, gone in a blink.

		[Parameter( "Tracer color", Group = "Look", Tip = "The colour the streak starts as" )]
		local tracerColor = Color.Parse( "#fff6c8" )

		[Parameter( "Thickness", Min = 0.5, Max = 8, Step = 0.1, Group = "Look", Tip = "How thick the streak is" )]
		local thickness = 2.4

		Effect.Name = "Bullet tracer"
		Effect.Anchor = Anchor.HeldWeapon
		Effect.Joint = Joint.RightHand

		-- Glows all along the shot's path, thrown out with each shot and fading at once
		local tracer = Sprite()
		tracer.ComesFrom = ComesFrom.Shot
		tracer.Spread = 0
		tracer.Color = tracerColor
		tracer.ColorEnd = Color.Parse( "#ffb040" )
		tracer.Size = thickness
		tracer.SizeEnd = 0.6
		tracer.Speed = 200
		tracer.Lifetime = 0.16
		tracer.Amount = 0
		tracer.When = When.OnShoot
		tracer.Burst = 100
		""";
}
