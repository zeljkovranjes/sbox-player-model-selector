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
/// everyone wearing it. An emblem is a shape made of a stack of layers instead (see <see cref="Emblem"/>), each a
/// standard shape or one of the drawn shapes it carries (<see cref="Parts"/>), placed, coloured and cut out on its own.
/// </summary>
public sealed class DrawnShape
{
	/// <summary>The most points a shape has, all its strokes together, and the most strokes.</summary>
	public const int MaxPoints = 500;
	public const int MaxStrokes = 64;

	/// <summary>The most shapes a look carries.</summary>
	public const int MaxPerLook = 8;

	/// <summary>The most points an emblem's drawn layers have, all together, and the most drawn shapes it carries.</summary>
	public const int MaxEmblemPoints = 2000;
	public const int MaxParts = EmblemLayer.MaxPerEmblem;

	/// <summary>
	/// The most points the shapes a look carries have, all together, and the most emblem layers: an emblem is many shapes
	/// in one, and the look still has to fit in <see cref="PlayerLook.MaxTextLength"/>.
	/// </summary>
	public const int MaxLookPoints = 4000;
	public const int MaxLookEmblemLayers = 2 * EmblemLayer.MaxPerEmblem;

	/// <summary>How wide a wide line or an eraser's stroke is, at least and at most, on the square (2 across).</summary>
	public const float MinStrokeWidth = 0.01f;
	public const float MaxStrokeWidth = 0.8f;

	/// <summary>The longest name.</summary>
	public const int MaxNameLength = 32;

	/// <summary>What a layer's shape or picture starts with when it is a drawn shape: then its id.</summary>
	public const char ReferencePrefix = '@';

	// A stroke is its kind (and a wide line's or an eraser's width as two digits of 64), then each point as four digits of
	// 64: twelve bits for x, twelve for y
	private const char ClosedMark = 'c';
	private const char OpenMark = 'o';
	private const char WideMark = 'w';
	private const char EraseMark = 'e';
	private const int WidthDigits = 2;
	private const int DigitsPerPoint = 4;
	private const int Levels = 4095;
	private const string Digits = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
	[Sandbox.SkipHotload]
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

	/// <summary>
	/// For a shape drawn as a sprite's picture, where a fade round its middle starts, in degrees anticlockwise from the
	/// right (0 up to 360): nothing of it there (a slash's tail), growing to all of it <see cref="FadeArc"/> further round
	/// (its front). See <see cref="FadeAt"/>.
	/// </summary>
	[JsonPropertyName( "fa" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float FadeFrom { get; set; }

	/// <summary>How far round the fade reaches all of it, in degrees: anticlockwise, or clockwise below 0; 0 for no fade.</summary>
	[JsonPropertyName( "fr" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float FadeArc { get; set; }

	/// <summary>The Workshop share it was used or forked from; null for one made here.</summary>
	[JsonPropertyName( "ff" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public ShareOrigin ForkedFrom { get; set; }

	/// <summary>
	/// An emblem's layers, from the bottom of the stack to the top (see <see cref="EmblemLayer"/>); null for a shape that
	/// isn't one.
	/// </summary>
	[JsonPropertyName( "em" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<EmblemLayer> Emblem { get; set; }

	/// <summary>The drawn shapes an emblem's layers name (by <see cref="Reference"/>), each once; null for none.</summary>
	[JsonPropertyName( "pt" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<DrawnShape> Parts { get; set; }

	/// <summary>Whether an emblem is drawn again flipped left to right, as one half of a symmetric picture.</summary>
	[JsonPropertyName( "mir" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool Mirror { get; set; }

	/// <summary>
	/// A sticker of layers' own script (a sticker script, see <see cref="PlayerModels.Core.Script.ScriptKind.Sticker"/>),
	/// minified: run for each of its layers as it shows (<c>i</c> the layer, <c>origin</c> its place) to colour it, and for
	/// each sprite showing it to fade, size, turn or move the sprite. Null for none. Held to
	/// <see cref="PlayerModels.Core.Script.ScriptLimits.MaxScriptBytes"/>, and with the look's other scripts to
	/// <see cref="PlayerModels.Core.Script.ScriptLimits.MaxLookBytes"/>. Not part of its <see cref="Id"/> (a sticker whose
	/// script a host turns off is still the sticker its layers name), but part of its <see cref="Content"/>.
	/// </summary>
	[JsonPropertyName( "sc" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Script { get; set; }

	/// <summary>What a sticker is printed on (see <see cref="StickerFinish"/>) by name; null for paper.</summary>
	[JsonPropertyName( "fin" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Finish { get; set; }

	/// <summary>What it is printed on; paper for a plain shape.</summary>
	[JsonIgnore]
	public StickerFinish FinishIs => IsSticker ? StickerFinishes.Of( Finish ) : StickerFinish.Paper;

	/// <summary>
	/// A lenticular sticker's second look: its layers but those of the first look alone (see <see cref="EmblemLayer.Side"/>);
	/// null for a sticker that isn't lenticular.
	/// </summary>
	public DrawnShape SecondLook() => FinishIs != StickerFinish.Lenticular || !IsEmblem ? null : new DrawnShape
	{
		Emblem = Emblem.FindAll( x => x.Side != EmblemLayer.FirstLook ), Parts = Parts, Mirror = Mirror, Script = Script,
	}.Sanitized();

	/// <summary>Whether it is a sticker (an emblem of layers, or one coloured whole as stickers were before them), not a plain shape.</summary>
	[JsonIgnore]
	public bool IsSticker => IsEmblem || Sticker is not null;

	/// <summary>Whether it is an emblem: a stack of layers rather than strokes.</summary>
	[JsonIgnore]
	public bool IsEmblem => Emblem is { Count: > 0 };

	/// <summary>Whether it has colours of its own (a sticker or an emblem), rather than taking its layer's colour.</summary>
	[JsonIgnore]
	public bool HasOwnColours => IsSticker;

	/// <summary>
	/// What it is, as one string: its strokes and how it is coloured, its name and origin aside. Two shapes are the same
	/// exactly when this is, so what each looks like is kept by it, never by the shorter id.
	/// </summary>
	[JsonIgnore]
	public string Content => _content ??= ContentOf( Strokes, Sticker, FadeFrom, FadeArc, Emblem, Parts, Mirror, Finish ) + (Script is null ? "" : "#script:" + Script);

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

			var text = new StringBuilder( 1 + WidthDigits + count * DigitsPerPoint );
			var kind = stroke.Erase ? EraseMark : stroke.Closed && count >= 3 ? ClosedMark : stroke.Width > 0f ? WideMark : OpenMark;
			text.Append( kind );
			if ( HasWidth( kind ) )
				AppendWidth( text, stroke.Width );

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

	/// <summary>Its strokes, read back (an eraser's among them).</summary>
	public List<ShapeStroke> Decode()
	{
		var strokes = new List<ShapeStroke>();
		foreach ( var text in Strokes ?? new() )
		{
			var kind = text[ 0 ];
			var start = HeaderOf( kind );
			var count = (text.Length - start) / DigitsPerPoint;
			var points = new float[ count * 2 ];
			for ( var i = 0; i < count; i++ )
			{
				points[ i * 2 ] = Coordinate( text, start + i * DigitsPerPoint );
				points[ i * 2 + 1 ] = Coordinate( text, start + 2 + i * DigitsPerPoint );
			}

			var width = HasWidth( kind ) ? WidthAt( text ) : 0f;
			strokes.Add( new ShapeStroke { Closed = kind == ClosedMark, Erase = kind == EraseMark, Width = width, Points = points } );
		}

		return strokes;
	}

	/// <summary>
	/// Its outlines, closed ones (filled) and open lines, each its points as x, y pairs: what it is drawn and spread from.
	/// An emblem's are its layers' outlines, together, where they are on it (see <see cref="Core.Emblem.Outline"/>); an
	/// eraser's strokes are no outline.
	/// </summary>
	public (List<float[]> Closed, List<float[]> Open) Outline()
	{
		if ( IsEmblem )
			return Core.Emblem.Outline( this );

		var closed = new List<float[]>();
		var open = new List<float[]>();
		foreach ( var stroke in Decode() )
		{
			if ( !stroke.Erase )
				(stroke.Closed ? closed : open).Add( stroke.Points );
		}

		return (closed, open);
	}

	/// <summary>
	/// <paramref name="count"/> points to send sprites out from, as <see cref="StandardShapes.FormationOf"/> spreads them:
	/// along its outline, or with <paramref name="filled"/> all over it; for an emblem, over each layer it shows; none
	/// where an eraser cut it away. Worked out when a look changes, never each frame.
	/// </summary>
	public float[] Formation( bool filled, int count = StandardShapes.FormationPoints ) => Core.Emblem.Formation( this, filled, count );

	/// <summary>How many points it has, all its strokes together (an eraser's too), and an emblem's drawn layers'.</summary>
	[JsonIgnore]
	public int PointCount
	{
		get
		{
			var points = 0;
			foreach ( var text in Strokes ?? new() )
				points += (text.Length - HeaderOf( text[ 0 ] )) / DigitsPerPoint;

			foreach ( var part in Parts ?? new() )
				points += part?.PointCount ?? 0;

			return points;
		}
	}

	/// <summary>The drawn shape an emblem carries that <paramref name="reference"/> names; null for none.</summary>
	public DrawnShape PartOf( string reference )
	{
		if ( Parts is null || reference is null )
			return null;

		foreach ( var part in Parts )
		{
			if ( part is not null && part.Reference == reference )
				return part;
		}

		return null;
	}

	/// <summary>
	/// A copy within bounds with its id worked out again (never the one it came with), or null when there is nothing to
	/// see: at most <see cref="MaxStrokes"/> strokes and <see cref="MaxPoints"/> points (the rest cut off), a stroke with
	/// anything but digits left out, a closed one of fewer than three points open, and a shape with no size at all
	/// (every point the same, or nothing but an eraser's strokes) refused. An emblem is kept to its caps too (see
	/// <see cref="Core.Emblem.Sanitize"/>): <see cref="EmblemLayer.MaxPerEmblem"/> layers and
	/// <see cref="MaxEmblemPoints"/> points of drawn layers. Checked from the text's length before anything else, so a
	/// tampered one costs no more than its length.
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

			if ( text is null || text.Length < 1 || text[ 0 ] is not (ClosedMark or OpenMark or WideMark or EraseMark) )
				continue;

			var kind = text[ 0 ];
			var start = HeaderOf( kind );
			if ( text.Length < start + DigitsPerPoint )
				continue;

			var count = Math.Min( (text.Length - start) / DigitsPerPoint, MaxPoints - points );
			var end = start + count * DigitsPerPoint;
			if ( !AllDigits( text, end ) )
				continue;

			for ( var i = 0; i < count && kind != EraseMark; i++ )
			{
				var x = Level( text, start + i * DigitsPerPoint );
				var y = Level( text, start + 2 + i * DigitsPerPoint );
				(minX, maxX, minY, maxY) = (Math.Min( minX, x ), Math.Max( maxX, x ), Math.Min( minY, y ), Math.Max( maxY, y ));
			}

			// A closed stroke of fewer than three points is a line, at the usual width
			kind = kind == ClosedMark && count < 3 ? OpenMark : kind;
			kept.Add( kind + text.Substring( 1, end - 1 ) );
			points += count;
		}

		List<DrawnShape> parts = null;
		var emblem = Emblem is null ? null : Core.Emblem.Sanitize( Emblem, Parts, out parts );
		var drawn = minX != int.MaxValue && (minX != maxX || minY != maxY);
		if ( !drawn && emblem is null )
			return null;

		var name = SharePost.Tidy( Name, MaxNameLength );
		var sticker = emblem is null ? Sticker?.Sanitized() : null;
		var mirror = emblem is not null && Mirror;
		var finish = emblem is not null || sticker is not null ? StickerFinishes.Sanitized( Finish ) : null;
		var (from, arc) = Fade( FadeFrom, FadeArc );
		return new DrawnShape
		{
			Id = IdOf( ContentOf( kept, sticker, from, arc, emblem, emblem is null ? null : parts, mirror, finish ) ),
			Name = name == "" ? null : name,
			Strokes = kept,
			Sticker = sticker,
			FadeFrom = from,
			FadeArc = arc,
			ForkedFrom = ForkedFrom?.Sanitized(),
			Emblem = emblem,
			Parts = emblem is null ? null : parts,
			Mirror = mirror,
			Finish = finish,
			Script = emblem is not null && PlayerModels.Core.Script.ScriptLimits.Fits( Script ) ? Script : null,
		};
	}

	/// <summary>The longest shape read from the Workshop or a code, as JSON: past the longest a shape (an emblem) within its caps can be.</summary>
	public const int MaxSharedLength = 32 * 1024;

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
	public DrawnShape Named( string name ) => Credited( name, ForkedFrom );

	/// <summary>
	/// A copy fading round its middle as a picture: nothing at <paramref name="fromDegrees"/>, all of it at
	/// <paramref name="toDegrees"/> (anticlockwise when it is the larger, else clockwise; at most a whole turn); the same
	/// two for no fade.
	/// </summary>
	public DrawnShape Faded( float fromDegrees, float toDegrees ) =>
		new DrawnShape
		{
			Name = Name, Strokes = Strokes, Sticker = Sticker, FadeFrom = fromDegrees, FadeArc = toDegrees - fromDegrees, ForkedFrom = ForkedFrom,
			Emblem = Emblem, Parts = Parts, Mirror = Mirror, Finish = Finish, Script = Script,
		}.Sanitized();

	/// <summary>
	/// How much of the picture shows at (<paramref name="x"/>, <paramref name="y"/>) on its square, 0 to 1, for its fade:
	/// rising smoothly from nothing at its start to all of it at its end; past its end all of it, behind its start none
	/// (the rest of the turn split between the two). 1 everywhere without a fade, and at the very middle.
	/// </summary>
	public float FadeAt( float x, float y )
	{
		if ( FadeArc == 0f || x == 0f && y == 0f )
			return 1f;

		var angle = MathF.Atan2( y, x ) * 180f / MathF.PI;
		var round = FadeArc > 0f ? angle - FadeFrom : FadeFrom - angle;
		round -= MathF.Floor( round / FullTurn ) * FullTurn;
		var arc = MathF.Abs( FadeArc );
		if ( round > arc )
			return round - arc < (FullTurn - arc) / 2f ? 1f : 0f;

		var along = round / arc;
		return along * along * (3f - 2f * along);
	}

	private const float FullTurn = 360f;

	// A fade within bounds: its start 0 up to 360, its reach at most a turn either way; none for anything not finite
	private static (float From, float Arc) Fade( float from, float arc )
	{
		if ( !float.IsFinite( from ) || !float.IsFinite( arc ) || arc == 0f )
			return (0f, 0f);

		return (from - MathF.Floor( from / FullTurn ) * FullTurn, Math.Clamp( arc, -FullTurn, FullTurn ));
	}

	/// <summary>A copy named <paramref name="name"/>, crediting <paramref name="origin"/> (null for none), kept within bounds.</summary>
	public DrawnShape Credited( string name, ShareOrigin origin ) => new DrawnShape
	{
		Name = name, Strokes = Strokes, Sticker = Sticker, FadeFrom = FadeFrom, FadeArc = FadeArc, ForkedFrom = origin, Emblem = Emblem, Parts = Parts, Mirror = Mirror, Finish = Finish, Script = Script,
	}.Sanitized();

	/// <summary>A copy moved by <paramref name="script"/> (a sticker script, minified; null for none), kept within bounds.</summary>
	public DrawnShape Scripted( string script ) => new DrawnShape
	{
		Name = Name, Strokes = Strokes, Sticker = Sticker, FadeFrom = FadeFrom, FadeArc = FadeArc, ForkedFrom = ForkedFrom, Emblem = Emblem, Parts = Parts, Mirror = Mirror, Finish = Finish, Script = script,
	}.Sanitized();

	/// <summary>
	/// Holds the scripts of <paramref name="shapes"/> to <paramref name="bytes"/> together, in order: one past it loses its
	/// script (in place: its id stays, so whatever names it still finds it).
	/// </summary>
	internal static int HoldScriptsTo( List<DrawnShape> shapes, int bytes )
	{
		var total = 0;
		for ( var i = 0; i < shapes.Count; i++ )
		{
			if ( shapes[ i ].Script is not { } script )
				continue;

			var size = PlayerModels.Core.Script.ScriptLimits.Utf8Bytes( script );
			if ( total + size <= bytes )
				total += size;
			else
				shapes[ i ] = shapes[ i ].Scripted( null );
		}

		return total;
	}

	/// <summary>A copy printed on <paramref name="finish"/>, kept within bounds.</summary>
	public DrawnShape Finished( StickerFinish finish ) => new DrawnShape
	{
		Name = Name, Strokes = Strokes, Sticker = Sticker, FadeFrom = FadeFrom, FadeArc = FadeArc, ForkedFrom = ForkedFrom, Emblem = Emblem, Parts = Parts, Mirror = Mirror, Finish = StickerFinishes.NameOf( finish ), Script = Script,
	}.Sanitized();

	// A plain shape's content is just as it was before fades, emblems and finishes, so its id stays the same
	private static string ContentOf( List<string> strokes, Sticker sticker, float fadeFrom = 0f, float fadeArc = 0f, List<EmblemLayer> emblem = null, List<DrawnShape> parts = null, bool mirror = false, string finish = null )
	{
		var style = sticker is null ? "" : JsonSerializer.Serialize( sticker );
		var fade = fadeArc == 0f ? "" : "~" + fadeFrom.ToString( "R", System.Globalization.CultureInfo.InvariantCulture ) + "," + fadeArc.ToString( "R", System.Globalization.CultureInfo.InvariantCulture );
		var content = string.Join( "|", strokes ?? new() ) + "#" + style + fade + (finish is null ? "" : "#finish:" + finish);
		if ( emblem is null )
			return content;

		var drawn = new StringBuilder( content ).Append( "#emblem" ).Append( mirror ? "m" : "" );
		foreach ( var layer in emblem )
			drawn.Append( '#' ).Append( JsonSerializer.Serialize( layer ) );

		foreach ( var part in parts ?? new() )
			drawn.Append( '#' ).Append( part.Content );

		return drawn.ToString();
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

	private static bool HasWidth( char kind ) => kind is WideMark or EraseMark;

	// Where a stroke's points start: after its kind, and its width if it has one
	private static int HeaderOf( char kind ) => 1 + (HasWidth( kind ) ? WidthDigits : 0);

	private static void AppendWidth( StringBuilder text, float width )
	{
		var t = (Math.Clamp( float.IsFinite( width ) ? width : MinStrokeWidth, MinStrokeWidth, MaxStrokeWidth ) - MinStrokeWidth) / (MaxStrokeWidth - MinStrokeWidth);
		var level = (int)MathF.Round( t * Levels );
		text.Append( Digits[ level >> 6 ] );
		text.Append( Digits[ level & 63 ] );
	}

	private static float WidthAt( string text ) => MinStrokeWidth + Level( text, 1 ) / (float)Levels * (MaxStrokeWidth - MinStrokeWidth);

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
