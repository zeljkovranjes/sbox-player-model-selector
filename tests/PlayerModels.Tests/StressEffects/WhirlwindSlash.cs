namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A spin attack's ring: a near-full circle of slash lying flat round the waist, whirling round as it fades, its front bright and its tail faded, with gusts streaking round it.</summary>
	public static readonly StressEffect WhirlwindSlash = new(
		"Spinning whirlwind slash",
		"https://realtimevfx.com/t/venom-slash-breakdown-of-the-effect-included/18903",
		"A spin attack's ring: a near-full circle of slash lying flat round the waist, whirling round as it fades, its front bright and its tail faded, with gusts streaking round it",
		"""
		-- Whirlwind slash: a spin attack's ring: a near-full circle of slash lying flat round the waist, whirling round twice
		-- as it fades, its front bright and its tail faded, with gusts of wind streaking round it.

		[Parameter( "Slash color", Group = "Look", Tip = "The colour of the ring" )]
		local slashColor = Color.Parse( "#c8f0ff" )

		[Parameter( "Turns", Min = 0.5, Max = 4, Step = 0.5, Group = "Rhythm", Tip = "How many times it whirls round as it fades" )]
		local turns = 2

		[Parameter( "Radius", Min = 10, Max = 24, Step = 1, Group = "Look", Tip = "How far out the ring is" )]
		local radius = 24

		Effect.Name = "Whirlwind slash"
		Effect.Joint = Joint.Hips

		-- The ring: 330 degrees of a thin crescent from its tail round to its front (straight up), clear at its tail
		local arc = 330
		local steps = 48
		local ring = Shapes.New( "Whirlwind" )
		for k = 0, steps do
			local angle = (90 - arc * (1 - k / steps)) * Math.PI / 180
			ring.Point( Math.Cos( angle ) * 0.95, Math.Sin( angle ) * 0.95 )
		end

		for k = steps, 0, -1 do
			local along = k / steps
			local angle = (90 - arc * (1 - along)) * Math.PI / 180
			local inner = 0.95 * (1 - 0.15 * Math.Sin( Math.PI * along ))
			ring.Point( Math.Cos( angle ) * inner, Math.Sin( angle ) * inner )
		end

		ring.EndOutline()
		ring.Fade( 90 - arc, 90 )

		-- Lying flat round the waist, whirling round as it fades
		local spin = Sprite()
		spin.LooksLike = ring
		spin.Color = slashColor
		spin.Size = radius * 2
		spin.Lifetime = 0.45
		spin.Amount = 0
		spin.Follow = true
		spin.Align = Align.Ground

		function spin.Particle( p )
			p.Rotation = p.Life * turns * 360
			p.Size = 0.85 + p.Life * 0.25
			p.Alpha = 1 - Math.SmoothStep( 0.5, 1, p.Life )
		end

		-- Gusts: streaks circling the waist with the ring, a little above and below it
		local gusts = Sprite()
		gusts.LooksLike = Picture.Spark
		gusts.Color = Color.White
		gusts.Size = 1.6
		gusts.Lifetime = 0.45
		gusts.Amount = 0
		gusts.Follow = true

		function gusts.Particle( p )
			local angle = p.Seed * Math.PI * 2 + p.Life * turns * Math.PI * 2
			local out = radius * (0.7 + 0.4 * Math.Random( p.Seed + 1 ))
			p.Offset = Vector3( Math.Cos( angle ) * out, Math.Sin( angle ) * out, (Math.Random( p.Seed + 2 ) - 0.5) * 12 )
			p.Alpha = 1 - p.Life
		end

		function OnShoot( shot )
			spin.Emit( 1 )
			gusts.Emit( 24 )
		end
		""" );
}
