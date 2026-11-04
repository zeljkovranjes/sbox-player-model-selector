namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A StatTrak-style counter on the held weapon: three seven-segment digits counting kills, the weapon glowing a new colour every few kills and flashing on each.</summary>
	public static readonly StressEffect KillCounter = new(
		"Weapon kill-counter glow",
		"https://csboard.com/en/blog/what-does-stattrak-mean-in-cs2-the-complete-2026-guide",
		"A StatTrak-style counter on the held weapon: three seven-segment digits counting kills, the weapon glowing a new colour every few kills and flashing on each",
		"""
		-- Kill counter: a StatTrak-style counter on the side of the held weapon: three lit seven-segment digits counting the
		-- kills, the weapon glowing a new colour every few kills and flashing on each one. Dying starts the count again.

		[Parameter( "Digit color", Group = "Look", Tip = "The colour of the counter's digits" )]
		local digitColor = Color.Parse( "#ff8a2a" )

		[Parameter( "Tier colors", Group = "Look", Tip = "The glow's colour as kills add up, a tier at a time" )]
		local tiers = { Color.Parse( "#4aa8ff" ), Color.Parse( "#b84aff" ), Color.Parse( "#ff4a4a" ), Color.Parse( "#ffd23a" ) }

		[Parameter( "Kills a tier", Min = 1, Max = 20, Step = 1, Group = "Rhythm", Tip = "How many kills each colour lasts" )]
		local perTier = 5

		Effect.Name = "Kill counter"
		Effect.Anchor = Anchor.HeldWeapon
		Effect.Joint = Joint.RightHand

		-- Which of a digit's seven segments are lit, as bits: 0 top, 1 top right, 2 bottom right, 3 bottom, 4 bottom left,
		-- 5 top left, 6 middle; one number for each digit from 0 to 9
		local segmentsOf = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 }

		-- Where each segment sits in its digit (across, up)
		local segmentX = { 0, 0.5, 0.5, 0, -0.5, -0.5, 0 }
		local segmentY = { 1, 0.5, -0.5, -1, -0.5, 0.5, 0 }

		local kills = 0
		local killedAt = -100

		function OnKill()
			kills = Math.Min( kills + 1, 999 )
			killedAt = t
		end

		function OnDeath()
			kills = 0
		end

		-- Whether bit number bit of mask is set
		local function lit( mask: number, bit: number ): bool
			return Math.Floor( mask / Math.Pow( 2, bit ) ) % 2 == 1
		end

		-- The counter: three digits of seven bars each, on the weapon's left side, facing out from it
		local counter = Sprite()
		counter.Offset = Vector3( 4, 1.5, 1 )
		counter.Facing = Facing.Left
		counter.LooksLike = Shapes.Rectangle
		counter.Color = digitColor
		counter.Size = 0.5
		counter.Amount = 0
		counter.Follow = true
		counter.Keep = 21
		counter.Align = Align.Facing

		function counter.Particle( p )
			local digit = Math.Floor( p.Index / 7 )
			local segment = p.Index % 7
			local value = Math.Floor( kills / Math.Pow( 10, 2 - digit ) ) % 10
			p.Offset = Vector3( (digit - 1) * 1.6 + segmentX[ segment ] * 0.8, 0, segmentY[ segment ] * 0.8 )
			p.Rotation = if segment == 1 or segment == 2 or segment == 4 or segment == 5 then 90 else 0
			p.Alpha = if lit( segmentsOf[ value ], segment ) then 1 else 0.08
		end

		-- A glow along the weapon, more of it with every kill, in the tier's colour
		local glow = Sprite()
		glow.ComesFrom = ComesFrom.Line
		glow.Spread = 8
		glow.Color = tiers[ 0 ]
		glow.Size = 2.5
		glow.SizeEnd = 0.5
		glow.Lifetime = 0.4
		glow.Amount = 0
		glow.Follow = true

		local light = Light()
		light.Color = tiers[ 0 ]
		light.Size = 60
		light.Amount = 0

		function OnUpdate( dt )
			local tier = Math.Min( Math.Floor( kills / perTier ), tiers.Count - 1 )
			local flash = Math.Exp( -(t - killedAt) * 5 )
			glow.Color = tiers[ tier ]
			glow.Amount = Math.Min( kills * 3, 60 ) + flash * 40
			light.Color = tiers[ tier ]
			light.Amount = Math.Min( kills * 0.1, 1.5 ) + flash * 2
		end
		""" );
}
