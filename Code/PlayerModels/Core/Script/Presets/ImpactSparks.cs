namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>Sparks thrown off whatever a shot or a swing hits, falling away, and a puff of smoke.</summary>
	public const string ImpactSparks = """
		-- Impact sparks: sparks thrown off whatever a shot or a swing hits, falling away, and a puff of smoke.

		[Parameter( "Spark color", Group = "Look", Tip = "The colour sparks start as" )]
		local sparkColor = Color.Parse( "#fff4c8" )

		[Parameter( "Sparks", Min = 0, Max = 100, Step = 1, Group = "Amount", Tip = "How many sparks fly off each hit" )]
		local sparkCount = 24

		Effect.Name = "Impact sparks"
		Effect.Anchor = Anchor.HeldWeapon
		Effect.Joint = Joint.RightHand

		-- Sparks where the shot hit, thrown out of what it hit in a cone and falling away
		local sparks = Sprite()
		sparks.ComesFrom = ComesFrom.Cone
		sparks.Spread = 1
		sparks.Angle = 35
		sparks.LooksLike = Picture.Spark
		sparks.Color = sparkColor
		sparks.ColorEnd = Color.Parse( "#ff9a3a" )
		sparks.Size = 1.8
		sparks.SizeEnd = 0.4
		sparks.Speed = 140
		sparks.Gravity = 320
		sparks.Lifetime = 0.45
		sparks.Amount = 0
		sparks.When = When.OnHit
		sparks.Burst = sparkCount
		sparks.AtHit = true

		-- A little solid smoke there too, drifting up
		local smoke = Sprite()
		smoke.ComesFrom = ComesFrom.Sphere
		smoke.Spread = 1
		smoke.Angle = 35
		smoke.LooksLike = Picture.Smoke
		smoke.Color = Color.Parse( "#8a8a8a" )
		smoke.ColorEnd = Color.Parse( "#4a4a4a" )
		smoke.Size = 3
		smoke.SizeEnd = 6
		smoke.Speed = 8
		smoke.Gravity = -10
		smoke.Lifetime = 0.6
		smoke.Amount = 0
		smoke.Solid = true
		smoke.When = When.OnHit
		smoke.Burst = 3
		smoke.AtHit = true
		""";
}
