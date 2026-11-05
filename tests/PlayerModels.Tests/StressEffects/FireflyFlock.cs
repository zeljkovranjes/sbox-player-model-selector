namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>Sparks flocking round the player's head like boids: cohesion, alignment, separation and a pull home, each leaving a short streak.</summary>
	public static readonly StressEffect FireflyFlock = new(
		"Boids firefly flock",
		"https://en.wikipedia.org/wiki/Boids",
		"Sparks flocking round the player's head like boids: cohesion, alignment, separation and a pull home, each leaving a short streak",
		"""
		-- Firefly flock: sparks flocking round the player's head like boids. Each steers towards the flock's middle
		-- (cohesion), matches the flock's heading (alignment), keeps off the spark next to it (separation) and is drawn home
		-- to the player; the faster one flies the brighter it glows, and each leaves a short fading streak.

		[Parameter( "Spark color", Group = "Look", Tip = "The sparks' colour" )]
		local sparkColor = Color.Parse( "#ffe27a" )

		[Parameter( "Cohesion", Min = 0, Max = 4, Step = 0.1, Group = "Rhythm", Tip = "How hard each steers to the flock's middle" )]
		local cohesion = 1.2

		[Parameter( "Alignment", Min = 0, Max = 4, Step = 0.1, Group = "Rhythm", Tip = "How hard each matches the flock's heading" )]
		local alignment = 0.8

		[Parameter( "Top speed", Min = 20, Max = 200, Step = 10, Group = "Rhythm", Tip = "The fastest a spark flies, in units a second" )]
		local topSpeed = 90

		Effect.Name = "Firefly flock"
		Effect.Joint = Joint.Head

		-- How many fly, how close is too close, and how hard they are drawn home
		local flock = 16
		local tooClose = 6
		local homing = 1.5

		local sparks = Sprite()
		sparks.LooksLike = Picture.Spark
		sparks.Color = sparkColor
		sparks.Size = 1.4
		sparks.Amount = 0
		sparks.Keep = flock

		-- The streaks each leaves, fifteen times a second
		local streaks = Sprite()
		streaks.Color = sparkColor
		streaks.ColorEnd = Color.Parse( "#ff7a2a" )
		streaks.Size = 0.8
		streaks.SizeEnd = 0.1
		streaks.Lifetime = 0.35
		streaks.Amount = 0

		-- Where each spark is and how it moves, in the world
		local positions = List.Create( flock, Vector3.Zero )
		local velocities = List.Create( flock, Vector3.Zero )
		local streakedAt = -1

		-- Each starts somewhere round the head, drifting its own way (the same on every client)
		function OnSpawn()
			for k = 0, flock - 1 do
				local scatter = Vector3( Math.Random( k ) - 0.5, Math.Random( k + 0.25 ) - 0.5, Math.Random( k + 0.5 ) - 0.5 )
				positions[ k ] = anchorPosition + scatter * 40
				velocities[ k ] = Vector3( Math.Random( k + 0.75 ) - 0.5, Math.Random( k + 0.125 ) - 0.5, 0 ) * 60
			end
		end

		function OnUpdate( dt )
			local step = Math.Min( dt, 0.05 )

			-- the flock's middle and heading
			local middle = Vector3.Zero
			local heading = Vector3.Zero
			for k = 0, flock - 1 do
				middle = middle + positions[ k ]
				heading = heading + velocities[ k ]
			end

			middle = middle / flock
			heading = heading / flock
			local home = anchorPosition + Vector3( 0, 0, 12 )
			for k = 0, flock - 1 do
				local position = positions[ k ]
				local velocity = velocities[ k ]
				local away = position - positions[ (k + 1) % flock ]
				local push = Vector3.Zero
				if away.Length < tooClose then
					push = away.Normal * (tooClose - away.Length) * 20
				end

				local steer = (middle - position) * cohesion + (heading - velocity) * alignment + (home - position) * homing + push
				velocity = velocity + steer * step
				if velocity.Length > topSpeed then
					velocity = velocity.Normal * topSpeed
				end

				velocities[ k ] = velocity
				positions[ k ] = position + velocity * step
			end

			if t - streakedAt >= 1 / 15 then
				streakedAt = t
				for k = 0, flock - 1 do
					streaks.EmitAt( positions[ k ], 1 )
				end
			end
		end

		-- Each spark sprite sits where its boid is, brighter the faster it flies, flickering
		function sparks.Particle( p )
			p.Position = positions[ p.Index ]
			local pace = velocities[ p.Index ].Length / topSpeed
			p.Alpha = 0.4 + 0.6 * pace * Math.Noise( p.Index * 3, t * 6 )
		end
		""" );
}
