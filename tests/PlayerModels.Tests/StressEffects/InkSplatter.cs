namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>Getting hurt throws drops of ink out of the player that land as ragged blots on the ground round their feet, fading after a few seconds; the harder the hit, the more ink.</summary>
	public static readonly StressEffect InkSplatter = new(
		"Ink splatter on hurt",
		"https://vfxmike.blogspot.com/2017/04/splatoon-in-unity.html",
		"Getting hurt throws drops of ink out of the player that land as ragged blots on the ground round their feet, fading after a few seconds; the harder the hit, the more ink",
		"""
		-- Ink splatter: getting hurt throws drops of ink out of the player that land a moment later as blots on the ground
		-- round their feet, each a ragged splat turned and sized its own way, spreading as it lands and fading after a few
		-- seconds. The harder the hit, the more ink.

		[Parameter( "Ink color", Group = "Look", Tip = "The colour of the ink" )]
		local inkColor = Color.Parse( "#ff3aa8" )

		[Parameter( "Stay", Min = 1, Max = 5, Step = 0.5, Group = "Rhythm", Tip = "Seconds a blot stays" )]
		local stay = 4

		Effect.Name = "Ink splatter"
		Effect.Joint = Joint.Chest

		-- A splat, drawn once: a ragged blob (its edge pushed in and out by noise) and a few drops round it
		local blot = Shapes.New( "Blot" )
		for k = 0, 31 do
			local angle = k / 32 * Math.PI * 2
			local edge = 0.45 + Math.Noise( Math.Cos( angle ) * 2 + 3, Math.Sin( angle ) * 2 + 3 ) * 0.4
			blot.Point( Math.Cos( angle ) * edge, Math.Sin( angle ) * edge )
		end

		blot.EndOutline()
		for drop = 0, 4 do
			local angle = Math.Random( drop ) * Math.PI * 2
			blot.Add( Shapes.Circle, Math.Cos( angle ) * 0.85, Math.Sin( angle ) * 0.85, 0.06 + Math.Random( drop + 0.5 ) * 0.06 )
		end

		-- Drops thrown out of the body
		local drops = Sprite()
		drops.ComesFrom = ComesFrom.Sphere
		drops.Spread = 4
		drops.LooksLike = Picture.Droplet
		drops.Color = inkColor
		drops.Size = 1.6
		drops.SizeEnd = 0.8
		drops.Speed = 90
		drops.Gravity = 320
		drops.Lifetime = 0.6
		drops.Amount = 0
		drops.Solid = true

		-- Blots flat on the ground round the feet
		local blots = Sprite()
		blots.Joint = Joint.Ground
		blots.ComesFrom = ComesFrom.Ring
		blots.Spread = 14
		blots.Offset = Vector3( 0, 0, 0.3 )
		blots.LooksLike = blot
		blots.Color = inkColor
		blots.Size = 6
		blots.Lifetime = stay
		blots.Amount = 0
		blots.Solid = true
		blots.Align = Align.Ground

		-- Each its own turn and size, spreading out as it lands and fading at the end
		function blots.Particle( p )
			p.Rotation = p.Seed * 360
			p.Size = (0.6 + Math.Random( p.Seed + 1 ) * 0.9) * Math.Min( p.Age * 8, 1 )
			p.Alpha = 1 - Math.SmoothStep( 0.7, 1, p.Life )
		end

		-- How many blots are waiting to land, and when they do
		local landing = 0
		local landAt = 0

		function OnHurt( damage )
			drops.Emit( Math.Clamp( damage / 2, 6, 40 ) )
			landing = Math.Clamp( Math.Floor( damage / 8 ), 1, 10 )
			landAt = t + 0.35
		end

		function OnUpdate( dt )
			if landing > 0 and t >= landAt then
				blots.Emit( landing )
				landing = 0
			end
		end
		""" );
}
