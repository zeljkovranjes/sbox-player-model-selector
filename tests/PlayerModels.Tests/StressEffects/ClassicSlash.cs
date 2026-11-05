namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>The classic sword swoosh written from scratch: a crescent arc in the swing's plane sweeping round with the blade, bright and coloured at its leading edge and fading along its length to nothing at its tail, then fading out quickly.</summary>
	public static readonly StressEffect ClassicSlash = new(
		"Traditional crescent slash",
		"https://www.youtube.com/watch?v=9Nv28O2OIoQ",
		"The classic sword swoosh written from scratch: a crescent arc in the swing's plane sweeping round with the blade, bright and coloured at its leading edge and fading along its length to nothing at its tail, then fading out quickly",
		"""
		-- Classic slash, from scratch: on each swing a crescent arc appears in the swing's plane in front of the player and
		-- sweeps round with the blade. Its leading edge is bright and coloured, the arc fading along its length to nothing at
		-- its tail, so it reads as one swoosh; then the whole arc fades out quickly. A thinner white-hot edge rides its front.

		[Parameter( "Slash color", Group = "Look", Tip = "The colour of the arc" )]
		local slashColor = Color.Parse( "#ffd27a" )

		[Parameter( "Radius", Min = 8, Max = 24, Step = 1, Group = "Look", Tip = "How far the arc reaches from its middle" )]
		local radius = 22

		[Parameter( "Arc length", Min = 60, Max = 300, Step = 10, Group = "Look", Tip = "Degrees of circle the arc covers" )]
		local arc = 160

		[Parameter( "Thickness", Min = 0.05, Max = 0.6, Step = 0.05, Group = "Look", Tip = "How thick its middle is, a share of its radius" )]
		local thickness = 0.25

		[Parameter( "Sweep", Min = 0, Max = 180, Step = 5, Group = "Rhythm", Tip = "Degrees it sweeps round with the swing" )]
		local sweep = 110

		[Parameter( "Fade", Min = 0.1, Max = 0.6, Step = 0.05, Group = "Rhythm", Tip = "Seconds until it is gone" )]
		local fade = 0.3

		Effect.Name = "Classic slash"
		Effect.Anchor = Anchor.HeldWeapon
		Effect.Joint = Joint.RightHand

		-- A crescent along the circle's edge from its tail round to its front (straight up): its outer edge, then back along
		-- its inner edge, thickest in the middle and tapering to points at both ends; nothing at its tail, growing to all of
		-- it at its front
		local steps = 40
		local function drawArc( into: Shape, width: number )
			for k = 0, steps do
				local angle = (90 - arc * (1 - k / steps)) * Math.PI / 180
				into.Point( Math.Cos( angle ) * 0.95, Math.Sin( angle ) * 0.95 )
			end

			for k = steps, 0, -1 do
				local along = k / steps
				local angle = (90 - arc * (1 - along)) * Math.PI / 180
				local inner = 0.95 * (1 - width * Math.Sin( Math.PI * along ))
				into.Point( Math.Cos( angle ) * inner, Math.Sin( angle ) * inner )
			end

			into.EndOutline()
			into.Fade( 90 - arc, 90 )
		end

		local swoosh = Shapes.New( "Slash" )
		drawArc( swoosh, thickness )
		local edgeShape = Shapes.New( "Slash edge" )
		drawArc( edgeShape, thickness * 0.3 )

		-- The arc: one sprite each swing, in the swing's plane round the chest, glowing over what's behind it
		local slash = Sprite()
		slash.Joint = Joint.Chest
		slash.Offset = Vector3( 20, 0, 6 )
		slash.LooksLike = swoosh
		slash.Color = slashColor
		slash.Size = radius * 2
		slash.Lifetime = fade
		slash.Amount = 0
		slash.Follow = true
		slash.Align = Align.Swing

		-- Its white-hot edge, on the same circle
		local edge = Sprite()
		edge.Joint = Joint.Chest
		edge.Offset = Vector3( 20, 0, 6 )
		edge.LooksLike = edgeShape
		edge.Color = Color.White
		edge.Size = radius * 2
		edge.Lifetime = fade
		edge.Amount = 0
		edge.Follow = true
		edge.Align = Align.Swing

		-- It sweeps round with the swing (anticlockwise, the way its front points) and fades out fast
		function slash.Particle( p )
			p.Rotation = p.Life * sweep
			p.Alpha = 1 - Math.SmoothStep( 0.3, 1, p.Life )
		end

		edge.Particle = slash.Particle

		function OnShoot( shot )
			slash.Emit( 1 )
			edge.Emit( 1 )
		end
		""" );
}
