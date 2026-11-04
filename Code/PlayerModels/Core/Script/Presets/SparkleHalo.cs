namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A turning ring of stars over the head, on a faint ring of light.</summary>
	public const string SparkleHalo = """
		-- Sparkle halo: a ring of stars turning over the head, on a faint ring of light.

		[Parameter( "Halo color", Group = "Look", Tip = "The colour the stars start as" )]
		local haloColor = Color.Parse( "#fff4b0" )

		[Parameter( "Halo size", Min = 1, Max = 24, Step = 0.5, Group = "Look", Tip = "How wide the ring is" )]
		local haloSize = 6

		[Parameter( "Spin speed", Min = 0, Max = 3, Step = 0.05, Group = "Rhythm", Tip = "Turns a second" )]
		local spinSpeed = 0.3

		Effect.Name = "Sparkle halo"
		Effect.Joint = Joint.Crown

		-- Stars on a ring four units over the top of the head, turning with it
		local stars = Sprite()
		stars.ComesFrom = ComesFrom.Ring
		stars.Spread = haloSize
		stars.Offset = Vector3( 0, 0, 4 )
		stars.LooksLike = Picture.Sparkle
		stars.Color = haloColor
		stars.ColorEnd = Color.Parse( "#ffc94d" )
		stars.Size = 2.2
		stars.SizeEnd = 0.3
		stars.Amount = 22
		stars.Follow = true
		stars.Rhythm = Rhythm.Spin
		stars.RhythmSpeed = spinSpeed
		stars.RhythmAmount = 1

		-- A soft golden glow along the same ring
		local glow = Sprite()
		glow.ComesFrom = ComesFrom.Ring
		glow.Spread = haloSize
		glow.Offset = Vector3( 0, 0, 4 )
		glow.Color = Color.Parse( "#ffe27a" )
		glow.ColorEnd = Color.Parse( "#b88a2a" )
		glow.Size = 2.4
		glow.SizeEnd = 1.6
		glow.Lifetime = 0.6
		glow.Amount = 30
		glow.Follow = true
		glow.Rhythm = Rhythm.Spin
		glow.RhythmSpeed = spinSpeed
		glow.RhythmAmount = 1
		""";
}
