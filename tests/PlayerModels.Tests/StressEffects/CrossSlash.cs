namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>Two quick slashes crossing in an X in front of the player, the second a moment after the first, the X flashing white where they cross before both fade.</summary>
	public static readonly StressEffect CrossSlash = new(
		"Cross slash",
		"https://www.youtube.com/watch?v=ebAjo-AvB0M",
		"Two quick slashes crossing in an X in front of the player, the second a moment after the first, the X flashing white where they cross before both fade",
		"""
		-- Cross slash: two quick slashes crossing in an X in front of the player: the first cuts down from the top right, the
		-- second from the top left a moment later, and the X flashes white where they cross before both fade.

		[Parameter( "Slash color", Group = "Look", Tip = "The colour of the slashes" )]
		local slashColor = Color.Parse( "#ff3a5a" )

		[Parameter( "Delay", Min = 0, Max = 0.3, Step = 0.02, Group = "Rhythm", Tip = "Seconds between the two cuts" )]
		local delay = 0.08

		[Parameter( "Reach", Min = 10, Max = 24, Step = 1, Group = "Look", Tip = "How far each slash reaches from its middle" )]
		local reach = 22

		Effect.Name = "Cross slash"
		Effect.Joint = Joint.Ground
		Effect.Offset = Vector3( 20, 0, 24 )

		-- A crescent of 140 degrees, thickest in the middle; side 1 sweeps anticlockwise to its front straight up, -1 is its
		-- mirror image (clockwise); each clear at its tail and whole at its front
		local arc = 140
		local steps = 28
		local function drawCut( cut: Shape, side: number )
			for k = 0, steps do
				local angle = (90 - arc * (1 - k / steps)) * Math.PI / 180
				cut.Point( side * Math.Cos( angle ) * 0.95, Math.Sin( angle ) * 0.95 )
			end

			for k = steps, 0, -1 do
				local along = k / steps
				local angle = (90 - arc * (1 - along)) * Math.PI / 180
				local inner = 0.95 * (1 - 0.3 * Math.Sin( Math.PI * along ))
				cut.Point( side * Math.Cos( angle ) * inner, Math.Sin( angle ) * inner )
			end

			cut.EndOutline()
			if side > 0 then
				cut.Fade( 90 - arc, 90 )
			else
				cut.Fade( 90 + arc, 90 )
			end
		end

		local downRight = Shapes.New( "Cut from the right" )
		drawCut( downRight, 1 )
		local downLeft = Shapes.New( "Cut from the left" )
		drawCut( downLeft, -1 )

		-- The cuts lie in a plane facing forwards, so the X is seen from the front
		local first = Sprite()
		first.Offset = Vector3( 0, 0, 24 )
		first.LooksLike = downRight
		first.Color = slashColor
		first.Size = reach * 2
		first.Lifetime = 0.35
		first.Amount = 0
		first.Follow = true
		first.Align = Align.Facing

		local second = Sprite()
		second.Offset = Vector3( 0, 0, 24 )
		second.LooksLike = downLeft
		second.Color = slashColor
		second.Size = reach * 2
		second.Lifetime = 0.35
		second.Amount = 0
		second.Follow = true
		second.Align = Align.Facing

		-- Each cuts down across the middle, turning on a little as it fades, moved so its arc passes through the middle
		local across = reach * 0.56

		function first.Particle( p )
			p.Rotation = 115 + p.Life * 25
			p.Offset = Vector3( 0, across, -across )
			p.Alpha = 1 - p.Life * p.Life
		end

		function second.Particle( p )
			p.Rotation = -115 - p.Life * 25
			p.Offset = Vector3( 0, -across, -across )
			p.Alpha = 1 - p.Life * p.Life
		end

		-- The flash where they cross
		local flash = Sprite()
		flash.Offset = Vector3( 0, 0, 24 )
		flash.LooksLike = Picture.Sparkle
		flash.Color = Color.White
		flash.Size = 14
		flash.SizeEnd = 4
		flash.Lifetime = 0.15
		flash.Amount = 0

		local secondAt = -1

		function OnShoot( shot )
			first.Emit( 1 )
			secondAt = t + delay
		end

		function OnUpdate( dt )
			if secondAt >= 0 and t >= secondAt then
				second.Emit( 1 )
				flash.Emit( 1 )
				secondAt = -1
			end
		end
		""" );
}
