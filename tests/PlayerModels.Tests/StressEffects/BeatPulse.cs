namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>The player pulses to a tempo they set: a glow at the chest thumps on every beat, a ring rolls out from the feet (bigger on the first beat of each bar) and the light swells with it.</summary>
	public static readonly StressEffect BeatPulse = new(
		"Beat pulse",
		"https://discussions.unity.com/t/make-an-object-pulse-to-a-beat/37300",
		"The player pulses to a tempo they set: a glow at the chest thumps on every beat, a ring rolls out from the feet (bigger on the first beat of each bar) and the light swells with it",
		"""
		-- Beat pulse: the player pulses to a tempo they set (not to the music: scripts never hear the game's sound, and other
		-- players' clients don't hear the same). A glow at the chest thumps on every beat, a ring of light rolls out from the
		-- feet, bigger on the first beat of each bar, and the light swells with it.

		[Parameter( "Beats a minute", Min = 60, Max = 200, Step = 1, Group = "Rhythm", Tip = "The tempo" )]
		local bpm = 128

		[Parameter( "Beats a bar", Min = 1, Max = 8, Step = 1, Group = "Rhythm", Tip = "Every this many beats the ring is bigger" )]
		local perBar = 4

		[Parameter( "Pulse color", Group = "Look", Tip = "The colour of the glow, the ring and the light" )]
		local pulseColor = Color.Parse( "#ff3aa8" )

		Effect.Name = "Beat pulse"
		Effect.Joint = Joint.Chest

		-- One glow kept at the chest
		local core = Sprite()
		core.Color = pulseColor
		core.Size = 8
		core.Amount = 0
		core.Follow = true
		core.Keep = 1

		-- A ring of sparkles rolled out along the ground on each beat
		local ring = Sprite()
		ring.Joint = Joint.Ground
		ring.ComesFrom = ComesFrom.Ring
		ring.Spread = 4
		ring.LooksLike = Picture.Sparkle
		ring.Color = pulseColor
		ring.ColorEnd = Color.Parse( "#7a3aff" )
		ring.Size = 1.6
		ring.SizeEnd = 0.4
		ring.Speed = 90
		ring.Lifetime = 0.5
		ring.Amount = 0

		local light = Light()
		light.Color = pulseColor
		light.Size = 120
		light.Amount = 0.3

		-- How hard the beat kicks right now: 1 on the beat, dying away before the next
		local function kick(): number
			return Math.Exp( -Math.Frac( t * bpm / 60 ) * 6 )
		end

		function core.Particle( p )
			p.Size = 0.7 + kick() * 0.6
			p.Alpha = 0.5 + kick() * 0.5
		end

		local lastBeat = -1

		function OnUpdate( dt )
			local beat = Math.Floor( t * bpm / 60 )
			if beat ~= lastBeat then
				lastBeat = beat
				ring.Emit( if beat % perBar == 0 then 40 else 16 )
			end

			light.Amount = 0.3 + kick() * 1.5
		end
		""" );
}
