namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>Dark smoke rolling off the body with a purple glow inside it that shows on a dark night.</summary>
	public const string DarkAura = """
		-- Dark aura: dark smoke rolling off the body, big enough to read as smoke in daylight, with a purple glow inside
		-- it that shows on a dark night.

		[Parameter( "Smoke color", Group = "Look", Tip = "The colour the smoke starts as" )]
		local smokeColor = Color.Parse( "#2a1238" )

		[Parameter( "Glow color", Group = "Look", Tip = "The glow inside the smoke" )]
		local glowColor = Color.Parse( "#6a2a9a" )

		Effect.Name = "Dark aura"
		Effect.Joint = Joint.Hips

		-- Solid smoke all around the body, drifting up slowly as it moves with it
		local smoke = Sprite()
		smoke.ComesFrom = ComesFrom.AroundBody
		smoke.Spread = 32
		smoke.LooksLike = Picture.Smoke
		smoke.Color = smokeColor
		smoke.ColorEnd = Color.Parse( "#08040c" )
		smoke.Size = 14
		smoke.SizeEnd = 22
		smoke.Speed = 3
		smoke.Gravity = -5
		smoke.Lifetime = 2.4
		smoke.Amount = 18
		smoke.Follow = true
		smoke.Solid = true

		-- A purple glow inside it, moving with the body
		local glow = Sprite()
		glow.ComesFrom = ComesFrom.AroundBody
		glow.Spread = 30
		glow.Color = glowColor
		glow.ColorEnd = Color.Parse( "#1a0628" )
		glow.Size = 8
		glow.SizeEnd = 13
		glow.Lifetime = 1.8
		glow.Amount = 9
		glow.Follow = true
		""";
}
