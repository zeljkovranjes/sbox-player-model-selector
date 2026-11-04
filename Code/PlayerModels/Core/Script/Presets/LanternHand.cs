namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A warm lantern glow held in a hand, lighting the way.</summary>
	public const string LanternHand = """
		-- Lantern hand: a warm lantern glow held in a hand, lighting the way.

		[Parameter( "Hand", Group = "Place", Tip = "Which hand holds it" )]
		local hand = Joint.LeftHand

		[Parameter( "Lantern color", Group = "Look", Tip = "The lantern's glow" )]
		local lanternColor = Color.Parse( "#ffb04a" )

		[Parameter( "Light reach", Min = 8, Max = 400, Step = 10, Group = "Look", Tip = "How far its light reaches" )]
		local reach = 140

		Effect.Name = "Lantern hand"
		Effect.Joint = hand

		-- The lantern's glow, flickering a little like a flame
		local glow = Sprite()
		glow.Offset = Vector3( 0, 4, 0 )
		glow.Color = lanternColor
		glow.ColorEnd = Color.Parse( "#ff8a1a" )
		glow.Size = 5
		glow.Lifetime = 0.6
		glow.Amount = 10
		glow.Follow = true
		glow.Rhythm = Rhythm.Flicker
		glow.RhythmSpeed = 3
		glow.RhythmAmount = 0.15

		-- Its bright heart
		local core = Sprite()
		core.Offset = Vector3( 0, 4, 0 )
		core.Color = Color.Parse( "#fff0c8" )
		core.ColorEnd = Color.Parse( "#ffd08a" )
		core.Lifetime = 0.4
		core.Amount = 10
		core.Follow = true

		-- The warm light it throws
		local light = Light()
		light.Offset = Vector3( 0, 4, 0 )
		light.Color = Color.Parse( "#ffb35a" )
		light.Size = reach
		light.Amount = 1.4
		light.Rhythm = Rhythm.Flicker
		light.RhythmSpeed = 3
		light.RhythmAmount = 0.12
		""";
}
