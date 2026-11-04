namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A magic circle of runes on the ground under the player, turning slowly and breathing, with motes rising off its rim.</summary>
	public static readonly StressEffect RuneCircle = new(
		"Orbiting rune circle",
		"https://realtimevfx.com/t/magic-circle-wip/12940",
		"A magic circle of runes on the ground under the player, turning slowly and breathing, with motes rising off its rim",
		"""
		-- Rune circle: a magic circle of runes on the ground under the player, turning slowly and breathing, with motes rising
		-- off its rim.

		[Parameter( "Circle color", Group = "Look", Tip = "The colour of the circle, its runes and the motes" )]
		local circleColor = Color.Parse( "#7ad8ff" )

		[Parameter( "Circle size", Min = 16, Max = 48, Step = 1, Group = "Look", Tip = "How wide the circle is" )]
		local circleSize = 44

		[Parameter( "Turn speed", Min = -90, Max = 90, Step = 5, Group = "Rhythm", Tip = "Degrees a second; below 0 it turns the other way" )]
		local turnSpeed = 20

		Effect.Name = "Rune circle"
		Effect.Joint = Joint.Ground

		-- The circle, drawn once: two rings, a six-pointed star inside them, and a rune on the band between the rings
		local circle = Shapes.New( "Rune circle" )
		local steps = 48
		for ring = 0, 1 do
			local radius = if ring == 0 then 0.95 else 0.72
			for k = 0, steps do
				local angle = k / steps * Math.PI * 2
				circle.Point( Math.Cos( angle ) * radius, Math.Sin( angle ) * radius )
			end

			circle.EndLine()
		end

		for corner = 0, 1 do
			for k = 0, 3 do
				local angle = (k / 3 + corner / 6) * Math.PI * 2 + Math.PI / 2
				circle.Point( Math.Cos( angle ) * 0.72, Math.Sin( angle ) * 0.72 )
			end

			circle.EndLine()
		end

		-- Letters stand in for runes, each turned to face out from the middle
		local runes = { "A", "Z", "R", "K", "T", "M", "X", "S", "N", "V" }
		for k = 0, runes.Count - 1 do
			local angle = k / runes.Count * Math.PI * 2
			circle.Letter( runes[ k ], Math.Cos( angle ) * 0.835, Math.Sin( angle ) * 0.835, 0.16, angle * 180 / Math.PI - 90 )
		end

		-- One sprite of the circle, kept out all the time and lying flat on the ground
		local rune = Sprite()
		rune.Offset = Vector3( 0, 0, 0.5 )
		rune.LooksLike = circle
		rune.Color = circleColor
		rune.Size = circleSize
		rune.Amount = 0
		rune.Follow = true
		rune.Keep = 1
		rune.Align = Align.Ground

		function rune.Particle( p )
			p.Rotation = t * turnSpeed
			p.Alpha = 0.75 + 0.25 * Math.Sin( t * 3 )
		end

		-- Motes rising off the rim
		local motes = Sprite()
		motes.ComesFrom = ComesFrom.Ring
		motes.Spread = circleSize * 0.45
		motes.LooksLike = Picture.Sparkle
		motes.Color = circleColor
		motes.Size = 1.2
		motes.SizeEnd = 0.2
		motes.Gravity = -40
		motes.Lifetime = 1.2
		motes.Amount = 20

		-- A soft light from the circle, breathing with it
		local glow = Light()
		glow.Offset = Vector3( 0, 0, 4 )
		glow.Color = circleColor
		glow.Size = 90
		glow.Amount = 0.8
		glow.Rhythm = Rhythm.Pulse
		glow.RhythmSpeed = 0.5
		glow.RhythmAmount = 0.3
		""" );
}
