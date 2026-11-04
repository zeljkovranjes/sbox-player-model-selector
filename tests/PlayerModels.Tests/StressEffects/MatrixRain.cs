namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>Columns of green glyphs falling round the player: each column's head falls at its own pace, the glyphs it passes stay lit and fade behind it, and glyphs flicker to others.</summary>
	public static readonly StressEffect MatrixRain = new(
		"Matrix digital rain",
		"https://godotshaders.com/shader/matrix-rain/",
		"Columns of green glyphs falling round the player: each column's head falls at its own pace, the glyphs it passes stay lit and fade behind it, and glyphs flicker to others",
		"""
		-- Matrix rain: columns of green glyphs falling round the player like the film's digital rain. Each column's head falls
		-- at its own pace from the top; a glyph appears wherever a head is and stays there, white at first, then green, fading
		-- behind it; now and then a glyph flickers to another.

		[Parameter( "Glyph color", Group = "Look", Tip = "The colour the glyphs fade in" )]
		local glyphColor = Color.Parse( "#3aff6a" )

		[Parameter( "Columns", Min = 4, Max = 24, Step = 1, Group = "Look", Tip = "How many columns stand round the player" )]
		local columns = 16

		[Parameter( "Fall speed", Min = 10, Max = 120, Step = 5, Group = "Rhythm", Tip = "How fast the heads fall, in units a second" )]
		local fallSpeed = 50

		Effect.Name = "Matrix rain"
		Effect.Joint = Joint.Hips

		-- How high the rain starts and stops round the hips, the space between glyphs in a column, how far out the columns
		-- stand
		local top = 44
		local bottom = -40
		local cell = 4
		local ringRadius = 18

		-- Each glyph sprite takes one of these
		local rain = Sprite()
		rain.Pictures = { Shapes.Text( "0" ), Shapes.Text( "1" ), Shapes.Text( "7" ), Shapes.Text( "Z" ), Shapes.Text( "X" ), Shapes.Text( "V" ) }
		rain.Color = glyphColor
		rain.Size = 3.2
		rain.Lifetime = 1.6
		rain.Amount = 90
		rain.Follow = true

		-- Where a column's head is at a time: falling at the column's own pace, from the top again once it reaches the bottom
		local function headHeight( column: number, at: number ): number
			local pace = fallSpeed * (0.7 + 0.6 * Math.Random( column ))
			local fallen = Math.Frac( at * pace / (top - bottom) + Math.Random( column + 0.5 ) )
			return top - fallen * (top - bottom)
		end

		-- A glyph stays where its column's head was when it appeared, on the column's grid
		function rain.Particle( p )
			local column = Math.Floor( p.Seed * columns )
			local turn = column / columns * Math.PI * 2
			local height = Math.Floor( headHeight( column, t - p.Age ) / cell ) * cell
			p.Offset = Vector3( Math.Cos( turn ) * ringRadius, Math.Sin( turn ) * ringRadius, height )
			p.Frame = Math.Floor( Math.Random( p.Seed * 977 + Math.Floor( t * 4 + p.Seed * 3 ) ) * rain.Pictures.Count )
			p.Color = if p.Age < 0.1 then Color.White else glyphColor
			p.Alpha = Math.Pow( 1 - p.Life, 1.5 )
		end
		""" );
}
