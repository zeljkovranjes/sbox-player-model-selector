namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>While swinging, the held blade leaves see-through copies of itself along the swing, cyan fading to violet, with a thin bright trail at its tip.</summary>
	public static readonly StressEffect GhostBlade = new(
		"Ghost blade afterimage",
		"https://www.youtube.com/watch?v=1HRuo581PqQ",
		"While swinging, the held blade leaves see-through copies of itself along the swing, cyan fading to violet, with a thin bright trail at its tip",
		"""
		-- Ghost blade: each swing leaves see-through copies of the held blade along its sweep, cyan fading to violet, and a
		-- thin white-hot trail behind its tip.

		[Parameter( "Ghost color", Group = "Look", Tip = "The colour a copy is left in" )]
		local ghostColor = Color.Parse( "#5ae0ff" )

		[Parameter( "Fade to", Group = "Look", Tip = "The colour a copy fades to" )]
		local fadeColor = Color.Parse( "#8a3aff" )

		[Parameter( "Copies a second", Min = 5, Max = 30, Step = 1, Group = "Amount", Tip = "How many copies a swing leaves a second" )]
		local rate = 24

		[Parameter( "Stay", Min = 0.1, Max = 0.6, Step = 0.05, Group = "Rhythm", Tip = "Seconds a copy stays" )]
		local stay = 0.25

		Effect.Name = "Ghost blade"
		Effect.Anchor = Anchor.HeldWeapon
		Effect.Joint = Joint.RightHand

		-- Copies of the blade, left for a moment after each swing
		local copies = Ghost()
		copies.Color = ghostColor
		copies.ColorEnd = fadeColor
		copies.Lifetime = stay
		copies.Amount = rate
		copies.When = When.OnShoot
		copies.ShowFor = 0.35

		-- The trail behind the tip, only as much as it moves
		local edge = Trail()
		edge.Color = Color.White
		edge.ColorEnd = ghostColor
		edge.Size = 1.5
		edge.SizeEnd = 0.2
		edge.Lifetime = 0.2
		edge.FadeWithSpeed = true
		""" );
}
