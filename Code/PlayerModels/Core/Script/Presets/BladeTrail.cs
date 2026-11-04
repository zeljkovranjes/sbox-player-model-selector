namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A trail behind the tip of the weapon held as it swings, and sparks when it strikes.</summary>
	public const string BladeTrail = """
		-- Blade trail: a trail behind the tip of the weapon held as it swings, and sparks when it strikes.

		[Parameter( "Trail color", Group = "Look", Tip = "The colour the trail fades to" )]
		local trailColor = Color.Parse( "#5a9aff" )

		[Parameter( "Sparks", Min = 0, Max = 100, Step = 1, Group = "Amount", Tip = "How many sparks fly on each strike" )]
		local sparkCount = 16

		Effect.Name = "Blade trail"
		Effect.Anchor = Anchor.HeldWeapon
		Effect.Joint = Joint.RightHand

		-- A trail behind the weapon's tip, showing only as much as the tip moves
		local trail = Trail()
		trail.ColorEnd = trailColor
		trail.Size = 5
		trail.SizeEnd = 0.5
		trail.Lifetime = 0.25
		trail.FadeWithSpeed = true

		-- Sparks bursting from the tip when it strikes (a swing counts as a shot)
		local sparks = Sprite()
		sparks.ComesFrom = ComesFrom.Sphere
		sparks.Spread = 1
		sparks.LooksLike = Picture.Spark
		sparks.Color = Color.Parse( "#fff4c8" )
		sparks.ColorEnd = Color.Parse( "#ff9a3a" )
		sparks.Size = 1.8
		sparks.SizeEnd = 0.4
		sparks.Speed = 90
		sparks.Gravity = 220
		sparks.Lifetime = 0.45
		sparks.Amount = 0
		sparks.When = When.OnShoot
		sparks.Burst = sparkCount
		""";
}
