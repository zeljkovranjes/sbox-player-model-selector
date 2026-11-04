namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A print left flat on the ground at each step, pointing the way the player walks, staying a while and then fading away.</summary>
	public static readonly StressEffect Footprints = new(
		"Fading footprints",
		"https://wilkgames.itch.io/tikus-tales/devlog/199972/creating-footprints-in-the-snow",
		"A print left flat on the ground at each step, pointing the way the player walks, staying a while and then fading away",
		"""
		-- Footprints: a print left flat on the ground at each step, under the foot that stepped and pointing the way the
		-- player walks; it stays dark for a while, then fades away.

		[Parameter( "Print color", Group = "Look", Tip = "The colour of the prints" )]
		local printColor = Color.Parse( "#3a2a1e" )

		[Parameter( "Stay", Min = 1, Max = 5, Step = 0.5, Group = "Rhythm", Tip = "Seconds a print stays" )]
		local stay = 5

		Effect.Name = "Footprints"
		Effect.Joint = Joint.LeftFoot

		-- A sole: the ball of the foot (an egg, wider at the front), a heel and four toes, the big toe on the inside; side is
		-- 1 for the right foot, -1 for the left (mirrored)
		local function drawFoot( foot: Shape, side: number )
			for k = 0, 23 do
				local angle = k / 24 * Math.PI * 2
				local wide = 0.42 + 0.08 * Math.Sin( angle )
				foot.Point( side * (0.05 + Math.Cos( angle ) * wide * 0.8), 0.15 + Math.Sin( angle ) * 0.45 )
			end

			foot.EndOutline()
			foot.Add( Shapes.Ellipse, side * 0.08, -0.62, 0.3 )
			for toe = 0, 3 do
				foot.Add( Shapes.Circle, side * (-0.2 + toe * 0.17), 0.72 - toe * 0.05, 0.11 - toe * 0.015 )
			end
		end

		local leftFoot = Shapes.New( "Left foot" )
		drawFoot( leftFoot, -1 )
		local rightFoot = Shapes.New( "Right foot" )
		drawFoot( rightFoot, 1 )

		-- A print appears under the foot that steps (3 down from the ankle to the sole), lies flat turned the way the body
		-- faces, and stays where it was left
		local leftPrint = Sprite()
		leftPrint.Offset = Vector3( 0, 0, -3 )
		leftPrint.LooksLike = leftFoot
		leftPrint.Color = printColor
		leftPrint.Size = 8
		leftPrint.Lifetime = stay
		leftPrint.Amount = 0
		leftPrint.Solid = true
		leftPrint.Align = Align.Ground

		local rightPrint = Sprite()
		rightPrint.Joint = Joint.RightFoot
		rightPrint.Offset = Vector3( 0, 0, -3 )
		rightPrint.LooksLike = rightFoot
		rightPrint.Color = printColor
		rightPrint.Size = 8
		rightPrint.Lifetime = stay
		rightPrint.Amount = 0
		rightPrint.Solid = true
		rightPrint.Align = Align.Ground

		-- Dark for most of its time, then fading
		local function printAlpha( life: number ): number
			return 1 - Math.SmoothStep( 0.6, 1, life )
		end

		function leftPrint.Particle( p )
			p.Alpha = printAlpha( p.Life )
		end

		function rightPrint.Particle( p )
			p.Alpha = printAlpha( p.Life )
		end

		function OnFootstep( foot )
			if foot == Joint.LeftFoot then
				leftPrint.Emit( 1 )
			else
				rightPrint.Emit( 1 )
			end
		end
		""" );
}
