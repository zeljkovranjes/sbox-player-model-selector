namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>Each jump launches a rocket that climbs trailing sparks, bursts into a ball of coloured stars and crackles with glitter a moment later.</summary>
	public static readonly StressEffect Fireworks = new(
		"Fireworks on jump",
		"https://docs.unity3d.com/2017.2/Documentation/Manual/PartSysSubEmitModule.html",
		"Each jump launches a rocket that climbs trailing sparks, bursts into a ball of coloured stars and crackles with glitter a moment later",
		"""
		-- Fireworks: each jump launches a rocket from the player. It climbs trailing sparks (fast off the ground, slowing to
		-- the top), bursts into a ball of coloured stars that lights the player up, then crackles with glitter a moment
		-- later; up to three can be in the air at once.

		[Parameter( "Colors", Group = "Look", Tip = "The stars' colours, one to each in turn" )]
		local colors = { Color.Parse( "#ff4a6a" ), Color.Parse( "#ffd23a" ), Color.Parse( "#3ad8ff" ), Color.Parse( "#7aff6a" ), Color.Parse( "#c07aff" ) }

		[Parameter( "Height", Min = 40, Max = 300, Step = 10, Group = "Look", Tip = "How high the rockets climb" )]
		local climb = 160

		[Parameter( "Stars", Min = 10, Max = 100, Step = 5, Group = "Amount", Tip = "How many stars a burst has" )]
		local stars = 70

		Effect.Name = "Fireworks"
		Effect.Joint = Joint.Chest

		-- How long a rocket climbs, and how long after bursting it crackles
		local climbTime = 0.9
		local crackleAfter = 0.4

		-- The rocket's trail, left along its path as it climbs
		local trail = Sprite()
		trail.LooksLike = Picture.Spark
		trail.Color = Color.Parse( "#ffe2a0" )
		trail.ColorEnd = Color.Parse( "#ff7a2a" )
		trail.Size = 1.4
		trail.SizeEnd = 0.2
		trail.Gravity = 60
		trail.Lifetime = 0.5
		trail.Amount = 0

		-- The burst: a ball of stars flung out, each one of the colours in turn, falling as they fade
		local burst = Sprite()
		burst.ComesFrom = ComesFrom.Sphere
		burst.Spread = 2
		burst.LooksLike = Picture.Sparkle
		burst.ColorMode = ColorMode.Mix
		burst.Palette = colors
		burst.Size = 2
		burst.SizeEnd = 0.4
		burst.Speed = 120
		burst.Gravity = 80
		burst.Lifetime = 1.4
		burst.Amount = 0

		-- The crackle: white glitter winking where the stars went
		local crackle = Sprite()
		crackle.ComesFrom = ComesFrom.Sphere
		crackle.Spread = 40
		crackle.LooksLike = Picture.Spark
		crackle.Color = Color.White
		crackle.Size = 0.8
		crackle.Lifetime = 0.3
		crackle.Amount = 0

		function crackle.Particle( p )
			p.Alpha = if Math.Random( p.Seed + Math.Floor( t * 20 ) ) > 0.5 then 1 else 0.2
		end

		-- A flash lighting the player up as a rocket bursts
		local flash = Light()
		flash.Offset = Vector3( 0, 0, 24 )
		flash.Color = Color.Parse( "#ffe8c8" )
		flash.Size = 300
		flash.Amount = 0

		-- Three rockets: where each left from, when, and how far along it is (0 climbing, 1 burst, 2 done)
		local launchedFrom = List.Create( 3, Vector3.Zero )
		local launchedAt = List.Create( 3, -100 )
		local stage = List.Create( 3, 2 )
		local nextRocket = 0
		local flashAt = -100

		function OnJump()
			launchedFrom[ nextRocket ] = anchorPosition
			launchedAt[ nextRocket ] = t
			stage[ nextRocket ] = 0
			nextRocket = (nextRocket + 1) % launchedAt.Count
		end

		function OnUpdate( dt )
			for k = 0, launchedAt.Count - 1 do
				local flight = (t - launchedAt[ k ]) / climbTime
				local top = launchedFrom[ k ] + Vector3( 0, 0, climb )
				if stage[ k ] == 0 and flight < 1 then
					local eased = 1 - (1 - flight) * (1 - flight)
					trail.EmitAt( launchedFrom[ k ] + Vector3( 0, 0, climb * eased ), 2 )
				elseif stage[ k ] == 0 then
					burst.EmitAt( top, stars )
					stage[ k ] = 1
					flashAt = t
				elseif stage[ k ] == 1 and t - launchedAt[ k ] > climbTime + crackleAfter then
					crackle.EmitAt( top, stars / 2 )
					stage[ k ] = 2
				end
			end

			flash.Amount = 3 * Math.Exp( -(t - flashAt) * 8 )
		end
		""" );
}
