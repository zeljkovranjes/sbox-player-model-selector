namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>Confetti thrown up when you jump, and a ring of it when you land.</summary>
	public const string ConfettiBurst = """
		-- Confetti burst: confetti thrown up when you jump, and a ring of stars when you land.

		[Parameter( "Colors", Group = "Look", Tip = "The confetti's colours, one to each piece in turn" )]
		local colors = { Color.Parse( "#ff4a6a" ), Color.Parse( "#ffd23a" ), Color.Parse( "#3ad8ff" ), Color.Parse( "#7aff6a" ), Color.Parse( "#c07aff" ) }

		[Parameter( "Pieces", Min = 0, Max = 100, Step = 1, Group = "Amount", Tip = "How many pieces fly when you jump" )]
		local pieces = 50

		Effect.Name = "Confetti burst"
		Effect.Joint = Joint.Chest

		-- Little rectangles thrown up from the chest on a jump, each one of the colours in turn
		local jump = Sprite()
		jump.ComesFrom = ComesFrom.Sphere
		jump.Spread = 4
		jump.LooksLike = Shapes.Rectangle
		jump.ColorMode = ColorMode.Mix
		jump.Palette = colors
		jump.Size = 1.6
		jump.Speed = 140
		jump.Gravity = 220
		jump.Lifetime = 1.8
		jump.Amount = 0
		jump.Solid = true
		jump.When = When.OnJump
		jump.Burst = pieces

		-- Stars flung out in a ring round the feet on landing
		local land = Sprite()
		land.Joint = Joint.Hips
		land.ComesFrom = ComesFrom.Ring
		land.Spread = 10
		land.Offset = Vector3( 0, 0, -20 )
		land.LooksLike = Shapes.Star5
		land.ColorMode = ColorMode.Mix
		land.Palette = colors
		land.Size = 1.4
		land.Speed = 90
		land.Gravity = 160
		land.Lifetime = 1.4
		land.Amount = 0
		land.Solid = true
		land.When = When.OnLand
		land.Burst = 40
		""";
}
