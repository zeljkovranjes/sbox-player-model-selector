namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>See-through copies of the player left behind while running, blue fading to violet, more of them the faster they go.</summary>
	public static readonly StressEffect GhostTrail = new(
		"Ghost afterimage trail",
		"https://forums.unrealengine.com/t/community-tutorial-afterimage-evade-niagara-particle-system-vfx-tutorial/2151512",
		"See-through copies of the player left behind while running, blue fading to violet, more of them the faster they go",
		"""
		-- Ghost trail: see-through copies of the player left behind while running, blue fading to violet as they go; none
		-- standing still, more the faster the player runs, and a burst of them on landing from a jump.

		[Parameter( "Ghost color", Group = "Look", Tip = "The colour a copy is left in" )]
		local ghostColor = Color.Parse( "#7ad8ff" )

		[Parameter( "Fade to", Group = "Look", Tip = "The colour a copy fades to" )]
		local fadeColor = Color.Parse( "#7a3aff" )

		[Parameter( "Stay", Min = 0.1, Max = 1, Step = 0.05, Group = "Rhythm", Tip = "Seconds a copy stays" )]
		local stay = 0.35

		Effect.Name = "Ghost trail"
		Effect.Joint = Joint.Hips

		local copies = Ghost()
		copies.Color = ghostColor
		copies.ColorEnd = fadeColor
		copies.Lifetime = stay
		copies.Amount = 0
		copies.FadeWithSpeed = true

		-- A faint smear of light at the hips, left behind too
		local smear = Sprite()
		smear.ComesFrom = ComesFrom.AroundBody
		smear.Spread = 10
		smear.Color = ghostColor
		smear.ColorEnd = fadeColor
		smear.Size = 6
		smear.SizeEnd = 2
		smear.Lifetime = 0.3
		smear.Amount = 0
		smear.FadeWithSpeed = true

		local landedAt = -100

		function OnLand( fallSpeed )
			landedAt = t
		end

		-- None standing; one more copy a second for every 10 units a second past a walk, up to twenty, and a burst of
		-- twenty for a moment after landing
		function OnUpdate( dt )
			local landing = if t - landedAt < 0.25 then 20 else 0
			copies.Amount = Math.Max( Math.Clamp( (speed - 80) / 10, 0, 20 ), landing )
			smear.Amount = copies.Amount * 2
		end
		""" );
}
