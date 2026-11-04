namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>Dust spiralling up round the player from the ground on a widening spiral, rising at a steady pace, with dust kicked up at its foot.</summary>
	public static readonly StressEffect Tornado = new(
		"Tornado vortex",
		"https://www.gamedeveloper.com/design/stirring-up-a-storm-in-unity-s-shuriken",
		"Dust spiralling up round the player from the ground on a widening spiral, rising at a steady pace, with dust kicked up at its foot",
		"""
		-- Tornado: dust spiralling up round the player from the ground. Each mote climbs at a steady pace while it circles,
		-- and the funnel widens as it rises (an Archimedean spiral, r = a + b θ); dark debris whips round faster, and a ring
		-- of dust is kicked up at its foot.

		[Parameter( "Dust color", Group = "Look", Tip = "The colour of the dust" )]
		local dustColor = Color.Parse( "#c8b89a" )

		[Parameter( "Height", Min = 20, Max = 96, Step = 4, Group = "Look", Tip = "How tall the funnel is" )]
		local height = 88

		[Parameter( "Spin", Min = 0.25, Max = 4, Step = 0.25, Group = "Rhythm", Tip = "Turns a second" )]
		local spin = 1.5

		Effect.Name = "Tornado"
		Effect.Joint = Joint.Ground

		-- The funnel: each mote starts its own way round, climbs steadily and swings out as it goes
		local funnel = Sprite()
		funnel.LooksLike = Picture.Smoke
		funnel.Color = dustColor
		funnel.ColorEnd = Color.Parse( "#8a7a64" )
		funnel.Size = 2.5
		funnel.SizeEnd = 7
		funnel.Lifetime = 1.8
		funnel.Amount = 70
		funnel.Follow = true
		funnel.Solid = true

		function funnel.Particle( p )
			local turned = p.Seed * Math.PI * 2 + p.Age * spin * Math.PI * 2
			local radius = 3 + p.Life * height * 0.3
			p.Offset = Vector3( Math.Cos( turned ) * radius, Math.Sin( turned ) * radius, p.Life * height )
			p.Alpha = Math.Sin( p.Life * Math.PI ) * 0.7
			p.Rotation = p.Age * 90
		end

		-- Debris: dark specks whipped round twice as fast, close in
		local debris = Sprite()
		debris.LooksLike = Picture.Spark
		debris.Color = Color.Parse( "#3a2e22" )
		debris.Size = 0.8
		debris.Lifetime = 1.2
		debris.Amount = 30
		debris.Follow = true
		debris.Solid = true

		function debris.Particle( p )
			local turned = p.Seed * Math.PI * 2 - p.Age * spin * Math.PI * 4
			local radius = 2 + p.Life * height * 0.2
			p.Offset = Vector3( Math.Cos( turned ) * radius, Math.Sin( turned ) * radius, p.Life * height * 0.8 )
		end

		-- Dust kicked up in a ring at its foot
		local kicked = Sprite()
		kicked.ComesFrom = ComesFrom.Ring
		kicked.Spread = 6
		kicked.LooksLike = Picture.Smoke
		kicked.Color = dustColor
		kicked.Size = 4
		kicked.SizeEnd = 9
		kicked.Speed = 30
		kicked.Gravity = -10
		kicked.Lifetime = 1
		kicked.Amount = 12
		kicked.Solid = true
		""" );
}
