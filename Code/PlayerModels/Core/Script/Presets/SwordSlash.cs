namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>The classic slash: a crescent in front with each swing, lying along it and sweeping round with it.</summary>
	public const string SwordSlash = """
		-- Sword slash: the classic slash. With each swing a crescent appears in front, lying along the swing and sweeping
		-- round with it, bright at its front and fading to nothing at its tail, gone in a moment: a white-hot edge inside a
		-- coloured one.

		[Parameter( "Slash color", Group = "Look", Tip = "The colour of the outer crescent" )]
		local slashColor = Color.Parse( "#8ad0ff" )

		[Parameter( "Reach", Min = 30, Max = 330, Step = 5, Group = "Look", Tip = "How far round the crescent reaches, in degrees" )]
		local reach = 160

		[Parameter( "Sweep", Min = -720, Max = 720, Step = 10, Group = "Motion", Tip = "How far it turns with the swing as it fades, in degrees" )]
		local sweep = 110

		Effect.Name = "Sword slash"
		Effect.Joint = Joint.Chest

		-- The coloured crescent, a step in front of the chest, lying along each swing (a swing counts as a shot)
		local slash = Sprite()
		slash.Offset = Vector3( 20, 0, 6 )
		slash.LooksLike = Picture.Slash
		slash.Color = slashColor
		slash.ColorEnd = Color.Parse( "#2a5aff" )
		slash.Size = 44
		slash.SizeEnd = 46
		slash.Lifetime = 0.28
		slash.Amount = 0
		slash.Follow = true
		slash.When = When.OnShoot
		slash.Burst = 1
		slash.Align = Align.Swing
		slash.Sweep = sweep
		slash.SlashArc = reach
		slash.SlashThickness = 0.3

		-- Its white-hot edge, a little shorter and thinner, gone sooner
		local edge = Sprite()
		edge.Offset = Vector3( 20, 0, 6 )
		edge.LooksLike = Picture.Slash
		edge.Color = Color.White
		edge.ColorEnd = Color.Parse( "#bfe6ff" )
		edge.Size = 40
		edge.SizeEnd = 42
		edge.Lifetime = 0.2
		edge.Amount = 0
		edge.Follow = true
		edge.When = When.OnShoot
		edge.Burst = 1
		edge.Align = Align.Swing
		edge.Sweep = sweep
		edge.SlashArc = reach - 20
		edge.SlashThickness = 0.12
		""";
}
