namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>A sticker's rainbow: once round the colour wheel every two seconds, each sticker of a row further round.</summary>
	public const string StickerRainbow = """
		-- Rainbow: once round the colour wheel every two seconds; each sticker of a row (i of count) starts further round.
		color = Color.FromHsv( (t * 0.5 + i / count) % 1 * 360, 1, 1 )
		""";

	/// <summary>A sticker's pulse: its own colour (the palette's first) brighter and dimmer once a second, down to half.</summary>
	public const string StickerPulse = """
		-- Pulse: the sticker's own colour (the palette's first) brighter and dimmer once a second, down to half.
		local level = 1 - 0.5 * (0.5 - 0.5 * Math.Cos( t * Math.PI * 2 ))
		color = Color.Lerp( Color.Black, palette[ 0 ], level )
		""";

	/// <summary>The script a sticker's fill moving as <paramref name="motion"/> runs, per sticker; null for none.</summary>
	public static string StickerMotionScript( Sticker.Motion motion ) => motion switch
	{
		Sticker.Motion.Rainbow => StickerRainbow,
		Sticker.Motion.Pulse => StickerPulse,
		Sticker.Motion.Rain => Rain,
		Sticker.Motion.Cycl => Cycl,
		Sticker.Motion.Cyln => Cyln,
		_ => null,
	};
}
