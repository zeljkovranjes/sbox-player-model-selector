namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A bubble of hexagon cells round the player, faint until hit; each hit sends a ring of light across the bubble from where it struck.</summary>
	public static readonly StressEffect EnergyShield = new(
		"Energy shield bubble",
		"https://justiceshultz.pages.dev/portfolio-shieldfluidsimulation",
		"A bubble of hexagon cells round the player, faint until hit; each hit sends a ring of light across the bubble from where it struck",
		"""
		-- Energy shield: a bubble of hexagon cells round the player, faint at rest. Each hit sends a ring of light out across
		-- the bubble from where it struck, fading as it spreads; the last four hits ripple at once, and the shield's light
		-- flares with each.

		[Parameter( "Shield color", Group = "Look", Tip = "The colour of the cells" )]
		local shieldColor = Color.Parse( "#5ae0ff" )

		[Parameter( "Ripple speed", Min = 1, Max = 8, Step = 0.5, Group = "Rhythm", Tip = "How fast a hit's ring spreads: bubble widths a second" )]
		local rippleSpeed = 3

		Effect.Name = "Energy shield"
		Effect.Joint = Joint.Chest

		-- The cells: ninety hexagons kept spread over a ball as tall as the body, each facing out like a tile
		local cells = Sprite()
		cells.ComesFrom = ComesFrom.AroundBody
		cells.Spread = 30
		cells.LooksLike = Shapes.Hexagon
		cells.Color = shieldColor
		cells.Size = 7
		cells.Amount = 0
		cells.Follow = true
		cells.Keep = 90
		cells.Align = Align.Outward

		local flash = Light()
		flash.Color = shieldColor
		flash.Size = 140
		flash.Amount = 0.2

		-- The last four hits: where each struck (a way out from the middle), when, and how hard
		local hitWhere = List.Create( 4, Vector3.Forward )
		local hitWhen = List.Create( 4, -100 )
		local hitStrength = List.Create( 4, 0 )
		local hits = 0
		local lastHit = -100

		-- No client knows which way a hit came from, so each strikes a side picked by how many came before (the same on
		-- every client)
		function OnHurt( damage )
			local slot = hits % hitWhen.Count
			local turn = Math.Random( hits ) * Math.PI * 2
			local height = Math.Random( hits + 0.5 ) - 0.5
			hitWhere[ slot ] = Vector3( Math.Cos( turn ), Math.Sin( turn ), height ).Normal
			hitWhen[ slot ] = t
			hitStrength[ slot ] = Math.Clamp( damage / 25, 0.3, 1 )
			hits = hits + 1
			lastHit = t
		end

		function OnUpdate( dt )
			flash.Amount = 0.2 + 2 * Math.Exp( -(t - lastHit) * 6 )
		end

		-- A cell shimmers faintly at rest, and lights up as a hit's ring passes it
		function cells.Particle( p )
			local facing = p.Origin.Normal
			local light = 0.06 + 0.04 * Math.Sin( t * 1.5 + p.Index * 0.7 )
			for k = 0, hitWhen.Count - 1 do
				local since = t - hitWhen[ k ]

				-- how far the cell is from the hit, across the bubble: 0 there, 2 on the far side
				local apart = Vector3.Distance( facing, hitWhere[ k ] )
				local ring = Math.Exp( -Math.Abs( apart - since * rippleSpeed ) * 8 )
				light = light + ring * hitStrength[ k ] * Math.Exp( -since * 2.5 )
			end

			p.Alpha = Math.Min( light, 1 )
			p.Size = 1 + Math.Min( light, 1 ) * 0.2
		end
		""" );
}
