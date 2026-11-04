namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>World at War's "rain" as a sticker script: the palette scrolling through the elements every 100 ms.</summary>
	public const string Rain = """
		-- World at War's "rain": every 100 ms the palette moves one element along, so the colours scroll.
		local step = Math.Floor( time / 100 )
		color = palette[ (step + i) % palette.Count ]
		""";

	/// <summary>World at War's "cycl" as a sticker script: one colour wiping across the elements, then the next.</summary>
	public const string Cycl = """
		-- World at War's "cycl": a colour wipes across the elements one step (100 ms) at a time; once it has covered
		-- them all, the next colour starts.
		local base = Math.Floor( time / (count * 100) ) % palette.Count
		local position = Math.Floor( time / 100 ) % count
		if i < position then
			color = palette[ base ]
		else
			color = palette[ (base + palette.Count - 1) % palette.Count ]
		end
		""";

	/// <summary>World at War's "cyln" as a sticker script: one highlighted element sweeping back and forth.</summary>
	public const string Cyln = """
		-- World at War's "cyln": a scanner eye sweeping back and forth across the elements; it is the highlight colour,
		-- the rest are the other.
		local position = Math.Floor( (Math.Cos( time * 0.004 ) + 1) * 0.5 * count )
		color = if i == position then highlight else rest
		""";
}
