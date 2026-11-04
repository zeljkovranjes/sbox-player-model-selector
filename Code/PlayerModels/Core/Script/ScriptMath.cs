using System;

namespace PlayerModels.Core.Script;

/// <summary>
/// The arithmetic behind the library, shared by the compiler (folding literals) and the machine (running them): safe
/// numbers (never NaN or infinite), colours from hex text and HSV, smooth noise and seeded randomness that every client
/// works out the same. Nothing here allocates.
/// </summary>
public static class ScriptMath
{
	/// <summary>
	/// <paramref name="value"/> made safe: NaN is 0, and an infinity the largest number of its sign
	/// (<see cref="ScriptLimits.MaxNumber"/>).
	/// </summary>
	public static double Safe( double value )
	{
		if ( double.IsFinite( value ) )
			return value;

		return double.IsNaN( value ) ? 0 : value > 0 ? ScriptLimits.MaxNumber : -ScriptLimits.MaxNumber;
	}

	/// <summary>a / b, 0 when b is 0.</summary>
	public static double Divide( double a, double b ) => b == 0 ? 0 : Safe( a / b );

	/// <summary>
	/// 0 at or below <paramref name="from"/>, 1 at or above <paramref name="to"/>, easing smoothly between (like HLSL's
	/// smoothstep); a step at <paramref name="from"/> when the two are the same.
	/// </summary>
	public static double SmoothStep( double from, double to, double x )
	{
		if ( from == to )
			return x < from ? 0 : 1;

		var t = Math.Clamp( Safe( (x - from) / (to - from) ), 0, 1 );
		return t * t * (3 - 2 * t);
	}

	/// <summary>C#'s remainder (its sign is a's), 0 when b is 0.</summary>
	public static double Remainder( double a, double b ) => b == 0 ? 0 : Safe( a % b );

	/// <summary>
	/// Reads a colour written as <c>#rgb</c>, <c>#rrggbb</c> or <c>#rrggbbaa</c> (case doesn't matter) into channels from
	/// 0 to 1; false for anything else.
	/// </summary>
	public static bool TryParseColor( string text, out double r, out double g, out double b, out double a )
	{
		r = g = b = 0;
		a = 1;
		if ( text is null || text.Length is not (4 or 7 or 9) || text[ 0 ] != '#' )
			return false;

		if ( text.Length == 4 )
		{
			if ( !TryHex( text[ 1 ], out var rh ) || !TryHex( text[ 2 ], out var gh ) || !TryHex( text[ 3 ], out var bh ) )
				return false;

			(r, g, b) = (rh * 17 / 255.0, gh * 17 / 255.0, bh * 17 / 255.0);
			return true;
		}

		if ( !TryByte( text, 1, out var red ) || !TryByte( text, 3, out var green ) || !TryByte( text, 5, out var blue ) )
			return false;

		var alpha = 255;
		if ( text.Length == 9 && !TryByte( text, 7, out alpha ) )
			return false;

		(r, g, b, a) = (red / 255.0, green / 255.0, blue / 255.0, alpha / 255.0);
		return true;
	}

	private static bool TryByte( string text, int at, out int value )
	{
		value = 0;
		if ( !TryHex( text[ at ], out var high ) || !TryHex( text[ at + 1 ], out var low ) )
			return false;

		value = high * 16 + low;
		return true;
	}

	private static bool TryHex( char c, out int value )
	{
		value = c switch
		{
			>= '0' and <= '9' => c - '0',
			>= 'a' and <= 'f' => c - 'a' + 10,
			>= 'A' and <= 'F' => c - 'A' + 10,
			_ => -1,
		};

		return value >= 0;
	}

	/// <summary>The colour of hue <paramref name="hue"/> (degrees, any), saturation and value (0 to 1), as r, g, b.</summary>
	public static (double R, double G, double B) FromHsv( double hue, double saturation, double value )
	{
		hue = Safe( hue ) % 360;
		if ( hue < 0 )
			hue += 360;

		saturation = Math.Clamp( Safe( saturation ), 0, 1 );
		value = Math.Clamp( Safe( value ), 0, 1 );
		var chroma = value * saturation;
		var sector = hue / 60;
		var x = chroma * (1 - Math.Abs( sector % 2 - 1 ));
		var (r, g, b) = (int)sector switch
		{
			0 => (chroma, x, 0.0),
			1 => (x, chroma, 0.0),
			2 => (0.0, chroma, x),
			3 => (0.0, x, chroma),
			4 => (x, 0.0, chroma),
			_ => (chroma, 0.0, x),
		};

		var m = value - chroma;
		return (r + m, g + m, b + m);
	}

	/// <summary>A number from 0 up to 1 picked by <paramref name="seed"/>: the same seed, the same number, everywhere.</summary>
	public static double Random( double seed ) => Hash( BitConverter.DoubleToInt64Bits( Safe( seed ) + 0.0 ) );

	/// <summary>Smooth value noise from 0 to 1 at (<paramref name="x"/>, <paramref name="y"/>): nearby places, nearby values.</summary>
	public static double Noise( double x, double y )
	{
		x = Math.Clamp( Safe( x ), -ScriptLimits.MaxNumber, ScriptLimits.MaxNumber );
		y = Math.Clamp( Safe( y ), -ScriptLimits.MaxNumber, ScriptLimits.MaxNumber );
		var cellX = Math.Floor( x );
		var cellY = Math.Floor( y );
		var fx = Smooth( x - cellX );
		var fy = Smooth( y - cellY );
		var (ix, iy) = ((long)cellX, (long)cellY);
		var a = Hash( Cell( ix, iy ) );
		var b = Hash( Cell( ix + 1, iy ) );
		var c = Hash( Cell( ix, iy + 1 ) );
		var d = Hash( Cell( ix + 1, iy + 1 ) );
		return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
	}

	private static double Smooth( double t ) => t * t * (3 - 2 * t);

	private static long Cell( long x, long y ) => unchecked(x * 73856093L ^ y * 19349663L);

	// SplitMix64 to a double in [0, 1)
	private static double Hash( long value )
	{
		unchecked
		{
			var z = (ulong)value + 0x9E3779B97F4A7C15UL;
			z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
			z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
			z ^= z >> 31;
			return (z >> 11) * (1.0 / (1UL << 53));
		}
	}
}
