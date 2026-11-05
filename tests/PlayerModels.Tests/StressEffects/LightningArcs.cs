namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>Crackling bolts between the hands, their middles thrown about while their ends stay put, re-forming many times a second.</summary>
	public static readonly StressEffect LightningArcs = new(
		"Lightning arcs between hands",
		"https://code.tutsplus.com/how-to-generate-shockingly-good-2d-lightning-effects--gamedev-2681t",
		"Crackling bolts between the hands, their middles thrown about while their ends stay put, re-forming many times a second",
		"""
		-- Lightning arcs: crackling bolts between the hands. Each point of a bolt is pushed sideways by smooth noise that
		-- tapers to nothing at both ends, so the ends stay at the hands; the bolts take a new shape many times a second and
		-- dim between shapes, with sparks spitting from each palm.

		[Parameter( "Bolt color", Group = "Look", Tip = "The colour at the bolts' edges" )]
		local boltColor = Color.Parse( "#8ac8ff" )

		[Parameter( "Jaggedness", Min = 0, Max = 12, Step = 0.5, Group = "Look", Tip = "How far the bolts' middles are thrown about" )]
		local jagged = 5

		[Parameter( "Flicker", Min = 1, Max = 30, Step = 1, Group = "Rhythm", Tip = "How many times a second a bolt takes a new shape" )]
		local flicker = 16

		Effect.Name = "Lightning arcs"
		Effect.Joint = Joint.LeftHand

		-- How far a bolt's point is thrown from the line between the hands (forwards and up): none at the ends, most in the
		-- middle, coarse and fine noise together, a new shape each flicker (the same on every client)
		local function thrown( along: number, bolt: number ): Vector3
			local shape = Math.Floor( t * flicker ) * 3 + bolt * 101
			local taper = Math.Sin( along * Math.PI ) * jagged
			local forward = Math.Noise( along * 5, shape ) * 0.7 + Math.Noise( along * 13, shape + 50 ) * 0.3 - 0.5
			local up = Math.Noise( along * 5, shape + 25 ) * 0.7 + Math.Noise( along * 13, shape + 75 ) * 0.3 - 0.5
			return Vector3( forward, 0, up ) * 2 * taper
		end

		-- The main bolt: a beam from the left hand to the right through twenty points
		local bolt = Beam()
		bolt.ComesFrom = ComesFrom.Line
		bolt.ToJoint = Joint.RightHand
		bolt.Color = Color.White
		bolt.ColorEnd = boltColor
		bolt.Size = 0.7
		bolt.Keep = 20

		function bolt.Particle( p )
			p.Offset = thrown( p.Index / (p.Count - 1), 0 )

			-- each new shape starts bright and dims until the next
			p.Alpha = 1 - Math.Frac( t * flicker ) * 0.6
		end

		-- A thinner second bolt, wilder, taking its own shapes half a flicker later
		local branch = Beam()
		branch.ComesFrom = ComesFrom.Line
		branch.ToJoint = Joint.RightHand
		branch.Color = boltColor
		branch.Size = 0.35
		branch.Keep = 14

		function branch.Particle( p )
			p.Offset = thrown( p.Index / (p.Count - 1), 1 ) * 1.4
			p.Alpha = 0.8 - Math.Frac( t * flicker + 0.5 ) * 0.6
		end

		-- Sparks spitting from each palm
		local leftSparks = Sprite()
		leftSparks.ComesFrom = ComesFrom.Sphere
		leftSparks.Spread = 1
		leftSparks.LooksLike = Picture.Spark
		leftSparks.Color = Color.White
		leftSparks.ColorEnd = boltColor
		leftSparks.Size = 0.8
		leftSparks.SizeEnd = 0.1
		leftSparks.Speed = 40
		leftSparks.Lifetime = 0.25
		leftSparks.Amount = 20

		local rightSparks = Sprite()
		rightSparks.Joint = Joint.RightHand
		rightSparks.ComesFrom = ComesFrom.Sphere
		rightSparks.Spread = 1
		rightSparks.LooksLike = Picture.Spark
		rightSparks.Color = Color.White
		rightSparks.ColorEnd = boltColor
		rightSparks.Size = 0.8
		rightSparks.SizeEnd = 0.1
		rightSparks.Speed = 40
		rightSparks.Lifetime = 0.25
		rightSparks.Amount = 20

		-- A blue light flickering between the hands
		local flash = Light()
		flash.Joint = Joint.Chest
		flash.Offset = Vector3( 10, 0, 0 )
		flash.Color = boltColor
		flash.Size = 80
		flash.Amount = 1.4
		flash.Rhythm = Rhythm.Flicker
		flash.RhythmSpeed = 10
		flash.RhythmAmount = 0.6
		""" );
}
