namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>World at War's rain: the game's colours scrolling round a turning ring at the waist, sparkles rising through it.</summary>
	public const string Rainbow = """
		-- Rainbow: World at War's "rain", the game's colours scrolling round a turning ring at the waist, sparkles rising
		-- through it.

		[Parameter( "Cycle", Group = "Look", Tip = "Which World at War colour cycle it runs" )]
		local cycle = ColorMode.Rain

		[Parameter( "Spin speed", Min = 0, Max = 3, Step = 0.05, Group = "Rhythm", Tip = "Turns a second" )]
		local spinSpeed = 0.25

		Effect.Name = "Rainbow"
		Effect.Joint = Joint.Hips

		-- Glows on a ring round the waist, coloured one by one as they appear
		local ring = Sprite()
		ring.ComesFrom = ComesFrom.Ring
		ring.Spread = 14
		ring.Offset = Vector3( 0, 0, 4 )
		ring.ColorMode = cycle
		ring.Size = 3.5
		ring.SizeEnd = 2.5
		ring.Lifetime = 1.2
		ring.Amount = 20
		ring.Follow = true
		ring.Rhythm = Rhythm.Spin
		ring.RhythmSpeed = spinSpeed
		ring.RhythmAmount = 1

		-- Sparkles rising all around the body in the same colours
		local sparkles = Sprite()
		sparkles.ComesFrom = ComesFrom.AroundBody
		sparkles.Spread = 30
		sparkles.LooksLike = Picture.Sparkle
		sparkles.ColorMode = cycle
		sparkles.Size = 2.2
		sparkles.SizeEnd = 0.4
		sparkles.Speed = 2
		sparkles.Gravity = -18
		sparkles.Lifetime = 1.4
		sparkles.Amount = 16
		sparkles.Follow = true
		""";
}
