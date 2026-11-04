namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A double helix turning round the player from the knees to the head: two strands and base-pair rungs between them coloured by pair.</summary>
	public static readonly StressEffect DnaHelix = new(
		"DNA double helix",
		"https://github.com/TheComputerGeek2/MagicSpells/wiki/EffectLib-Dna",
		"A double helix turning round the player from the knees to the head: two strands and base-pair rungs between them coloured by pair",
		"""
		-- DNA helix: a double helix turning round the player from the knees to the head: two strands half a turn apart, and
		-- twelve base-pair rungs between them, each half coloured by its base (A-T, G-C, either way round).

		[Parameter( "Strand color", Group = "Look", Tip = "The colour of one strand" )]
		local firstColor = Color.Parse( "#4aa8ff" )

		[Parameter( "Other strand color", Group = "Look", Tip = "The colour of the other" )]
		local secondColor = Color.Parse( "#ff4a8a" )

		[Parameter( "Radius", Min = 6, Max = 30, Step = 1, Group = "Look", Tip = "How far out the strands wind" )]
		local radius = 16

		[Parameter( "Turns", Min = 0.5, Max = 4, Step = 0.25, Group = "Look", Tip = "How many times the strands wind round, bottom to top" )]
		local turns = 2

		[Parameter( "Spin", Min = -2, Max = 2, Step = 0.1, Group = "Rhythm", Tip = "Turns a second" )]
		local spin = 0.4

		Effect.Name = "DNA helix"
		Effect.Joint = Joint.Hips

		-- From below the hips to the head, and the bases' colours: A, T, G, C
		local bottom = -24
		local top = 36
		local rungs = 12
		local bases = { Color.Parse( "#ffd23a" ), Color.Parse( "#3aff8a" ), Color.Parse( "#c07aff" ), Color.Parse( "#ff8a3a" ) }

		-- Where a strand is at a height (0 at the bottom, 1 at the top); the second is half a turn round from the first
		local function strandAt( along: number, strand: number ): Vector3
			local angle = along * turns * Math.PI * 2 + strand * Math.PI + t * spin * Math.PI * 2
			return Vector3( Math.Cos( angle ) * radius, Math.Sin( angle ) * radius, Math.Lerp( bottom, top, along ) )
		end

		local first = Beam()
		first.Color = firstColor
		first.Size = 0.9
		first.Keep = 40

		function first.Particle( p )
			p.Offset = strandAt( p.Index / (p.Count - 1), 0 )
		end

		local second = Beam()
		second.Color = secondColor
		second.Size = 0.9
		second.Keep = 40

		function second.Particle( p )
			p.Offset = strandAt( p.Index / (p.Count - 1), 1 )
		end

		-- The rungs: five dots each, from one strand across to the other
		local pairs = Sprite()
		pairs.Size = 0.8
		pairs.Amount = 0
		pairs.Follow = true
		pairs.Keep = 60

		function pairs.Particle( p )
			local rung = Math.Floor( p.Index / 5 )
			local along = (rung + 0.5) / rungs
			local across = p.Index % 5 / 4
			p.Offset = Vector3.Lerp( strandAt( along, 0 ), strandAt( along, 1 ), across )

			-- A-T on even rungs, G-C on odd, turned round every other pair; each half its own base
			local pair = rung % 2
			local turned = Math.Floor( rung / 2 ) % 2 == 1
			local base = pair * 2 + (if (across < 0.5) == turned then 1 else 0)
			p.Color = bases[ base ]
		end
		""" );
}
