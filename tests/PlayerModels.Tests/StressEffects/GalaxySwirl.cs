namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A small spiral galaxy turning above the open hand: stars along logarithmic spiral arms round a bright core.</summary>
	public static readonly StressEffect GalaxySwirl = new(
		"Galaxy swirl",
		"https://kevinawge.github.io/Blog/Galaxy.html",
		"A small spiral galaxy turning above the open hand: stars along logarithmic spiral arms round a bright core",
		"""
		-- Galaxy swirl: a small spiral galaxy turning above the open hand. Its stars lie along logarithmic spiral arms
		-- (r = a e^(b θ), so more of them near the middle), stray a little from their arm, and twinkle; the core glows.

		[Parameter( "Core color", Group = "Look", Tip = "The colour of the core and the stars near it" )]
		local coreColor = Color.Parse( "#fff2c8" )

		[Parameter( "Arm color", Group = "Look", Tip = "The colour the arms fade out to" )]
		local armColor = Color.Parse( "#6a8aff" )

		[Parameter( "Arms", Min = 1, Max = 6, Step = 1, Group = "Look", Tip = "How many spiral arms" )]
		local arms = 3

		[Parameter( "Size", Min = 4, Max = 24, Step = 1, Group = "Look", Tip = "How far the arms reach" )]
		local galaxySize = 10

		[Parameter( "Spin", Min = -2, Max = 2, Step = 0.05, Group = "Rhythm", Tip = "Turns a second" )]
		local spin = 0.15

		Effect.Name = "Galaxy swirl"
		Effect.Joint = Joint.RightHand
		Effect.Offset = Vector3( 0, 0, 8 )

		-- How tightly the arms wind, and how far round each one goes
		local winding = 0.3
		local reach = 2.5 * Math.PI

		-- Ninety stars, each its own place on an arm from its index (the same on every client)
		local stars = Sprite()
		stars.Color = coreColor
		stars.Size = 0.8
		stars.Amount = 0
		stars.Follow = true
		stars.Keep = 90

		function stars.Particle( p )
			local arm = p.Index % arms
			local out = Math.Random( p.Index )
			local round = out * reach
			local radius = galaxySize * Math.Exp( winding * round ) / Math.Exp( winding * reach )
			local stray = (Math.Random( p.Index + 0.5 ) - 0.5) * 0.6
			local angle = arm / arms * Math.PI * 2 + round + stray + t * spin * Math.PI * 2
			local thickness = (Math.Random( p.Index + 0.25 ) - 0.5) * (1 - out) * 2
			p.Offset = Vector3( Math.Cos( angle ) * radius, Math.Sin( angle ) * radius, thickness )
			p.Color = Color.Lerp( coreColor, armColor, out )
			p.Size = 1.2 - out * 0.8
			p.Alpha = 0.6 + 0.4 * Math.Noise( p.Index, t * 3 )
		end

		-- The core: one bright glow kept in the middle
		local core = Sprite()
		core.Color = coreColor
		core.Size = 5
		core.Amount = 0
		core.Follow = true
		core.Keep = 1
		core.Rhythm = Rhythm.Pulse
		core.RhythmSpeed = 0.5
		core.RhythmAmount = 0.2

		local light = Light()
		light.Color = coreColor
		light.Size = 40
		light.Amount = 0.6
		""" );
}
