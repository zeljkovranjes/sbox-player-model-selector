using System;
using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>
/// The animated clan tag colourings of Call of Duty: World at War, as an effect's colours: "rain" scrolls a gradient
/// through every element, "cycl" wipes one colour across them, "cyln" sweeps a single highlight back and forth. Ported
/// exactly from https://github.com/zeljkovranjes/waw-color-tags (reverse engineered from CoDWaW.exe): the same palette,
/// timing and per-element rules. An "element" is whatever the effect colours one by one (its particles, or the points of
/// its shape), counted 0..<c>count</c>-1. A palette of the player's own, of any length, takes the place of the game's
/// five colours; with the game's palette every result is the game's.
/// </summary>
public static class ColorCycle
{
	public enum Kind { Rain, Cycl, Cyln }

	/// <summary>The engine colour table the <c>^1</c>..<c>^5</c> codes read (<c>g_color_table</c> @ 0x00838680).</summary>
	[Sandbox.SkipHotload]
	public static readonly IReadOnlyList<string> GamePalette = new[] { "#FF3333", "#00FF00", "#FFFF00", "#0000FF", "#00FFFF" };

	/// <summary>The scanner eye of "cyln": <c>^1</c> red, everything else <c>^7</c> white.</summary>
	public const string ScannerHighlight = "#FF3333";
	public const string ScannerRest = "#FFFFFF";

	// Every animator steps on a 100 ms clock (timeGetTime, /100 via 0x51EB851F)
	private const long StepMs = 100;

	// "cyln": pos = floor((cos(t * 0.004) + 1) * 0.5 * L), constants @0x8AF8F4, 0x826A4C, 0x82B678
	private const double ScannerSpeed = 0.004;

	/// <summary>
	/// The palette index element <paramref name="i"/> of <paramref name="count"/> shows <paramref name="ms"/> milliseconds
	/// into the effect, for a palette of <paramref name="paletteSize"/> colours ("rain" and "cycl"). -1 for anything out
	/// of range.
	/// </summary>
	public static int PaletteIndex( Kind kind, long ms, int i, int count, int paletteSize )
	{
		if ( count <= 0 || paletteSize <= 0 || i < 0 || i >= count || ms < 0 )
			return -1;

		switch ( kind )
		{
			case Kind.Rain:
			{
				// rain.js: base = floor(t / 100) % 5; color[i] = (base + i) % 5
				var bas = (int)(ms / StepMs % paletteSize);
				return (bas + i) % paletteSize;
			}

			case Kind.Cycl:
			{
				// cycl.js: base = floor(t / (L*100)) % 5; pos = floor(t / 100) % L
				// color[i] = (i < pos) ? base : (base+4)%5   ((base+4)%5 is base-1 for five colours)
				var bas = (int)(ms / (count * StepMs) % paletteSize);
				var pos = (int)(ms / StepMs % count);
				return i < pos ? bas : (bas + paletteSize - 1) % paletteSize;
			}

			default:
				return -1;
		}
	}

	/// <summary>
	/// Whether element <paramref name="i"/> of <paramref name="count"/> is the "cyln" scanner's highlight
	/// <paramref name="ms"/> milliseconds in; every other element shows the rest colour.
	/// </summary>
	public static bool IsScannerHighlight( long ms, int i, int count )
	{
		if ( count <= 0 || ms < 0 )
			return false;

		// cyln.js: pos = floor((cos(t * 0.004) + 1) * 0.5 * L); color[i] = (i == pos) ? red : white
		var pos = (int)Math.Floor( (Math.Cos( ms * ScannerSpeed ) + 1) * 0.5 * count );
		return i == pos;
	}
}
