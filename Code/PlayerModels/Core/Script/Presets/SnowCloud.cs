namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A little white cloud over the head, snowing.</summary>
	public const string SnowCloud = """
		-- Snow cloud: a little white cloud over the head, snowing.

		[Parameter( "Cloud color", Group = "Look", Tip = "The cloud's colour" )]
		local cloudColor = Color.Parse( "#f4f6fa" )

		[Parameter( "Snowflakes a second", Min = 0, Max = 60, Step = 1, Group = "Amount", Tip = "How hard it snows" )]
		local snowfall = 18

		Effect.Name = "Snow cloud"
		Effect.Joint = Joint.Crown

		-- Puffs of solid cloud ten units over the head, moving with it
		local cloud = Sprite()
		cloud.ComesFrom = ComesFrom.Sphere
		cloud.Spread = 6
		cloud.Offset = Vector3( 0, 0, 10 )
		cloud.LooksLike = Picture.Smoke
		cloud.Color = cloudColor
		cloud.ColorEnd = Color.Parse( "#d8dde6" )
		cloud.Size = 8
		cloud.SizeEnd = 10
		cloud.Lifetime = 2
		cloud.Amount = 8
		cloud.Follow = true
		cloud.Solid = true

		-- Snowflakes falling gently out of it
		local snow = Sprite()
		snow.ComesFrom = ComesFrom.Sphere
		snow.Spread = 7
		snow.Offset = Vector3( 0, 0, 8 )
		snow.LooksLike = Picture.Snowflake
		snow.Size = 2
		snow.SizeEnd = 1.6
		snow.Gravity = 25
		snow.Lifetime = 2
		snow.Amount = snowfall
		snow.Solid = true
		""";
}
