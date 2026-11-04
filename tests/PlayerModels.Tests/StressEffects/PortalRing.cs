namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A swirling portal standing in front of the player: a fiery ring whose radius breathes, sparks spiralling inwards with a twist that swings back and forth, and a dim disc filling it.</summary>
	public static readonly StressEffect PortalRing = new(
		"Portal ring",
		"https://godotshaders.com/shader/2d-swirling-vortex-portal/",
		"A swirling portal standing in front of the player: a fiery ring whose radius breathes, sparks spiralling inwards with a twist that swings back and forth, and a dim disc filling it",
		"""
		-- Portal ring: a swirling portal standing in front of the player. Its edge is a ring of fire whose radius breathes in
		-- and out; sparks spiral in from the edge with a twist that swings back and forth as the whole thing turns; a dim
		-- disc fills it.

		[Parameter( "Edge color", Group = "Look", Tip = "The colour of the ring of fire" )]
		local edgeColor = Color.Parse( "#ff9a2a" )

		[Parameter( "Inner color", Group = "Look", Tip = "The colour the sparks turn as they fall in, and of the disc" )]
		local innerColor = Color.Parse( "#7a3aff" )

		[Parameter( "Radius", Min = 8, Max = 24, Step = 1, Group = "Look", Tip = "How big the portal is" )]
		local radius = 22

		[Parameter( "Spin", Min = -3, Max = 3, Step = 0.1, Group = "Rhythm", Tip = "Turns a second; below 0 the other way" )]
		local spin = 0.6

		Effect.Name = "Portal ring"
		Effect.Joint = Joint.Chest
		Effect.Offset = Vector3( 24, 0, 0 )

		-- How big the ring is now: it breathes in and out
		local function ringSize(): number
			return radius * (1 + 0.06 * Math.Sin( t * 2.3 ))
		end

		-- A point round the ring, standing up facing forwards (across to the left, and up)
		local function round( angle: number ): Vector3
			return Vector3( 0, Math.Cos( angle ), Math.Sin( angle ) )
		end

		-- The edge: a beam round the ring whose last point meets its first, wobbling with noise
		local edge = Beam()
		edge.Color = edgeColor
		edge.ColorEnd = Color.Parse( "#ffd27a" )
		edge.Size = 2.2
		edge.Keep = 49

		function edge.Particle( p )
			local angle = p.Index / (p.Count - 1) * Math.PI * 2 + t * spin * Math.PI * 2
			local wobble = 1 + 0.08 * (Math.Noise( p.Index % 48 * 0.5, t * 4 ) - 0.5)
			p.Offset = round( angle ) * ringSize() * wobble
		end

		-- Sparks spiralling in from the edge, the twist swinging back and forth, turning from fire to the inner colour
		local sparks = Sprite()
		sparks.LooksLike = Picture.Spark
		sparks.Color = edgeColor
		sparks.Size = 1.2
		sparks.SizeEnd = 0.4
		sparks.Lifetime = 1.2
		sparks.Amount = 70
		sparks.Follow = true

		function sparks.Particle( p )
			local twist = 2.5 + 1.5 * Math.Sin( t * 0.8 )
			local angle = p.Seed * Math.PI * 2 + p.Life * twist + t * spin * Math.PI * 2
			p.Offset = round( angle ) * ringSize() * (1 - p.Life)
			p.Color = Color.Lerp( edgeColor, innerColor, p.Life )
			p.Alpha = 1 - p.Life * p.Life
		end

		-- The disc: one dim circle kept facing forwards, breathing with the ring
		local disc = Sprite()
		disc.LooksLike = Shapes.Circle
		disc.Color = innerColor
		disc.Size = radius * 2
		disc.Amount = 0
		disc.Follow = true
		disc.Keep = 1
		disc.Align = Align.Facing

		function disc.Particle( p )
			p.Size = ringSize() / radius
			p.Alpha = 0.3 + 0.1 * Math.Sin( t * 5 )
		end

		local light = Light()
		light.Color = innerColor
		light.Size = 120
		light.Amount = 1.2
		""" );
}
