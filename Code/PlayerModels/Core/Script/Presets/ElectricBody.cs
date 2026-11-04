namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>Sparks crackling all over the body, flashes of blue between them, and a flickering blue light.</summary>
	public const string ElectricBody = """
		-- Electric body: sparks crackling all over the body, flashes of blue between them, and a flickering blue light.

		[Parameter( "Spark color", Group = "Look", Tip = "The colour sparks fade to" )]
		local sparkColor = Color.Parse( "#7ab8ff" )

		[Parameter( "Light color", Group = "Look", Tip = "The colour it lights things" )]
		local lightColor = Color.Parse( "#5aa0ff" )

		[Parameter( "Crackle speed", Min = 0, Max = 20, Step = 0.5, Group = "Rhythm", Tip = "How fast it flickers" )]
		local crackle = 14

		Effect.Name = "Electric body"
		Effect.Joint = Joint.Hips

		-- Bright sparks shooting out all over the body
		local sparks = Sprite()
		sparks.ComesFrom = ComesFrom.AroundBody
		sparks.Spread = 32
		sparks.LooksLike = Picture.Sparkle
		sparks.ColorEnd = sparkColor
		sparks.Size = 6
		sparks.SizeEnd = 1
		sparks.Speed = 40
		sparks.Lifetime = 0.3
		sparks.Amount = 70
		sparks.Rhythm = Rhythm.Flicker
		sparks.RhythmSpeed = 12
		sparks.RhythmAmount = 0.4

		-- Quick blue flashes between them
		local flashes = Sprite()
		flashes.ComesFrom = ComesFrom.AroundBody
		flashes.Spread = 30
		flashes.Color = Color.Parse( "#9ccaff" )
		flashes.ColorEnd = Color.Parse( "#3a6aff" )
		flashes.Size = 14
		flashes.SizeEnd = 6
		flashes.Lifetime = 0.12
		flashes.Amount = 40
		flashes.Follow = true
		flashes.Rhythm = Rhythm.Flicker
		flashes.RhythmSpeed = crackle
		flashes.RhythmAmount = 0.6

		-- A blue light at the chest, flickering with them
		local light = Light()
		light.Joint = Joint.Chest
		light.Color = lightColor
		light.Size = 70
		light.Amount = 1.2
		light.Rhythm = Rhythm.Flicker
		light.RhythmSpeed = crackle
		light.RhythmAmount = 0.8
		""";
}
