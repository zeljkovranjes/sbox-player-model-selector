using PlayerModels.Core.Script;

namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A sticker of LED-sign text sliding left, wrapping round, fading in and out at the sign's edges, each column lit in the palette's colours.</summary>
	public static readonly StressEffect Marquee = new(
		"Scrolling marquee text sticker",
		"https://forum.godotengine.org/t/horizontally-scrolling-dynamicfont-shader/13446",
		"A sticker of LED-sign text sliding left, wrapping round, fading in and out at the sign's edges, each column lit in the palette's colours",
		"""
		-- Marquee: the text of a scrolling LED sign as a sticker, ten letters spread across the square; the sticker script
		-- slides them.

		local text = { "O", "P", "E", "N", " ", "2", "4", "/", "7", "*" }
		for k = 0, text.Count - 1 do
			shape.Letter( text[ k ], -0.9 + k * 0.2, 0, 0.3 )
		end
		""", ScriptKind.Shape,
		"""
		-- Marquee: slides every point of the text left at a steady pace, wrapping round the sign (2 wide, the whole square),
		-- and fades it out near the sign's edges so letters slip in and out of view; each column lights in the palette's
		-- colours in turn.

		[Parameter( "Scroll speed", Min = 0, Max = 2, Step = 0.05, Tip = "Sign widths a second" )]
		local scrollSpeed = 0.25

		[Parameter( "Edge", Min = 0.05, Max = 0.5, Step = 0.05, Tip = "How wide the fade at each edge is" )]
		local edge = 0.15

		local span = 2
		local x = origin.X - Math.Frac( t * scrollSpeed ) * span
		local wrapped = x - Math.Floor( (x + span / 2) / span ) * span
		offset = Vector3( wrapped - origin.X, 0, 0 )
		alpha = Math.SmoothStep( 1, 1 - edge, Math.Abs( wrapped ) )
		color = palette[ Math.Floor( (wrapped + 1) * 5 ) % palette.Count ]
		""" );
}
