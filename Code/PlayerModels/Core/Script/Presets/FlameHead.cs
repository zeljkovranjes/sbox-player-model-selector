namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>The head on fire, lighting up around it.</summary>
	public const string FlameHead = """
		-- Flame head: the head on fire, lighting up around it.

		[Parameter( "Flame color", Group = "Look", Tip = "The colour flames start as" )]
		local flameColor = Color.Parse( "#ffd25a" )

		[Parameter( "Flame size", Min = 1, Max = 24, Step = 0.5, Group = "Look", Tip = "How big the flames start" )]
		local flameSize = 7

		Effect.Name = "Flame head"
		Effect.Joint = Joint.Crown

		-- Flames rising off the top of the head
		local flames = Sprite()
		flames.ComesFrom = ComesFrom.Sphere
		flames.Spread = 4
		flames.Offset = Vector3( 0, 0, -1 )
		flames.LooksLike = Picture.Flame
		flames.Color = flameColor
		flames.ColorEnd = Color.Parse( "#ff3300" )
		flames.Size = flameSize
		flames.SizeEnd = 1.5
		flames.Speed = 5
		flames.Gravity = -100
		flames.Lifetime = 0.6
		flames.Amount = 40

		-- A flickering orange light around the head
		local light = Light()
		light.Offset = Vector3( 0, 0, 2 )
		light.Color = flameColor
		light.Size = 70
		light.Amount = 1.2
		light.Rhythm = Rhythm.Flicker
		light.RhythmSpeed = 8
		light.RhythmAmount = 0.3
		""";
}
