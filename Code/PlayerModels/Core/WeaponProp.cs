using System;
using System.Text.Json.Serialization;

namespace PlayerModels.Core;

/// <summary>
/// A model put on a weapon (a scope, a charm, a flower in the barrel): which model, where on the weapon, turned and
/// sized. Kept small whatever a look says: its longest side is never past <see cref="MaxLength"/>, and it stays within
/// the weapon's bounds grown by <see cref="Reach"/>. Only a client with both models loaded knows their sizes, so every
/// client fits it for itself (see <see cref="FittedTo"/>); the look keeps only numbers within absolute bounds.
/// </summary>
public sealed class WeaponProp
{
	/// <summary>The longest a prop's longest side is, after its scale, in units.</summary>
	public const float MaxLength = 16f;

	/// <summary>How far past the weapon's bounds a prop may reach, in units.</summary>
	public const float Reach = 8f;

	/// <summary>The smallest a prop goes, and the largest scale read at all (the size cap is the real one).</summary>
	public const float MinScale = 0.05f;
	public const float MostScale = 4f;

	/// <summary>How far from the weapon's middle a prop is read at all, along each axis: the bounds are the real cap.</summary>
	public const float MostOffset = 64f;

	/// <summary>
	/// How far a prop's first-person place may be nudged from where it is worked out to be on the first-person weapon (a
	/// different mesh), along each axis in units and around each in degrees.
	/// </summary>
	public const float MaxNudge = 4f;
	public const float MaxNudgeTurn = 45f;

	/// <summary>A model smaller than this, or larger than <see cref="AbsurdSize"/>, along its longest side isn't a prop.</summary>
	public const float DegenerateSize = 0.01f;
	public const float AbsurdSize = 4096f;

	private const int MaxModelLength = 256;
	private const string ModelExtension = ".vmdl";

	/// <summary>The model, by its path.</summary>
	[JsonPropertyName( "m" )]
	public string Model { get; set; } = "";

	/// <summary>
	/// The part of the weapon it is on (a magazine, a slide: see <see cref="WeaponDesign.IsPartName"/>), by name: it
	/// follows that part through the weapon's animations where the weapon has it, the weapon itself where it doesn't;
	/// null for the weapon as a whole. Where it is, is from the part's middle.
	/// </summary>
	[JsonPropertyName( "pt" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Part { get; set; }

	/// <summary>Whether it takes its part's place: the part is hidden, and the prop may be as large as the part.</summary>
	[JsonPropertyName( "rp" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool Replaces { get; set; }

	/// <summary>
	/// Where its middle sits on the weapon: forwards (along the barrel), to the left and up, from its part's middle, or
	/// the weapon's own origin.
	/// </summary>
	[JsonPropertyName( "f" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Forward { get; set; }

	/// <inheritdoc cref="Forward"/>
	[JsonPropertyName( "l" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Left { get; set; }

	/// <inheritdoc cref="Forward"/>
	[JsonPropertyName( "u" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Up { get; set; }

	/// <summary>How it is turned, in degrees.</summary>
	[JsonPropertyName( "p" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Pitch { get; set; }

	/// <inheritdoc cref="Pitch"/>
	[JsonPropertyName( "y" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Yaw { get; set; }

	/// <inheritdoc cref="Pitch"/>
	[JsonPropertyName( "r" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Roll { get; set; }

	/// <summary>Its size: 1 is the size it was made.</summary>
	[JsonPropertyName( "s" )]
	public float Scale { get; set; } = 1f;

	/// <summary>
	/// How far it is nudged on the first-person weapon from where it is worked out to be there: forwards, to the left and
	/// up (at most <see cref="MaxNudge"/>), and turned (at most <see cref="MaxNudgeTurn"/>).
	/// </summary>
	[JsonPropertyName( "nf" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float NudgeForward { get; set; }

	/// <inheritdoc cref="NudgeForward"/>
	[JsonPropertyName( "nl" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float NudgeLeft { get; set; }

	/// <inheritdoc cref="NudgeForward"/>
	[JsonPropertyName( "nu" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float NudgeUp { get; set; }

	/// <inheritdoc cref="NudgeForward"/>
	[JsonPropertyName( "np" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float NudgePitch { get; set; }

	/// <inheritdoc cref="NudgeForward"/>
	[JsonPropertyName( "ny" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float NudgeYaw { get; set; }

	/// <inheritdoc cref="NudgeForward"/>
	[JsonPropertyName( "nr" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float NudgeRoll { get; set; }

	/// <summary>Whether <paramref name="path"/> could name a model at all: a path ending in .vmdl, not too long, on one line.</summary>
	public static bool IsModelPath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) || path.Length > MaxModelLength || !path.EndsWith( ModelExtension, StringComparison.OrdinalIgnoreCase ) )
			return false;

		foreach ( var character in path )
		{
			if ( char.IsControl( character ) )
				return false;
		}

		return !path.Contains( ".." );
	}

	/// <summary>Whether a model <paramref name="size"/> long (its longest side, as made) can be a prop: not degenerate, not absurd.</summary>
	public static bool IsUsableSize( float size ) => float.IsFinite( size ) && size >= DegenerateSize && size <= AbsurdSize;

	/// <summary>
	/// The largest scale a model <paramref name="size"/> long may have: its longest side at most <see cref="MaxLength"/>,
	/// or <paramref name="partSize"/> for one taking the place of a part that long, if that is larger.
	/// </summary>
	public static float MaxScaleFor( float size, float partSize = 0f ) => IsUsableSize( size )
		? Math.Clamp( MathF.Max( MaxLength, float.IsFinite( partSize ) ? partSize : 0f ) / size, MinScale, MostScale )
		: MinScale;

	/// <summary>The scale a model <paramref name="size"/> long starts at: as made, or smaller to fit <see cref="MaxLength"/>.</summary>
	public static float StartingScale( float size ) => Math.Min( 1f, MaxScaleFor( size ) );

	/// <summary>
	/// A copy fitted to the weapon: for a model <paramref name="size"/> long placed from <paramref name="middle"/> (its
	/// part's middle, or the weapon's origin), on a weapon of <paramref name="weapon"/> bounds; replacing a part
	/// <paramref name="partSize"/> long. Its scale keeps its longest side within <see cref="MaxLength"/> (or the part's
	/// length), and its middle stays inside the weapon's bounds grown by <see cref="Reach"/>, less half its length. Null
	/// for a model that can't be a prop (see <see cref="IsUsableSize"/>).
	/// </summary>
	public WeaponProp FittedTo( float size, WeaponBounds middle, WeaponBounds weapon, float partSize = 0f )
	{
		var copy = Sanitized();
		if ( copy is null || !IsUsableSize( size ) )
			return null;

		copy.Scale = Math.Clamp( copy.Scale, MinScale, MaxScaleFor( size, copy.Replaces ? partSize : 0f ) );
		var half = size * copy.Scale * 0.5f;
		copy.Forward = Inside( copy.Forward, middle.MinX * copy.Scale, weapon.MinX, weapon.MaxX, half );
		copy.Left = Inside( copy.Left, middle.MinY * copy.Scale, weapon.MinY, weapon.MaxY, half );
		copy.Up = Inside( copy.Up, middle.MinZ * copy.Scale, weapon.MinZ, weapon.MaxZ, half );
		return copy;
	}

	/// <summary>
	/// <paramref name="value"/> (a pivot along one axis, whose middle is <paramref name="middle"/> further) held so the
	/// middle stays between <paramref name="least"/> and <paramref name="most"/> grown by <see cref="Reach"/>, less
	/// <paramref name="half"/>; the middle of that room when there is none.
	/// </summary>
	internal static float Inside( float value, float middle, float least, float most, float half )
	{
		var low = least - Reach + half - middle;
		var high = most + Reach - half - middle;
		return low <= high ? Math.Clamp( value, low, high ) : (low + high) * 0.5f;
	}

	/// <summary>A copy within absolute bounds, or null without a model path: numbers finite and limited.</summary>
	public WeaponProp Sanitized()
	{
		var model = Model?.Trim();
		if ( !IsModelPath( model ) )
			return null;

		var part = Part?.Trim();
		return new WeaponProp
		{
			Model = model,
			Part = WeaponDesign.IsPartName( part ) ? part : null,
			Replaces = Replaces && WeaponDesign.IsPartName( part ),
			Forward = Limited( Forward, MostOffset ),
			Left = Limited( Left, MostOffset ),
			Up = Limited( Up, MostOffset ),
			Pitch = Limited( Pitch, Accessory.MaxTurn ),
			Yaw = Limited( Yaw, Accessory.MaxTurn ),
			Roll = Limited( Roll, Accessory.MaxTurn ),
			Scale = float.IsFinite( Scale ) ? Math.Clamp( Scale, MinScale, MostScale ) : 1f,
			NudgeForward = Limited( NudgeForward, MaxNudge ),
			NudgeLeft = Limited( NudgeLeft, MaxNudge ),
			NudgeUp = Limited( NudgeUp, MaxNudge ),
			NudgePitch = Limited( NudgePitch, MaxNudgeTurn ),
			NudgeYaw = Limited( NudgeYaw, MaxNudgeTurn ),
			NudgeRoll = Limited( NudgeRoll, MaxNudgeTurn ),
		};
	}

	internal static float Limited( float value, float most ) => float.IsFinite( value ) ? Math.Clamp( value, -most, most ) : 0f;
}

/// <summary>
/// A box along a weapon's own directions (forwards, left, up), as six numbers: a weapon's bounds, or one corner of
/// something on it (only <see cref="MinX"/>, <see cref="MinY"/> and <see cref="MinZ"/> used, as a point).
/// </summary>
public readonly record struct WeaponBounds( float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ )
{
	/// <summary>A point, as a box of no size.</summary>
	public static WeaponBounds Point( float x, float y, float z ) => new( x, y, z, x, y, z );
}
