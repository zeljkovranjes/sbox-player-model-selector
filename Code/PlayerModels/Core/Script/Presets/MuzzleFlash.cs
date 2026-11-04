namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A burst of fire at the muzzle with each shot, lighting up around it for a moment.</summary>
	public const string MuzzleFlash = """
		-- Muzzle flash: a burst of fire at the weapon's muzzle with each shot, lighting up around it for a moment.

		[Parameter( "Flame color", Group = "Look", Tip = "The colour the flames start as" )]
		local flameColor = Color.Parse( "#fff0a0" )

		[Parameter( "Light color", Group = "Look", Tip = "The colour it lights things" )]
		local lightColor = Color.Parse( "#ffb25a" )

		Effect.Name = "Muzzle flash"
		Effect.Anchor = Anchor.HeldWeapon
		Effect.Joint = Joint.RightHand

		-- Flames thrown forwards out of a little cone, moving with the weapon
		local flames = Sprite()
		flames.ComesFrom = ComesFrom.Cone
		flames.Spread = 3
		flames.LooksLike = Picture.Flame
		flames.Color = flameColor
		flames.ColorEnd = Color.Parse( "#ff6a1a" )
		flames.Size = 5
		flames.SizeEnd = 1.5
		flames.Speed = 40
		flames.Lifetime = 0.1
		flames.Amount = 0
		flames.Follow = true
		flames.When = When.OnShoot
		flames.Burst = 6

		-- A glow at the muzzle behind them
		local glow = Sprite()
		glow.Color = Color.Parse( "#ffe08a" )
		glow.ColorEnd = Color.Parse( "#ff8a2a" )
		glow.Size = 7
		glow.SizeEnd = 3
		glow.Lifetime = 0.1
		glow.Amount = 0
		glow.When = When.OnShoot
		glow.Burst = 2

		-- A bright light for a moment after each shot
		local light = Light()
		light.Color = lightColor
		light.Size = 140
		light.Amount = 2
		light.When = When.OnShoot
		light.ShowFor = 0.06
		""";
}
