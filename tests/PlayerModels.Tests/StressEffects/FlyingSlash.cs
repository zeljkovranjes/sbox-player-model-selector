namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>Each swing throws a crescent wave of energy forward along the aim that widens as it flies, shedding sparks along its path, gone after a short flight.</summary>
	public static readonly StressEffect FlyingSlash = new(
		"Energy wave slash",
		"https://www.youtube.com/shorts/bNBgb1qoHTU",
		"Each swing throws a crescent wave of energy forward along the aim that widens as it flies, shedding sparks along its path, gone after a short flight",
		"""
		-- Flying slash: each swing throws a crescent wave of energy forward along the aim. It flies out widening as it goes,
		-- shedding sparks along its path, and fades out after a short flight; up to three are in the air at once.

		[Parameter( "Wave color", Group = "Look", Tip = "The colour of the wave" )]
		local waveColor = Color.Parse( "#ff5a3a" )

		[Parameter( "Wave speed", Min = 100, Max = 600, Step = 50, Group = "Rhythm", Tip = "How fast it flies, in units a second" )]
		local waveSpeed = 400

		[Parameter( "Flight", Min = 0.2, Max = 1, Step = 0.1, Group = "Rhythm", Tip = "Seconds it flies" )]
		local flight = 0.6

		Effect.Name = "Flying slash"
		Effect.Anchor = Anchor.HeldWeapon
		Effect.Joint = Joint.RightHand

		-- The wave: a crescent bulging forward (180 degrees, its thickest in the middle and tapering to both tips)
		local steps = 24
		local wave = Shapes.New( "Wave" )
		for k = 0, steps do
			local angle = (k / steps - 0.5) * Math.PI
			wave.Point( Math.Cos( angle ) * 0.95, Math.Sin( angle ) * 0.95 )
		end

		for k = steps, 0, -1 do
			local angle = (k / steps - 0.5) * Math.PI
			local inner = 0.95 - 0.4 * Math.Cos( angle )
			wave.Point( Math.Cos( angle ) * inner, Math.Sin( angle ) * inner )
		end

		wave.EndOutline()

		-- Three waves kept, one for each in the air: where each set off, which way, and when
		local slots = 3
		local waves = Sprite()
		waves.LooksLike = wave
		waves.Color = waveColor
		waves.Size = 32
		waves.Amount = 0
		waves.Keep = slots
		waves.Align = Align.Swing

		local sparks = Sprite()
		sparks.ComesFrom = ComesFrom.Sphere
		sparks.Spread = 6
		sparks.LooksLike = Picture.Spark
		sparks.Color = Color.Parse( "#ffd27a" )
		sparks.ColorEnd = waveColor
		sparks.Size = 1.2
		sparks.SizeEnd = 0.2
		sparks.Speed = 30
		sparks.Gravity = 100
		sparks.Lifetime = 0.4
		sparks.Amount = 0

		local setOffFrom = List.Create( slots, Vector3.Zero )
		local setOffTo = List.Create( slots, Vector3.Forward )
		local setOffAt = List.Create( slots, -100 )
		local nextWave = 0

		-- Where wave k is a while after it set off
		local function flown( k: number, age: number ): Vector3
			return setOffFrom[ k ] + setOffTo[ k ] * waveSpeed * age
		end

		function OnShoot( shot )
			setOffFrom[ nextWave ] = shot.Origin
			setOffTo[ nextWave ] = shot.Direction
			setOffAt[ nextWave ] = t
			nextWave = (nextWave + 1) % slots
		end

		-- Each wave sheds sparks where it is as it flies
		function OnUpdate( dt )
			for k = 0, slots - 1 do
				local age = t - setOffAt[ k ]
				if age >= 0 and age < flight then
					sparks.EmitAt( flown( k, age ), 3 )
				end
			end
		end

		-- A wave's sprite flies with it, widening, and fades at the end of its flight; hidden when it isn't flying
		function waves.Particle( p )
			local age = t - setOffAt[ p.Index ]
			local life = age / flight
			if life < 0 or life > 1 then
				p.Alpha = 0
				return
			end

			p.Position = flown( p.Index, age )
			p.Size = 0.7 + life * 0.5
			p.Alpha = 1 - Math.SmoothStep( 0.6, 1, life )
		end
		""" );
}
