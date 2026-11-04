namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A bright arc behind the tip of the weapon held as it swings, and sparks when it strikes.</summary>
	public const string SwordSlash = """
		-- Sword slash: a bright arc behind the tip of the weapon held as it swings, and sparks when it strikes.

		[Parameter( "Slash color", Group = "Look", Tip = "The colour the arc fades to" )]
		local slashColor = Color.Parse( "#5a9aff" )

		[Parameter( "Sparks", Min = 0, Max = 100, Step = 1, Group = "Amount", Tip = "How many sparks fly on each strike" )]
		local sparkCount = 16

		Effect.Name = "Sword slash"
		Effect.Anchor = Anchor.HeldWeapon
		Effect.Joint = Joint.RightHand

		-- The arc: a trail behind the weapon's tip, showing only as much as the tip moves
		local arc = Trail()
		arc.ColorEnd = slashColor
		arc.Size = 5
		arc.SizeEnd = 0.5
		arc.Lifetime = 0.25
		arc.FadeWithSpeed = true

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
