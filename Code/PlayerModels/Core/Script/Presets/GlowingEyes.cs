namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A glow in front of each eye (in front of glasses, too) and a soft light on the face, beating together.</summary>
	public const string GlowingEyes = """
		-- Glowing eyes: a glow in front of each eye, in front of glasses too, and a soft light on the face, all
		-- pulsing together.

		[Parameter( "Eye color", Group = "Look", Tip = "The colour each eye glows" )]
		local eyeColor = Color.Parse( "#7af0ff" )

		[Parameter( "Fade to", Group = "Look", Tip = "The colour a glow fades to as it goes" )]
		local fadeColor = Color.Parse( "#2aa8ff" )

		[Parameter( "Glow size", Min = 0.5, Max = 10, Step = 0.1, Group = "Look", Tip = "How big each glow is" )]
		local glowSize = 2.6

		[Parameter( "Pulse speed", Min = 0, Max = 10, Step = 0.1, Group = "Rhythm", Tip = "How many times a second it pulses" )]
		local pulseSpeed = 1.2

		Effect.Name = "Glowing eyes"
		Effect.Joint = Joint.Eyes

		-- The left eye's glow, three units in front of it, moving with the head
		local leftGlow = Sprite()
		leftGlow.Joint = Joint.LeftEye
		leftGlow.Offset = Vector3( 3, 0, 0 )
		leftGlow.Color = eyeColor
		leftGlow.ColorEnd = fadeColor
		leftGlow.Size = glowSize
		leftGlow.SizeEnd = 1.6
		leftGlow.Lifetime = 0.3
		leftGlow.Amount = 16
		leftGlow.Follow = true
		leftGlow.Rhythm = Rhythm.Pulse
		leftGlow.RhythmSpeed = pulseSpeed
		leftGlow.RhythmAmount = 0.35

		-- The right eye's, the same
		local rightGlow = Sprite()
		rightGlow.Joint = Joint.RightEye
		rightGlow.Offset = Vector3( 3, 0, 0 )
		rightGlow.Color = eyeColor
		rightGlow.ColorEnd = fadeColor
		rightGlow.Size = glowSize
		rightGlow.SizeEnd = 1.6
		rightGlow.Lifetime = 0.3
		rightGlow.Amount = 16
		rightGlow.Follow = true
		rightGlow.Rhythm = Rhythm.Pulse
		rightGlow.RhythmSpeed = pulseSpeed
		rightGlow.RhythmAmount = 0.35

		-- A soft light on the face, pulsing with the eyes
		local light = Light()
		light.Offset = Vector3( 5, 0, 0 )
		light.Color = Color.Parse( "#5ad8ff" )
		light.Size = 28
		light.Amount = 0.5
		light.Rhythm = Rhythm.Pulse
		light.RhythmSpeed = pulseSpeed
		light.RhythmAmount = 0.35
		""";
}
