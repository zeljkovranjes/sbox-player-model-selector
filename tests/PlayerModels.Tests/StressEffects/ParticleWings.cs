namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>Two wings of glowing motes on the back, beating slowly round the spine, with feathers drifting off them.</summary>
	public static readonly StressEffect ParticleWings = new(
		"Particle wings",
		"https://realtimevfx.com/t/unity-fire-wings-vfx-tutorial/11963",
		"Two wings of glowing motes on the back, beating slowly round the spine, with feathers drifting off them",
		"""
		-- Particle wings: two wings of light on the back, each a hundred glowing motes kept spread over a wing drawn once.
		-- They beat slowly, every mote swinging round the spine (the further out, the further it goes), brightening towards
		-- the tips, with feathers drifting off and tumbling down.

		[Parameter( "Inner color", Group = "Look", Tip = "The colour near the back" )]
		local innerColor = Color.Parse( "#fff6d8" )

		[Parameter( "Tip color", Group = "Look", Tip = "The colour at the wing tips" )]
		local tipColor = Color.Parse( "#ffb04a" )

		[Parameter( "Wingspan", Min = 16, Max = 48, Step = 2, Group = "Look", Tip = "How far the wings reach, tip to tip" )]
		local span = 44

		[Parameter( "Beats a second", Min = 0, Max = 3, Step = 0.1, Group = "Rhythm", Tip = "How fast the wings beat" )]
		local beat = 0.7

		Effect.Name = "Particle wings"
		Effect.Joint = Joint.Chest
		Effect.Offset = Vector3( -6, 0, 4 )

		-- Both wings in one shape: a leading edge sweeping up and out, then a scalloped trailing edge of feathers back to the
		-- shoulder; the left one drawn, then the right mirrored
		local wings = Shapes.New( "Wings" )
		for side = -1, 1, 2 do
			wings.Point( side * 0.05, 0.1 )
			wings.Point( side * 0.45, 0.55 )
			wings.Point( side * 0.8, 0.75 )
			wings.Point( side, 0.6 )
			for feather = 0, 5 do
				local along = feather / 5
				local x = side * (0.95 - along * 0.85)
				local y = 0.45 - along * 0.35 - Math.Sin( along * Math.PI ) * 0.55
				wings.Point( x, y )
				wings.Point( x - side * 0.08, y + 0.12 )
			end

			wings.EndOutline()
		end

		-- The motes, kept all over both wings, which face back from the chest
		local motes = Sprite()
		motes.ComesFrom = ComesFrom.Shape
		motes.Spread = span / 2
		motes.Facing = Facing.Back
		motes.Formation = wings
		motes.Fill = true
		motes.Color = innerColor
		motes.Size = 1.6
		motes.Amount = 0
		motes.Follow = true
		motes.Keep = 100

		-- Each mote swings round the spine with the beat: its reach out to the side turns towards the back
		function motes.Particle( p )
			local out = p.Origin.Y
			local flap = Math.Sin( t * beat * Math.PI * 2 ) * 0.5
			p.Offset = Vector3( -Math.Abs( out ) * Math.Sin( flap ), out * (Math.Cos( flap ) - 1), 0 )
			p.Color = Color.Lerp( innerColor, tipColor, Math.Clamp( Math.Abs( out ) / (span / 2), 0, 1 ) )
			p.Alpha = 0.6 + 0.4 * Math.Noise( p.Index, t * 2 )
		end

		-- Feathers drifting off the wings and tumbling down
		local feathers = Sprite()
		feathers.ComesFrom = ComesFrom.Shape
		feathers.Spread = span / 2
		feathers.Facing = Facing.Back
		feathers.Formation = wings
		feathers.Fill = true
		feathers.LooksLike = Shapes.Teardrop
		feathers.Color = tipColor
		feathers.Size = 1.8
		feathers.Gravity = 25
		feathers.Lifetime = 2.5
		feathers.Amount = 5

		function feathers.Particle( p )
			p.Rotation = p.Seed * 360 + p.Age * 120
			p.Alpha = 1 - p.Life
		end
		""" );
}
