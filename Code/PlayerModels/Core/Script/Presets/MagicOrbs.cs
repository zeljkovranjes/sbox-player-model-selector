namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>Two glowing orbs circling the hips leaving a short streak, and a third bobbing beside the head.</summary>
	public const string MagicOrbs = """
		-- Magic orbs: two glowing orbs circling the hips, each leaving a short streak, and a third bobbing beside the head.

		[Parameter( "Orbit size", Min = 0, Max = 24, Step = 0.5, Group = "Look", Tip = "How far out the orbs circle" )]
		local orbit = 16

		[Parameter( "Orbit speed", Min = 0, Max = 3, Step = 0.05, Group = "Rhythm", Tip = "Turns a second" )]
		local orbitSpeed = 0.45

		Effect.Name = "Magic orbs"
		Effect.Joint = Joint.Hips

		-- A purple orb sixteen units out, a little above the hips: turning (Spin) carries it round them, and lagging half
		-- behind the body (Drag) leaves its short streak
		local purple = Sprite()
		purple.Offset = Vector3( orbit, 0, 6 )
		purple.Color = Color.Parse( "#c08aff" )
		purple.ColorEnd = Color.Parse( "#5a2aff" )
		purple.Size = 3.4
		purple.SizeEnd = 0.8
		purple.Lifetime = 0.4
		purple.Amount = 45
		purple.Follow = true
		purple.Drag = 0.5
		purple.Rhythm = Rhythm.Spin
		purple.RhythmSpeed = orbitSpeed
		purple.RhythmAmount = 1

		-- A blue one, a little below
		local blue = Sprite()
		blue.Offset = Vector3( orbit, 0, -2 )
		blue.Color = Color.Parse( "#7ad8ff" )
		blue.ColorEnd = Color.Parse( "#2a6aff" )
		blue.Size = 3.4
		blue.SizeEnd = 0.8
		blue.Lifetime = 0.4
		blue.Amount = 45
		blue.Follow = true
		blue.Drag = 0.5
		blue.Rhythm = Rhythm.Spin
		blue.RhythmSpeed = orbitSpeed
		blue.RhythmAmount = 1

		-- A pink one beside the head, jiggling about
		local pink = Sprite()
		pink.Joint = Joint.Head
		pink.Offset = Vector3( 2, 11, 4 )
		pink.Color = Color.Parse( "#ff8ae0" )
		pink.ColorEnd = Color.Parse( "#ff2a8a" )
		pink.Size = 3
		pink.SizeEnd = 1
		pink.Lifetime = 0.35
		pink.Amount = 35
		pink.Follow = true
		pink.Drag = 0.5
		pink.Rhythm = Rhythm.Jiggle
		pink.RhythmSpeed = 0.8
		pink.RhythmAmount = 0.7

		-- A soft violet light among them
		local light = Light()
		light.Offset = Vector3( 0, 0, 4 )
		light.Color = Color.Parse( "#a08aff" )
		light.Size = 60
		light.Amount = 0.8
		light.Rhythm = Rhythm.Pulse
		light.RhythmSpeed = 0.6
		light.RhythmAmount = 0.3
		""";
}
