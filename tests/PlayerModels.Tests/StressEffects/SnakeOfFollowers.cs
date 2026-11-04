namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A chain of glowing orbs following the path the player took, each where the player was a moment before the one ahead, linked by a ribbon like a snake.</summary>
	public static readonly StressEffect SnakeOfFollowers = new(
		"Snake of followers",
		"https://forum.gdevelop.io/t/how-can-i-make-a-snake-following-my-player-without-any-lag-when-moving-on-a-grid/71081",
		"A chain of glowing orbs following the path the player took, each where the player was a moment before the one ahead, linked by a ribbon like a snake",
		"""
		-- Snake of followers: a chain of glowing orbs following the path the player took, each one where the player was a
		-- moment before the one ahead of it, bobbing as they go, linked by a thin ribbon like a snake's body.

		[Parameter( "Followers", Min = 1, Max = 16, Step = 1, Group = "Amount", Tip = "How many orbs follow" )]
		local followers = 8

		[Parameter( "Spacing", Min = 0.05, Max = 0.4, Step = 0.05, Group = "Look", Tip = "Seconds of the path between one orb and the next" )]
		local spacing = 0.15

		[Parameter( "Head color", Group = "Look", Tip = "The first orb's colour" )]
		local headColor = Color.Parse( "#7aff6a" )

		[Parameter( "Tail color", Group = "Look", Tip = "The last orb's colour" )]
		local tailColor = Color.Parse( "#2a8aff" )

		Effect.Name = "Snake of followers"
		Effect.Joint = Joint.Hips

		-- The path: where the hips were, twenty times a second, for the last 6.4 seconds; the newest is written over the
		-- oldest
		local sampleEvery = 0.05
		local path = List.Create( 128, Vector3.Zero )
		local newest = 0
		local sampledAt = -1

		function OnSpawn()
			for k = 0, path.Count - 1 do
				path[ k ] = anchorPosition
			end

			sampledAt = t
		end

		function OnUpdate( dt )
			if t - sampledAt >= sampleEvery then
				sampledAt = t
				newest = (newest + 1) % path.Count
				path[ newest ] = anchorPosition
			end
		end

		-- Where the hips were a while ago, between the two samples either side of it
		local function pathAt( secondsAgo: number ): Vector3
			local back = Math.Clamp( secondsAgo / sampleEvery, 0, path.Count - 2 )
			local older = Math.Floor( back )
			local after = path[ (newest - older + path.Count) % path.Count ]
			local before = path[ (newest - older - 1 + path.Count) % path.Count ]
			return Vector3.Lerp( after, before, back - older )
		end

		-- A little up and down, rolling back along the chain
		local function bob( place: number ): Vector3
			return Vector3( 0, 0, Math.Sin( t * 6 - place * 0.8 ) * 2 )
		end

		-- The orbs: sixteen kept, as many shown as there are followers
		local orbs = Sprite()
		orbs.Color = headColor
		orbs.Size = 3
		orbs.Amount = 0
		orbs.Keep = 16

		function orbs.Particle( p )
			if p.Index >= followers then
				p.Alpha = 0
				return
			end

			local along = p.Index / Math.Max( followers - 1, 1 )
			p.Position = pathAt( (p.Index + 1) * spacing ) + bob( p.Index )
			p.Color = Color.Lerp( headColor, tailColor, along )
			p.Size = 1 - along * 0.5
		end

		-- The body: a ribbon through the path from the first orb to the last
		local body = Beam()
		body.Color = headColor
		body.ColorEnd = tailColor
		body.Size = 0.8
		body.Keep = 32

		function body.Particle( p )
			local place = p.Index / (p.Count - 1) * (followers - 1)
			p.Position = pathAt( (place + 1) * spacing ) + bob( place )
		end
		""" );
}
