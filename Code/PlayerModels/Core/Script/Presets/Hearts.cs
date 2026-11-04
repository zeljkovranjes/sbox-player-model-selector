namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>Hearts floating up from the chest.</summary>
	public const string Hearts = """
		-- Hearts: hearts floating up from the chest.

		[Parameter( "Heart color", Group = "Look", Tip = "The colour a heart starts as" )]
		local heartColor = Color.Parse( "#ff3d7f" )

		[Parameter( "Heart size", Min = 0.5, Max = 12, Step = 0.1, Group = "Look", Tip = "How big each heart is" )]
		local heartSize = 2.6

		[Parameter( "Hearts a second", Min = 0, Max = 20, Step = 1, Group = "Amount", Tip = "How many hearts float up each second" )]
		local rate = 5

		Effect.Name = "Hearts"
		Effect.Joint = Joint.Chest

		-- Solid hearts from a little ball in front of the chest, floating up with the body as they grow
		local hearts = Sprite()
		hearts.ComesFrom = ComesFrom.Sphere
		hearts.Spread = 3
		hearts.Offset = Vector3( 5, 0, 0 )
		hearts.LooksLike = Picture.Heart
		hearts.Color = heartColor
		hearts.ColorEnd = Color.Parse( "#ff6f9f" )
		hearts.Size = heartSize
		hearts.SizeEnd = 3.4
		hearts.Speed = 6
		hearts.Gravity = -8
		hearts.Lifetime = 2.4
		hearts.Amount = rate
		hearts.Follow = true
		hearts.Solid = true
		""";
}
