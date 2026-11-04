namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A soft warm glow all around the body, sparkles drifting up through it, and a light that breathes with it.</summary>
	public const string HolyAura = """
		-- Holy aura: a soft warm glow all around the body, sparkles drifting up through it, and a light breathing with it.

		[Parameter( "Aura color", Group = "Look", Tip = "The glow's colour" )]
		local auraColor = Color.Parse( "#b89a5a" )

		[Parameter( "Light color", Group = "Look", Tip = "The colour it lights things" )]
		local lightColor = Color.Parse( "#ffe2a0" )

		[Parameter( "Breath speed", Min = 0, Max = 4, Step = 0.05, Group = "Rhythm", Tip = "Breaths a second" )]
		local breathSpeed = 0.5

		Effect.Name = "Holy aura"
		Effect.Joint = Joint.Hips

		-- Big soft glows all around the body, swelling as they go
		local glow = Sprite()
		glow.ComesFrom = ComesFrom.AroundBody
		glow.Spread = 34
		glow.Color = auraColor
		glow.ColorEnd = Color.Parse( "#7a5a24" )
		glow.Size = 9
		glow.SizeEnd = 15
		glow.Lifetime = 1.6
		glow.Amount = 16
		glow.Follow = true
		glow.Rhythm = Rhythm.Pulse
		glow.RhythmSpeed = breathSpeed
		glow.RhythmAmount = 0.3

		-- Sparkles drifting up through it
		local sparkles = Sprite()
		sparkles.ComesFrom = ComesFrom.AroundBody
		sparkles.Spread = 34
		sparkles.LooksLike = Picture.Sparkle
		sparkles.Color = Color.Parse( "#fff6d0" )
		sparkles.ColorEnd = Color.Parse( "#ffd27a" )
		sparkles.Size = 1.6
		sparkles.SizeEnd = 0.3
		sparkles.Speed = 2
		sparkles.Gravity = -20
		sparkles.Lifetime = 1.5
		sparkles.Amount = 10

		-- A warm light at the chest, breathing with the glow
		local light = Light()
		light.Offset = Vector3( 0, 0, 10 )
		light.Color = lightColor
		light.Size = 90
		light.Amount = 0.8
		light.Rhythm = Rhythm.Pulse
		light.RhythmSpeed = breathSpeed
		light.RhythmAmount = 0.3
		""";
}
