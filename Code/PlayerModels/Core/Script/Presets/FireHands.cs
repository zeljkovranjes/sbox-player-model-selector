namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>Flames rising from each fist, smoke above them and a flickering light in each hand.</summary>
	public const string FireHands = """
		-- Fire hands: flames rising from each fist, smoke curling above them, and a flickering light in each hand.

		[Parameter( "Flame color", Group = "Look", Tip = "The colour flames start as" )]
		local flameColor = Color.Parse( "#ffd25a" )

		[Parameter( "Flame height", Min = 0, Max = 400, Step = 5, Group = "Look", Tip = "How fast flames rise, so how tall they burn" )]
		local flameRise = 110

		[Parameter( "Smoke", Group = "Look", Tip = "Whether smoke rises from the flames" )]
		local smoke = true

		Effect.Name = "Fire hands"
		Effect.Joint = Joint.LeftHand

		-- Flames from the left fist (3 units to its left, out of the hand), rising: gravity below 0 lifts them
		local leftFlames = Sprite()
		leftFlames.Joint = Joint.LeftHand
		leftFlames.ComesFrom = ComesFrom.Sphere
		leftFlames.Spread = 2
		leftFlames.Offset = Vector3( 0, 3, 0 )
		leftFlames.LooksLike = Picture.Flame
		leftFlames.Color = flameColor
		leftFlames.ColorEnd = Color.Parse( "#ff3d00" )
		leftFlames.Size = 5.5
		leftFlames.SizeEnd = 1.2
		leftFlames.Speed = 4
		leftFlames.Gravity = -flameRise
		leftFlames.Lifetime = 0.5
		leftFlames.Amount = 40

		-- And from the right fist
		local rightFlames = Sprite()
		rightFlames.Joint = Joint.RightHand
		rightFlames.ComesFrom = ComesFrom.Sphere
		rightFlames.Spread = 2
		rightFlames.Offset = Vector3( 0, -3, 0 )
		rightFlames.LooksLike = Picture.Flame
		rightFlames.Color = flameColor
		rightFlames.ColorEnd = Color.Parse( "#ff3d00" )
		rightFlames.Size = 5.5
		rightFlames.SizeEnd = 1.2
		rightFlames.Speed = 4
		rightFlames.Gravity = -flameRise
		rightFlames.Lifetime = 0.5
		rightFlames.Amount = 40

		-- Dark smoke above each fist, solid so it reads as smoke in daylight
		local leftSmoke = Sprite()
		leftSmoke.Off = not smoke
		leftSmoke.Joint = Joint.LeftHand
		leftSmoke.ComesFrom = ComesFrom.Sphere
		leftSmoke.Spread = 1.5
		leftSmoke.Offset = Vector3( 0, 3, 5 )
		leftSmoke.LooksLike = Picture.Smoke
		leftSmoke.Color = Color.Parse( "#2a2624" )
		leftSmoke.ColorEnd = Color.Parse( "#5e5955" )
		leftSmoke.Size = 3
		leftSmoke.SizeEnd = 8
		leftSmoke.Speed = 2
		leftSmoke.Gravity = -40
		leftSmoke.Lifetime = 1.1
		leftSmoke.Amount = 6
		leftSmoke.Solid = true

		local rightSmoke = Sprite()
		rightSmoke.Off = not smoke
		rightSmoke.Joint = Joint.RightHand
		rightSmoke.ComesFrom = ComesFrom.Sphere
		rightSmoke.Spread = 1.5
		rightSmoke.Offset = Vector3( 0, -3, 5 )
		rightSmoke.LooksLike = Picture.Smoke
		rightSmoke.Color = Color.Parse( "#2a2624" )
		rightSmoke.ColorEnd = Color.Parse( "#5e5955" )
		rightSmoke.Size = 3
		rightSmoke.SizeEnd = 8
		rightSmoke.Speed = 2
		rightSmoke.Gravity = -40
		rightSmoke.Lifetime = 1.1
		rightSmoke.Amount = 6
		rightSmoke.Solid = true

		-- A flickering orange light in each hand
		local leftLight = Light()
		leftLight.Joint = Joint.LeftHand
		leftLight.Offset = Vector3( 0, 3, 0 )
		leftLight.Color = flameColor
		leftLight.Size = 60
		leftLight.Amount = 1.2
		leftLight.Rhythm = Rhythm.Flicker
		leftLight.RhythmSpeed = 8
		leftLight.RhythmAmount = 0.35

		local rightLight = Light()
		rightLight.Joint = Joint.RightHand
		rightLight.Offset = Vector3( 0, -3, 0 )
		rightLight.Color = flameColor
		rightLight.Size = 60
		rightLight.Amount = 1.2
		rightLight.Rhythm = Rhythm.Flicker
		rightLight.RhythmSpeed = 8
		rightLight.RhythmAmount = 0.35
		""";
}
