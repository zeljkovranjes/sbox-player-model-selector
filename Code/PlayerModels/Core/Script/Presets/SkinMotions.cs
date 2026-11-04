namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A weapon skin's rainbow: the colour wheel laid along the weapon, flowing forwards.</summary>
	public const string SkinRainbow = """
		-- Rainbow: the colour wheel laid along the weapon, flowing forwards once round every four seconds; painted over most
		-- of the weapon's own colour (keeping its light and dark), glowing a little.
		color = Color.FromHsv( Math.Frac( along - time * 0.00025 ) * 360, 1, 1 )
		alpha = 0.85
		glow = color * 0.12
		""";

	/// <summary>World at War's "rain" in bands along a weapon.</summary>
	public const string SkinRain = """
		-- World at War's "rain" in bands along the weapon: every 100 ms the palette moves one band along, so the colours
		-- scroll; painted over most of the weapon's own colour (keeping its light and dark), glowing a little.
		local step = Math.Floor( time / 100 )
		color = palette[ (step + i) % palette.Count ]
		alpha = 0.85
		glow = color * 0.12
		""";

	/// <summary>World at War's "cycl" in bands along a weapon.</summary>
	public const string SkinCycl = """
		-- World at War's "cycl" in bands along the weapon: a colour wipes across them one band (100 ms) at a time; once it
		-- has covered them all, the next colour starts.
		local base = Math.Floor( time / (count * 100) ) % palette.Count
		local position = Math.Floor( time / 100 ) % count
		if i < position then
			color = palette[ base ]
		else
			color = palette[ (base + palette.Count - 1) % palette.Count ]
		end
		alpha = 0.85
		glow = color * 0.12
		""";

	/// <summary>World at War's "cyln" in bands along a weapon.</summary>
	public const string SkinCyln = """
		-- World at War's "cyln" in bands along the weapon: a scanner eye sweeping back and forth over them; it is the
		-- highlight colour, the rest are the other.
		local position = Math.Floor( (Math.Cos( time * 0.004 ) + 1) * 0.5 * count )
		color = if i == position then highlight else rest
		alpha = 0.85
		glow = color * 0.12
		""";

	/// <summary>A weapon skin's pulse: the weapon's own colour brighter in the palette's first and back, once a second.</summary>
	public const string SkinPulse = """
		-- Pulse: once a second the weapon's own colour turns brighter, in the palette's first colour, and back.
		local beat = 0.5 - 0.5 * Math.Cos( t * Math.PI * 2 )
		shade = Color.White + (palette[ 0 ] * 1.4 - Color.White) * beat
		""";

	/// <summary>A weapon skin's glow: lit from within in the palette's first colour, breathing.</summary>
	public const string SkinGlow = """
		-- Glow: lit from within in the palette's first colour, breathing once a second.
		local beat = 0.6 + 0.4 * Math.Sin( t * Math.PI * 2 )
		glow = palette[ 0 ] * (beat * 1.5)
		""";

	/// <summary>A weapon skin's shimmer: a sheen sweeping along the weapon.</summary>
	public const string SkinShimmer = """
		-- Shimmer: a sheen of the palette's first colour sweeping from behind the weapon to past its front every two
		-- seconds, polishing what it passes.
		local sweep = Math.Frac( t * 0.5 ) * 1.6 - 0.3
		local offset = (along - sweep) * 16
		sheen = Math.Exp( -offset * offset )
		glow = palette[ 0 ] * (sheen * 1.2)
		""";

	/// <summary>A weapon skin's palette going round: each colour in turn, fading into the next.</summary>
	public const string SkinPalette = """
		-- Palette: the weapon painted in each colour of its palette in turn, fading into the next once a second.
		local step = time / 1000
		local at = Math.Floor( step )
		color = Color.Lerp( palette[ at % palette.Count ], palette[ (at + 1) % palette.Count ], Math.SmoothStep( 0, 1, step - at ) )
		alpha = 0.85
		glow = color * 0.12
		""";

	/// <summary>A weapon's pattern on the move: scrolling along it and spinning.</summary>
	public const string SkinPatternMotion = """
		-- A pattern on the move: scrolling along the weapon and spinning, as fast as the skin says.
		[Parameter( "Scrolls", Min = -4, Max = 4, Step = 0.05, Tip = "Its size a second along the weapon; negative the other way" )]
		local scrolls = 0
		[Parameter( "Spins", Min = -2, Max = 2, Step = 0.05, Tip = "Turns a second; negative the other way" )]
		local spins = 0
		scroll = t * scrolls
		spin = t * spins
		""";

	/// <summary>The script a weapon skin's colour moving as <paramref name="motion"/> runs, per band; null for none.</summary>
	public static string SkinMotionScript( WeaponSkin.Motion motion ) => motion switch
	{
		WeaponSkin.Motion.Rainbow => SkinRainbow,
		WeaponSkin.Motion.Rain => SkinRain,
		WeaponSkin.Motion.Cycl => SkinCycl,
		WeaponSkin.Motion.Cyln => SkinCyln,
		WeaponSkin.Motion.Pulse => SkinPulse,
		WeaponSkin.Motion.Glow => SkinGlow,
		WeaponSkin.Motion.Shimmer => SkinShimmer,
		WeaponSkin.Motion.Palette => SkinPalette,
		_ => null,
	};
}
