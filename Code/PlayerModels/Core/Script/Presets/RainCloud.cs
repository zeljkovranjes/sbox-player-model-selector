namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A dark cloud over the head, raining.</summary>
	public const string RainCloud = """
		-- Rain cloud: a dark cloud over the head, raining.

		[Parameter( "Cloud color", Group = "Look", Tip = "The cloud's colour" )]
		local cloudColor = Color.Parse( "#5a6270" )

		[Parameter( "Rain color", Group = "Look", Tip = "The drops' colour" )]
		local rainColor = Color.Parse( "#9fc6ff" )

		[Parameter( "Drops a second", Min = 0, Max = 100, Step = 1, Group = "Amount", Tip = "How hard it rains" )]
		local rainfall = 40

		Effect.Name = "Rain cloud"
		Effect.Joint = Joint.Crown

		-- Puffs of dark cloud ten units over the head, moving with it
		local cloud = Sprite()
		cloud.ComesFrom = ComesFrom.Sphere
		cloud.Spread = 6
		cloud.Offset = Vector3( 0, 0, 10 )
		cloud.LooksLike = Picture.Smoke
		cloud.Color = cloudColor
		cloud.ColorEnd = Color.Parse( "#3a404a" )
		cloud.Size = 8
		cloud.SizeEnd = 10
		cloud.Lifetime = 2
		cloud.Amount = 8
		cloud.Follow = true
		cloud.Solid = true

		-- Drops falling fast out of it
		local rain = Sprite()
		rain.ComesFrom = ComesFrom.Sphere
		rain.Spread = 6
		rain.Offset = Vector3( 0, 0, 7 )
		rain.LooksLike = Picture.Droplet
		rain.Color = rainColor
		rain.Size = 3.5
		rain.Gravity = 260
		rain.Lifetime = 0.5
		rain.Amount = rainfall
		""";
}
