using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlayerModels.Core;

/// <summary>
/// A shape a player made in the shape creator, drawn by hand, written as text or put together from others: its strokes
/// (see <see cref="ShapeStroke"/>) as quantized points, and, for a sticker, how it is coloured (see <see cref="Sticker"/>). A
/// layer uses one by its id (see <see cref="Reference"/>) as a formation sprites come from, or as the picture each sprite
/// is; a look carries each it uses once, so it shows on every machine with nothing else to fetch. Its id is worked out
/// from what it is, so the same shape is the same id everywhere; what it looks like is drawn on each machine, once for
/// everyone wearing it.
/// </summary>
public sealed class DrawnShape
{
	/// <summary>The most points a shape has, all its strokes together, and the most strokes.</summary>
	public const int MaxPoints = 500;
	public const int MaxStrokes = 64;

	/// <summary>The most shapes a look carries.</summary>
	public const int MaxPerLook = 8;

	/// <summary>The longest name.</summary>
	public const int MaxNameLength = 32;

	/// <summary>What a layer's shape or picture starts with when it is a drawn shape: then its id.</summary>
	public const char ReferencePrefix = '@';

	// A stroke is its kind, then each point as four digits of 64: twelve bits for x, twelve for y
	private const char ClosedMark = 'c';
	private const char OpenMark = 'o';
	private const int DigitsPerPoint = 4;
	private const int Levels = 4095;
	private const string Digits = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
	private static readonly int[] DigitValues = MakeDigitValues();

	// An id is sixteen hex digits of a 64-bit FNV-1a hash of what the shape is
	private const int IdLength = 16;
	private const ulong HashStart = 14695981039346656037UL;
	private const ulong HashPrime = 1099511628211UL;

	/// <summary>Which shape it is, worked out from what it is (see <see cref="Content"/>); set by <see cref="Sanitized"/>.</summary>
	[JsonPropertyName( "id" )]
	public string Id { get; set; } = "";

	/// <summary>What its maker called it; null for nothing.</summary>
	[JsonPropertyName( "n" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Name { get; set; }

	/// <summary>Its strokes as they are kept: a kind (c closed, o open), then the points.</summary>
	[JsonPropertyName( "s" )]
	public List<string> Strokes { get; set; } = new();

	/// <summary>How it is coloured as a sticker; null for a plain shape, which takes its layer's colour.</summary>
	[JsonPropertyName( "st" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public Sticker Sticker { get; set; }

	/// <summary>The Workshop share it was used or forked from; null for one made here.</summary>
	[JsonPropertyName( "ff" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public ShareOrigin ForkedFrom { get; set; }

	/// <summary>
	/// What it is, as one string: its strokes and how it is coloured, its name and origin aside. Two shapes are the same
	/// exactly when this is, so what each looks like is kept by it, never by the shorter id.
	/// </summary>
	[JsonIgnore]
	public string Content => _content ??= ContentOf( Strokes, Sticker );

	// Worked out once: a shape is never changed once read, only copied
	private string _content;

	/// <summary>How a layer names it: <see cref="ReferencePrefix"/> and its id.</summary>
	[JsonIgnore]
	public string Reference => ReferencePrefix + Id;

	/// <summary>Whether <paramref name="name"/> (a layer's shape or picture) names a drawn shape rather than a standard one.</summary>
	public static bool IsReference( string name )
	{
		if ( name is null || name.Length != IdLength + 1 || name[ 0 ] != ReferencePrefix )
			return false;

		for ( var i = 1; i < name.Length; i++ )
		{
			if ( !char.IsAsciiHexDigitLower( name[ i ] ) )
				return false;
		}

		return true;
	}

	/// <summary>
	/// A shape of <paramref name="strokes"/> (kept to <see cref="MaxStrokes"/> and <see cref="MaxPoints"/>, each point held
	/// to the square), named <paramref name="name"/>, a sticker when <paramref name="sticker"/> is given; null when there
	/// is nothing to see (see <see cref="Sanitized"/>).
	/// </summary>
	public static DrawnShape From( IEnumerable<ShapeStroke> strokes, string name = null, Sticker sticker = null, ShareOrigin forkedFrom = null )
	{
		var kept = new List<string>();
		var points = 0;
		foreach ( var stroke in strokes )
		{
			var count = Math.Min( stroke.Count, MaxPoints - points );
			if ( kept.Count >= MaxStrokes || count <= 0 )
				break;

			var text = new StringBuilder( 1 + count * DigitsPerPoint );
			text.Append( stroke.Closed && count >= 3 ? ClosedMark : OpenMark );
			for ( var i = 0; i < count; i++ )
			{
				Append( text, stroke.Points[ i * 2 ] );
				Append( text, stroke.Points[ i * 2 + 1 ] );
			}

			kept.Add( text.ToString() );
			points += count;
		}

		return new DrawnShape { Name = name, Strokes = kept, Sticker = sticker, ForkedFrom = forkedFrom }.Sanitized();
	}

	/// <summary>Its strokes, read back.</summary>
	public List<ShapeStroke> Decode()
	{
		var strokes = new List<ShapeStroke>();
		foreach ( var text in Strokes ?? new() )
		{
			var count = (text.Length - 1) / DigitsPerPoint;
			var points = new float[ count * 2 ];
			for ( var i = 0; i < count; i++ )
			{
				points[ i * 2 ] = Coordinate( text, 1 + i * DigitsPerPoint );
				points[ i * 2 + 1 ] = Coordinate( text, 3 + i * DigitsPerPoint );
			}

			strokes.Add( new ShapeStroke { Closed = text[ 0 ] == ClosedMark, Points = points } );
		}

		return strokes;
	}

	/// <summary>Its outlines, closed ones (filled) and open lines, each its points as x, y pairs: what it is drawn and spread from.</summary>
	public (List<float[]> Closed, List<float[]> Open) Outline()
	{
		var closed = new List<float[]>();
		var open = new List<float[]>();
		foreach ( var stroke in Decode() )
			(stroke.Closed ? closed : open).Add( stroke.Points );

		return (closed, open);
	}

	/// <summary>How many points it has, all its strokes together.</summary>
	[JsonIgnore]
	public int PointCount
	{
		get
		{
			var points = 0;
			foreach ( var text in Strokes ?? new() )
				points += (text.Length - 1) / DigitsPerPoint;

			return points;
		}
	}

	/// <summary>
	/// A copy within bounds with its id worked out again (never the one it came with), or null when there is nothing to
	/// see: at most <see cref="MaxStrokes"/> strokes and <see cref="MaxPoints"/> points (the rest cut off), a stroke with
	/// anything but digits left out, a closed one of fewer than three points open, and a shape with no size at all
	/// (every point the same) refused. Checked from the text's length before anything else, so a tampered one costs no
	/// more than its length.
	/// </summary>
	public DrawnShape Sanitized()
	{
		var kept = new List<string>();
		var points = 0;
		var (minX, maxX, minY, maxY) = (int.MaxValue, int.MinValue, int.MaxValue, int.MinValue);
		foreach ( var text in Strokes ?? new() )
		{
			if ( kept.Count >= MaxStrokes || points >= MaxPoints )
				break;

			if ( text is null || text.Length < 1 + DigitsPerPoint || text[ 0 ] is not (ClosedMark or OpenMark) )
				continue;

			var count = Math.Min( (text.Length - 1) / DigitsPerPoint, MaxPoints - points );
			var end = 1 + count * DigitsPerPoint;
			if ( !AllDigits( text, end ) )
				continue;

			for ( var i = 0; i < count; i++ )
			{
				var x = Level( text, 1 + i * DigitsPerPoint );
				var y = Level( text, 3 + i * DigitsPerPoint );
				(minX, maxX, minY, maxY) = (Math.Min( minX, x ), Math.Max( maxX, x ), Math.Min( minY, y ), Math.Max( maxY, y ));
			}

			var closed = text[ 0 ] == ClosedMark && count >= 3;
			kept.Add( (closed ? ClosedMark : OpenMark) + text.Substring( 1, end - 1 ) );
			points += count;
		}

		if ( kept.Count == 0 || minX == maxX && minY == maxY )
			return null;

		var name = SharePost.Tidy( Name, MaxNameLength );
		var sticker = Sticker?.Sanitized();
		return new DrawnShape
		{
			Id = IdOf( ContentOf( kept, sticker ) ),
			Name = name == "" ? null : name,
			Strokes = kept,
			Sticker = sticker,
			ForkedFrom = ForkedFrom?.Sanitized(),
		};
	}

	/// <summary>The longest shape read from the Workshop or a code, as JSON: past the longest a shape within its caps can be.</summary>
	public const int MaxSharedLength = 16 * 1024;

	/// <summary>A shape shared as JSON (see <see cref="SharedForm"/>), within bounds, or null when it isn't one.</summary>
	public static DrawnShape ParseShared( string json )
	{
		if ( string.IsNullOrWhiteSpace( json ) || json.Length > MaxSharedLength || !json.TrimStart().StartsWith( '{' ) )
			return null;

		try
		{
			return JsonSerializer.Deserialize<DrawnShape>( json )?.Sanitized();
		}
		catch ( JsonException )
		{
			return null;
		}
	}

	/// <summary>The shape as it is shared: within bounds.</summary>
	public string SharedForm() => Sanitized() is { } copy ? JsonSerializer.Serialize( copy ) : "";

	/// <summary>A copy with another name, kept within bounds.</summary>
	public DrawnShape Named( string name ) => new DrawnShape { Name = name, Strokes = Strokes, Sticker = Sticker, ForkedFrom = ForkedFrom }.Sanitized();

	private static string ContentOf( List<string> strokes, Sticker sticker )
	{
		var style = sticker is null ? "" : JsonSerializer.Serialize( sticker );
		return string.Join( "|", strokes ?? new() ) + "#" + style;
	}

	private static string IdOf( string content )
	{
		var hash = HashStart;
		foreach ( var character in content )
		{
			hash ^= character;
			hash *= HashPrime;
		}

		return hash.ToString( "x16" );
	}

	private static void Append( StringBuilder text, float value )
	{
		var level = (int)MathF.Round( (Math.Clamp( float.IsFinite( value ) ? value : 0f, -1f, 1f ) + 1f) * 0.5f * Levels );
		text.Append( Digits[ level >> 6 ] );
		text.Append( Digits[ level & 63 ] );
	}

	private static int Level( string text, int at ) => DigitValues[ text[ at ] ] << 6 | DigitValues[ text[ at + 1 ] ];

	private static float Coordinate( string text, int at ) => Level( text, at ) / (float)Levels * 2f - 1f;

	private static bool AllDigits( string text, int end )
	{
		for ( var i = 1; i < end; i++ )
		{
			if ( text[ i ] >= DigitValues.Length || DigitValues[ text[ i ] ] < 0 )
				return false;
		}

		return true;
	}

	private static int[] MakeDigitValues()
	{
		var values = new int[ 128 ];
		Array.Fill( values, -1 );
		for ( var i = 0; i < Digits.Length; i++ )
			values[ Digits[ i ] ] = i;

		return values;
	}
}
