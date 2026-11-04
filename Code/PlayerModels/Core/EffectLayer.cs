using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PlayerModels.Core;

/// <summary>What a layer of an effect is: sprites (flames, sparks, hearts), or a light.</summary>
public enum LayerKind { Sprite, Light }

/// <summary>
/// Where a layer's sprites come from, around its place: one point, a ball, a flat ring, or all around the body (an
/// upright ball as tall as the body, wherever the layer sits).
/// </summary>
public enum LayerShape { Point, Sphere, Ring, AroundBody }

/// <summary>
/// What a sprite looks like: the curated pictures every client draws for itself from lines (see <see cref="EffectShape"/>).
/// Saved by name, so pictures and shapes added later join without changing what is saved.
/// </summary>
public enum LayerPicture { Glow, Spark, Flame, Smoke, Sparkle, Heart, Snowflake, Droplet }

/// <summary>How a layer changes over time: not at all, brighter and dimmer, flickering, through every colour, or turning.</summary>
public enum LayerRhythm { Steady, Pulse, Flicker, Rainbow, Spin }

/// <summary>
/// How a layer's sprites are coloured: each fading from its colour to its end colour, or one by one in the order they
/// appear, by one of World at War's clan tag animations (see <see cref="ColorCycle"/>) over a palette.
/// </summary>
public enum LayerColorMode { Fixed, Rain, Cycl, Cyln }

/// <summary>
/// One layer of an <see cref="Effect"/>: sprites coming from a shape on the body, or a light there. Sizes and distances
/// are for the citizen; a body of another size gets them in proportion. Saved under short names, without what is at its
/// default, so a look full of effects stays small. Kinds, shapes and pictures are saved by name: a layer of a kind this
/// version doesn't know (added later) is dropped when it is read, never the look.
/// </summary>
public sealed class EffectLayer
{
	/// <summary>How far a layer's shape reaches (its radius), and how far it can sit from its joint, along each axis.</summary>
	public const float MaxSpread = 48f;
	public const float MaxOffset = 24f;

	/// <summary>The size of a sprite, across.</summary>
	public const float MinSize = 0.1f;
	public const float MaxSize = 48f;

	/// <summary>How far a light reaches.</summary>
	public const float MinReach = 8f;
	public const float MaxReach = 400f;

	/// <summary>How fast sprites leave their shape, and how hard they fall (rise when negative), in units a second.</summary>
	public const float MaxSpeed = 200f;
	public const float MaxGravity = 400f;

	/// <summary>How long a sprite lives, in seconds.</summary>
	public const float MinLifetime = 0.1f;
	public const float MaxLifetime = 5f;

	/// <summary>How many sprites a second, and how bright a light is.</summary>
	public const float MaxRate = 100f;
	public const float MaxBrightness = 4f;

	/// <summary>How many times a second a rhythm beats (or turns, for <see cref="LayerRhythm.Spin"/>).</summary>
	public const float MaxRhythmSpeed = 10f;

	/// <summary>The most colours a palette of the player's own has.</summary>
	public const int MaxPalette = 32;

	private const string White = "#ffffff";

	/// <summary>What the layer is, a <see cref="LayerKind"/> by name.</summary>
	[JsonPropertyName( "k" )]
	public string Kind { get; set; } = nameof( LayerKind.Sprite );

	/// <summary>The <see cref="Effect.Joint"/> it comes from, by name, in place of its effect's; null for its effect's.</summary>
	[JsonPropertyName( "j" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Joint { get; set; }

	/// <summary>Its <see cref="LayerShape"/> by name; null for a point.</summary>
	[JsonPropertyName( "s" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Shape { get; set; }

	/// <summary>How far the shape reaches from the layer's place.</summary>
	[JsonPropertyName( "r" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Spread { get; set; }

	/// <summary>How far it sits from its joint: forwards, to the body's left, and up.</summary>
	[JsonPropertyName( "f" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Forward { get; set; }

	/// <inheritdoc cref="Forward"/>
	[JsonPropertyName( "l" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Left { get; set; }

	/// <inheritdoc cref="Forward"/>
	[JsonPropertyName( "u" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Up { get; set; }

	/// <summary>Its sprites' <see cref="LayerPicture"/> by name; null for a glow.</summary>
	[JsonPropertyName( "p" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Picture { get; set; }

	/// <summary>Its colour as <c>#rrggbb</c>: a sprite's when it appears, or the light's.</summary>
	[JsonPropertyName( "c" )]
	public string Color { get; set; } = White;

	/// <summary>A sprite's colour as it fades away, as <c>#rrggbb</c>; null to keep <see cref="Color"/>.</summary>
	[JsonPropertyName( "c2" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string ColorEnd { get; set; }

	/// <summary>Its <see cref="LayerColorMode"/> by name; null for fixed colours.</summary>
	[JsonPropertyName( "cm" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string ColorMode { get; set; }

	/// <summary>
	/// The colours a colour cycle goes through, as <c>#rrggbb</c>, any number of them; null for the game's own (see
	/// <see cref="CycleColours"/>).
	/// </summary>
	[JsonPropertyName( "pal" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<string> Palette { get; set; }

	/// <summary>A sprite's size when it appears, or how far the light reaches.</summary>
	[JsonPropertyName( "a" )]
	public float Size { get; set; } = 2f;

	/// <summary>A sprite's size as it fades away; null to keep <see cref="Size"/>.</summary>
	[JsonPropertyName( "b" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public float? SizeEnd { get; set; }

	/// <summary>How fast sprites leave the shape, outwards from its middle.</summary>
	[JsonPropertyName( "v" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Speed { get; set; }

	/// <summary>How hard sprites fall; negative rises.</summary>
	[JsonPropertyName( "g" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Gravity { get; set; }

	/// <summary>How long a sprite lives, in seconds.</summary>
	[JsonPropertyName( "t" )]
	public float Lifetime { get; set; } = 1f;

	/// <summary>How many sprites appear a second, or how bright the light is.</summary>
	[JsonPropertyName( "n" )]
	public float Amount { get; set; } = 10f;

	/// <summary>Whether sprites move with the body once out; otherwise they stay where they appeared, trailing behind.</summary>
	[JsonPropertyName( "fo" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool Follow { get; set; }

	/// <summary>Whether sprites cover what is behind them (smoke, hearts) instead of glowing over it.</summary>
	[JsonPropertyName( "so" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool Solid { get; set; }

	/// <summary>Its <see cref="LayerRhythm"/> by name; null for steady.</summary>
	[JsonPropertyName( "rh" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Rhythm { get; set; }

	/// <summary>How many times a second the rhythm beats.</summary>
	[JsonPropertyName( "rs" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float RhythmSpeed { get; set; }

	/// <summary>How much the rhythm changes it, 0 to 1.</summary>
	[JsonPropertyName( "ra" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float RhythmAmount { get; set; }

	/// <summary>What the layer is; only right on a layer within bounds (see <see cref="Sanitized"/>).</summary>
	[JsonIgnore]
	public LayerKind KindIs => Parsed( Kind, LayerKind.Sprite );

	/// <summary>Where its sprites come from.</summary>
	[JsonIgnore]
	public LayerShape ShapeIs => Parsed( Shape, LayerShape.Point );

	/// <summary>What its sprites look like.</summary>
	[JsonIgnore]
	public LayerPicture PictureIs => Parsed( Picture, LayerPicture.Glow );

	/// <summary>How it changes over time.</summary>
	[JsonIgnore]
	public LayerRhythm RhythmIs => Parsed( Rhythm, LayerRhythm.Steady );

	/// <summary>How its sprites are coloured.</summary>
	[JsonIgnore]
	public LayerColorMode ColorModeIs => Parsed( ColorMode, LayerColorMode.Fixed );

	/// <summary>
	/// The colours its colour cycle goes through: its own palette, or the game's five, and for
	/// <see cref="LayerColorMode.Cyln"/> the highlight then the rest (the game's red and white unless its palette has two).
	/// </summary>
	public IReadOnlyList<string> CycleColours()
	{
		if ( ColorModeIs == LayerColorMode.Cyln )
			return Palette is { Count: >= 2 } ? Palette : new[] { ColorCycle.ScannerHighlight, ColorCycle.ScannerRest };

		return Palette is { Count: > 0 } ? Palette : ColorCycle.GamePalette;
	}

	/// <summary>How many of its sprites are out at once, at <paramref name="amount"/> times its own amount; a light has none.</summary>
	public float LiveSprites( float amount = 1f ) => KindIs == LayerKind.Sprite ? Amount * amount * Lifetime : 0f;

	/// <summary>
	/// A copy within bounds, or null for a layer of a kind (or from a joint, shape, picture, rhythm or colour mode) this
	/// version doesn't know: numbers finite and limited, colours valid hex colours (a palette keeps only those, at most
	/// <see cref="MaxPalette"/>), and what is at its default left out.
	/// </summary>
	public EffectLayer Sanitized()
	{
		if ( !Known<LayerKind>( Kind ) || Joint is not null && !Known<Effect.Joint>( Joint ) || Shape is not null && !Known<LayerShape>( Shape )
			|| Picture is not null && !Known<LayerPicture>( Picture ) || Rhythm is not null && !Known<LayerRhythm>( Rhythm )
			|| ColorMode is not null && !Known<LayerColorMode>( ColorMode ) )
			return null;

		var palette = new List<string>();
		foreach ( var colourInPalette in Palette ?? new() )
		{
			if ( palette.Count < MaxPalette && PlayerLook.IsColour( colourInPalette ) )
				palette.Add( colourInPalette.ToLowerInvariant() );
		}

		var isLight = Kind == nameof( LayerKind.Light );
		var colour = PlayerLook.IsColour( Color ) ? Color.ToLowerInvariant() : White;
		var colourEnd = PlayerLook.IsColour( ColorEnd ) ? ColorEnd.ToLowerInvariant() : null;
		var size = isLight ? Limited( Size, MinReach, MaxReach, MinReach ) : Limited( Size, MinSize, MaxSize, MinSize );
		var sizeEnd = SizeEnd is { } end && !isLight ? Limited( end, MinSize, MaxSize, size ) : (float?)null;

		return new EffectLayer
		{
			Kind = Kind,
			Joint = Joint,
			Shape = Shape == nameof( LayerShape.Point ) ? null : Shape,
			Spread = Limited( Spread, 0f, MaxSpread, 0f ),
			Forward = Limited( Forward, -MaxOffset, MaxOffset, 0f ),
			Left = Limited( Left, -MaxOffset, MaxOffset, 0f ),
			Up = Limited( Up, -MaxOffset, MaxOffset, 0f ),
			Picture = Picture == nameof( LayerPicture.Glow ) ? null : Picture,
			Color = colour,
			ColorEnd = colourEnd == colour ? null : colourEnd,
			ColorMode = ColorMode == nameof( LayerColorMode.Fixed ) ? null : ColorMode,
			Palette = palette.Count > 0 ? palette : null,
			Size = size,
			SizeEnd = sizeEnd == size ? null : sizeEnd,
			Speed = Limited( Speed, 0f, MaxSpeed, 0f ),
			Gravity = Limited( Gravity, -MaxGravity, MaxGravity, 0f ),
			Lifetime = Limited( Lifetime, MinLifetime, MaxLifetime, 1f ),
			Amount = isLight ? Limited( Amount, 0f, MaxBrightness, 1f ) : Limited( Amount, 0f, MaxRate, 0f ),
			Follow = Follow,
			Solid = Solid,
			Rhythm = Rhythm == nameof( LayerRhythm.Steady ) ? null : Rhythm,
			RhythmSpeed = Limited( RhythmSpeed, 0f, MaxRhythmSpeed, 0f ),
			RhythmAmount = Limited( RhythmAmount, 0f, 1f, 0f ),
		};
	}

	private static bool Known<TEnum>( string name ) where TEnum : struct, Enum
	{
		return name is not null && Enum.IsDefined( typeof( TEnum ), name );
	}

	private static TEnum Parsed<TEnum>( string name, TEnum otherwise ) where TEnum : struct, Enum
	{
		return name is not null && Enum.TryParse<TEnum>( name, false, out var value ) && Enum.IsDefined( typeof( TEnum ), value ) ? value : otherwise;
	}

	private static float Limited( float value, float least, float most, float otherwise )
	{
		return float.IsFinite( value ) ? Math.Clamp( value, least, most ) : otherwise;
	}
}
