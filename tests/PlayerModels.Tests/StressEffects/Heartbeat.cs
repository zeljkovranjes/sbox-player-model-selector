namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A monitor trace floating in front of the chest: a bright dot sweeps across drawing each beat, the trace behind it fading, and a heart thumping with every beat.</summary>
	public static readonly StressEffect Heartbeat = new(
		"Heartbeat ECG line",
		"https://godotshaders.com/shader/heat-beat-monitor-godot-4/",
		"A monitor trace floating in front of the chest: a bright dot sweeps across drawing each beat, the trace behind it fading, and a heart thumping with every beat",
		"""
		-- Heartbeat: a hospital monitor's trace floating in front of the chest. A bright dot sweeps across drawing each beat
		-- (a small bump, a dip, the tall spike, a dip and a slow hump), the trace fading behind it; a heart beside it thumps
		-- on every beat and the light pulses with it.

		[Parameter( "Trace color", Group = "Look", Tip = "The colour of the trace" )]
		local traceColor = Color.Parse( "#4aff8a" )

		[Parameter( "Beats a minute", Min = 40, Max = 180, Step = 1, Group = "Rhythm", Tip = "How fast the heart beats" )]
		local bpm = 72

		[Parameter( "Height", Min = 2, Max = 10, Step = 0.5, Group = "Look", Tip = "How tall the spike is" )]
		local height = 5

		Effect.Name = "Heartbeat"
		Effect.Joint = Joint.Chest
		Effect.Offset = Vector3( 12, 0, 8 )

		-- How wide the trace is, either way of its middle
		local halfWidth = 12

		-- One beat of the trace, x from 0 to 1: the P wave, Q dip, the R spike, S dip, the T wave, then flat
		local function ecg( x: number ): number
			if x < 0.1 then
				return 0.15 * Math.Sin( x / 0.1 * Math.PI )
			elseif x < 0.16 then
				return 0
			elseif x < 0.18 then
				return -0.15 * (x - 0.16) / 0.02
			elseif x < 0.22 then
				return -0.15 + 1.15 * (x - 0.18) / 0.04
			elseif x < 0.26 then
				return 1 - 1.3 * (x - 0.22) / 0.04
			elseif x < 0.3 then
				return -0.3 + 0.3 * (x - 0.26) / 0.04
			elseif x < 0.5 then
				return 0.25 * Math.Sin( (x - 0.3) / 0.2 * Math.PI )
			end

			return 0
		end

		-- How far across the sweep is, 0 to 1: one beat a pass
		local function sweep(): number
			return Math.Frac( t * bpm / 60 )
		end

		-- How hard the heart thumps now: at the spike, dying away
		local function thump(): number
			return Math.Exp( -Math.Frac( sweep() - 0.2 ) * 10 )
		end

		-- The trace: a beam across, from right to left, through sixty-four points lifted to the beat's shape
		local trace = Beam()
		trace.ComesFrom = ComesFrom.Line
		trace.Spread = halfWidth
		trace.Facing = Facing.Left
		trace.Color = traceColor
		trace.Size = 0.5
		trace.Keep = 64

		function trace.Particle( p )
			local along = p.Index / (p.Count - 1)
			p.Offset = Vector3( 0, 0, ecg( along ) * height )

			-- bright just behind the sweep, fading further back
			p.Alpha = Math.Pow( 1 - Math.Frac( sweep() - along ), 2 )
		end

		-- The dot drawing it
		local dot = Sprite()
		dot.Color = Color.White
		dot.Size = 1.6
		dot.Amount = 0
		dot.Follow = true
		dot.Keep = 1

		function dot.Particle( p )
			p.Offset = Vector3( 0, -halfWidth + 2 * halfWidth * sweep(), ecg( sweep() ) * height )
		end

		-- The heart beside it, thumping
		local heart = Sprite()
		heart.Offset = Vector3( 0, halfWidth + 4, 0 )
		heart.LooksLike = Shapes.Heart
		heart.Color = Color.Parse( "#ff3a5a" )
		heart.Size = 4
		heart.Amount = 0
		heart.Follow = true
		heart.Keep = 1

		function heart.Particle( p )
			p.Size = 1 + 0.35 * thump()
		end

		local light = Light()
		light.Color = traceColor
		light.Size = 60
		light.Amount = 0.3

		function OnUpdate( dt )
			light.Amount = 0.3 + thump()
		end
		""" );
}
