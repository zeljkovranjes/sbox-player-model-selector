using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PlayerModels.Core;

/// <summary>
/// A picture stuck on a weapon, pressed onto its surface from one side so it wraps over the shape beneath (a decal, not
/// a card standing off it): one of the effects' pictures, a standard shape, or a shape or sticker the player drew (see
/// <see cref="DrawnShape"/>). Coloured in one colour or a moving one, like a sticker's fill (see
/// <see cref="Sticker.Motion"/>); a drawn sticker keeps its own colours. Where it sits is held to the weapon's bounds
/// grown by <see cref="WeaponProp.Reach"/> on every client (see <see cref="FittedTo"/>).
/// </summary>
public sealed class WeaponSticker
{
	/// <summary>How wide a sticker is, at least and at most, in units.</summary>
	public const float MinSize = 0.5f;
	public const float MaxSize = 12f;

	private const string White = "#ffffff";

	/// <summary>
	/// Its picture: a <see cref="LayerPicture"/> or <see cref="StandardShape"/> by name, or a drawn shape the look carries
	/// (see <see cref="DrawnShape.Reference"/>).
	/// </summary>
	[JsonPropertyName( "pic" )]
	public string Picture { get; set; } = nameof( StandardShape.Star5 );

	/// <summary>Its colour, as <c>#rrggbb</c>; a moving one takes its colours from its motion.</summary>
	[JsonPropertyName( "c" )]
	public string Color { get; set; } = White;

	/// <summary>Its <see cref="Sticker.Motion"/> by name; null for none.</summary>
	[JsonPropertyName( "an" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Animation { get; set; }

	/// <summary>The colours its motion goes through, as <c>#rrggbb</c>; null for the game's.</summary>
	[JsonPropertyName( "pal" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<string> Palette { get; set; }

	/// <summary>How fast its colour moves: 1 is the game's own pace.</summary>
	[JsonPropertyName( "sp" )]
	public float Speed { get; set; } = 1f;

	/// <summary>Where its middle is on the weapon: forwards, to the left and up, from the weapon's own origin.</summary>
	[JsonPropertyName( "f" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Forward { get; set; }

	/// <inheritdoc cref="Forward"/>
	[JsonPropertyName( "l" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Left { get; set; }

	/// <inheritdoc cref="Forward"/>
	[JsonPropertyName( "u" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Up { get; set; }

	/// <summary>Which way it is pressed on, as a turn from pressing down from above, in degrees.</summary>
	[JsonPropertyName( "p" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Pitch { get; set; }

	/// <inheritdoc cref="Pitch"/>
	[JsonPropertyName( "y" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Yaw { get; set; }

	/// <inheritdoc cref="Pitch"/>
	[JsonPropertyName( "r" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Roll { get; set; }

	/// <summary>How wide it is, in units.</summary>
	[JsonPropertyName( "s" )]
	public float Size { get; set; } = 3f;

	/// <summary>How its colour moves; none on a sticker not within bounds.</summary>
	[JsonIgnore]
	public Sticker.Motion AnimationIs => Animation is not null && Enum.TryParse<Sticker.Motion>( Animation, false, out var motion ) && Enum.IsDefined( typeof( Sticker.Motion ), motion )
		? motion
		: Sticker.Motion.None;

	/// <summary>The id of the drawn shape it is, or null for a picture or standard shape.</summary>
	[JsonIgnore]
	public string DrawnPicture => DrawnShape.IsReference( Picture ) ? Picture[ 1.. ] : null;

	/// <summary>
	/// The colours its motion goes through: its own palette or the game's five, and for <see cref="Sticker.Motion.Cyln"/>
	/// the highlight then the rest.
	/// </summary>
	public IReadOnlyList<string> CycleColours() => new Sticker { Animation = Animation, Palette = Palette }.CycleColours();

	/// <summary>
	/// A copy fitted to a weapon of <paramref name="weapon"/> bounds: its middle inside them grown by
	/// <see cref="WeaponProp.Reach"/>.
	/// </summary>
	public WeaponSticker FittedTo( WeaponBounds weapon )
	{
		return new WeaponSticker
		{
			Picture = Picture, Color = Color, Animation = Animation, Palette = Palette, Speed = Speed, Pitch = Pitch, Yaw = Yaw, Roll = Roll, Size = Size,
			Forward = WeaponProp.Inside( Forward, 0f, weapon.MinX, weapon.MaxX, 0f ),
			Left = WeaponProp.Inside( Left, 0f, weapon.MinY, weapon.MaxY, 0f ),
			Up = WeaponProp.Inside( Up, 0f, weapon.MinZ, weapon.MaxZ, 0f ),
		};
	}

	/// <summary>
	/// A copy within bounds, or null for a picture this version doesn't know (or a drawn shape <paramref name="drawn"/>
	/// says the look doesn't carry): colours valid hex colours (a palette keeps only those, at most
	/// <see cref="EffectLayer.MaxPalette"/>), numbers limited, and a motion this version doesn't know none.
	/// </summary>
	public WeaponSticker Sanitized( Func<string, string> drawn = null )
	{
		var picture = DrawnShape.IsReference( Picture ) ? drawn?.Invoke( Picture ) : Picture;
		if ( picture is null || !DrawnShape.IsReference( picture ) && !Enum.IsDefined( typeof( LayerPicture ), picture )
			&& !Enum.IsDefined( typeof( StandardShape ), picture ) )
			return null;

		var motion = AnimationIs;
		var palette = new List<string>();
		foreach ( var colour in Palette ?? new() )
		{
			if ( palette.Count < EffectLayer.MaxPalette && PlayerLook.IsColour( colour ) )
				palette.Add( colour.ToLowerInvariant() );
		}

		return new WeaponSticker
		{
			Picture = picture,
			Color = PlayerLook.IsColour( Color ) ? Color.ToLowerInvariant() : White,
			Animation = motion == Sticker.Motion.None ? null : motion.ToString(),
			Palette = palette.Count > 0 && motion is Sticker.Motion.Rain or Sticker.Motion.Cycl or Sticker.Motion.Cyln ? palette : null,
			Speed = float.IsFinite( Speed ) ? Math.Clamp( Speed, 0f, Sticker.MaxSpeed ) : 1f,
			Forward = WeaponProp.Limited( Forward, WeaponProp.MostOffset ),
			Left = WeaponProp.Limited( Left, WeaponProp.MostOffset ),
			Up = WeaponProp.Limited( Up, WeaponProp.MostOffset ),
			Pitch = WeaponProp.Limited( Pitch, Accessory.MaxTurn ),
			Yaw = WeaponProp.Limited( Yaw, Accessory.MaxTurn ),
			Roll = WeaponProp.Limited( Roll, Accessory.MaxTurn ),
			Size = float.IsFinite( Size ) ? Math.Clamp( Size, MinSize, MaxSize ) : MinSize,
		};
	}
}
