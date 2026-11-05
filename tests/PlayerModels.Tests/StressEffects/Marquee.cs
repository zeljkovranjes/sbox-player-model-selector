namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>An LED sign of lights over the player's head, its text sliding past and wrapping round, fading in and out at the sign's edges, each column lit in the palette's colours.</summary>
	public static readonly StressEffect Marquee = new(
		"Scrolling marquee sign",
		"https://forum.godotengine.org/t/horizontally-scrolling-dynamicfont-shader/13446",
		"An LED sign of lights over the player's head, its text sliding past and wrapping round, fading in and out at the sign's edges, each column lit in the palette's colours",
		"""
		-- Marquee: a scrolling LED sign over the head, a hundred lights along the letters of its text. Each light slides past
		-- at a steady pace, wrapping round the sign, and fades near its edges so letters slip in and out of view; each column
		-- lights in the colours in turn.

		[Parameter( "Scroll speed", Min = 0, Max = 2, Step = 0.05, Group = "Rhythm", Tip = "Sign widths a second" )]
		local scrollSpeed = 0.25

		[Parameter( "Edge", Min = 0.05, Max = 0.5, Step = 0.05, Group = "Look", Tip = "How wide the fade at each edge is, a share of half the sign" )]
		local edge = 0.15

		[Parameter( "Colors", Group = "Look", Tip = "The colours the columns light in" )]
		local colors = { Color.Parse( "#ff4a4a" ), Color.Parse( "#ffd23a" ), Color.Parse( "#4aff8a" ), Color.Parse( "#4ac8ff" ) }

		Effect.Name = "Marquee"
		Effect.Joint = Joint.Head
		Effect.Offset = Vector3( 0, 0, 20 )

		local halfWidth = 24

		-- The text, ten letters spread across the shape
		local sign = Shapes.New( "Sign" )
		local text = { "O", "P", "E", "N", " ", "2", "4", "/", "7", "*" }
		for k = 0, text.Count - 1 do
			sign.Letter( text[ k ], -0.9 + k * 0.2, 0, 0.3 )
		end

		local lights = Sprite()
		lights.ComesFrom = ComesFrom.Shape
		lights.Formation = sign
		lights.Spread = halfWidth
		lights.Size = 1
		lights.Amount = 0
		lights.Keep = 100
		lights.Follow = true

		function lights.Particle( p )
			local span = halfWidth * 2
			local across = p.Origin.Y - Math.Frac( t * scrollSpeed ) * span
			local wrapped = across - Math.Floor( (across + halfWidth) / span ) * span
			p.Offset = Vector3( 0, wrapped - p.Origin.Y, 0 )
			p.Alpha = Math.SmoothStep( 1, 1 - edge, Math.Abs( wrapped ) / halfWidth )
			p.Color = colors[ Math.Floor( (wrapped / halfWidth + 1) * 5 ) % colors.Count ]
		end
		""" );
}
