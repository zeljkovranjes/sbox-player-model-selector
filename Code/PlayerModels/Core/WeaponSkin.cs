using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PlayerModels.Core;

/// <summary>
/// How a weapon's whole surface is painted over its finish and tint: a pattern laid over it from every side (stripes,
/// camo, checker, a gradient, carbon fibre or a shape repeated), which can scroll or spin, and a colour that moves over
/// it: a rainbow flowing along it, World at War's clan tag colourings across it in bands (see <see cref="ColorCycle"/>),
/// a pulse, a glow, a sheen sweeping over it or a palette going round. Worked out on the graphics card from these few
/// numbers, so nothing moves on the main thread and every client shows the same. Saved under short names, without what
/// is at its default.
/// </summary>
public sealed class WeaponSkin
{
	/// <summary>What is laid over the weapon.</summary>
	public enum Pattern { None, Stripes, Camo, Checker, Gradient, Carbon, Shapes }

	/// <summary>How its colour moves.</summary>
	public enum Motion { None, Rainbow, Rain, Cycl, Cyln, Pulse, Glow, Shimmer, Palette }

	/// <summary>How small or large a pattern goes: a quarter of its size to four times.</summary>
	public const float MinScale = 0.25f;
	public const float MaxScale = 4f;

	/// <summary>How fast a pattern scrolls (its size a second) and spins (turns a second) at most, either way.</summary>
	public const float MaxScroll = 4f;
	public const float MaxSpin = 2f;

	/// <summary>How fast the colour moves at most: 1 is the clan tags' own pace.</summary>
	public const float MaxSpeed = 10f;

	/// <summary>How many bands the colour moves along the weapon in, at least and at most.</summary>
	public const int MinBands = 2;
	public const int MaxBands = 32;

	/// <summary>The most colours a skin's palette has: what the graphics card is given.</summary>
	public const int MaxPalette = 8;

	private const string White = "#ffffff";
	private const string Black = "#000000";
	private const int DefaultBands = 8;

	/// <summary>Its <see cref="Pattern"/> by name; null for none.</summary>
	[JsonPropertyName( "pt" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string PatternName { get; set; }

	/// <summary>The pattern's two colours, as <c>#rrggbb</c>.</summary>
	[JsonPropertyName( "pa" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string PatternColor { get; set; }

	/// <inheritdoc cref="PatternColor"/>
	[JsonPropertyName( "pb" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string PatternColorEnd { get; set; }

	/// <summary>How large the pattern is: 1 as made.</summary>
	[JsonPropertyName( "ps" )]
	public float PatternScale { get; set; } = 1f;

	/// <summary>How far the pattern is turned, in degrees.</summary>
	[JsonPropertyName( "pr" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float PatternTurn { get; set; }

	/// <summary>How fast the pattern scrolls along the weapon, its size a second; negative the other way.</summary>
	[JsonPropertyName( "pv" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float PatternScroll { get; set; }

	/// <summary>How fast the pattern spins, turns a second; negative the other way.</summary>
	[JsonPropertyName( "pw" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float PatternSpin { get; set; }

	/// <summary>
	/// The shape a <see cref="Pattern.Shapes"/> pattern repeats: a <see cref="StandardShape"/> or <see cref="LayerPicture"/>
	/// by name, or a drawn shape the look carries (see <see cref="DrawnShape.Reference"/>); null for a star.
	/// </summary>
	[JsonPropertyName( "psh" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string PatternShape { get; set; }

	/// <summary>Its <see cref="Motion"/> by name; null for none.</summary>
	[JsonPropertyName( "an" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Animation { get; set; }

	/// <summary>
	/// The colours its motion goes through, as <c>#rrggbb</c>; null for the game's (see <see cref="Colours"/>).
	/// </summary>
	[JsonPropertyName( "pal" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<string> Palette { get; set; }

	/// <summary>
	/// The player's own script (a weapon skin script, see <see cref="PlayerModels.Core.Script.ScriptKind.Skin"/>) moving its
	/// colour, minified, in place of a stock <see cref="Motion"/> (which stays as what shows where scripts don't run); null
	/// for none. Held to <see cref="PlayerModels.Core.Script.ScriptLimits.MaxScriptBytes"/>, and with the look's other scripts
	/// to <see cref="PlayerModels.Core.Script.ScriptLimits.MaxLookBytes"/>.
	/// </summary>
	[JsonPropertyName( "sc" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Script { get; set; }

	/// <summary>How fast its colour moves: 1 is the clan tags' own pace.</summary>
	[JsonPropertyName( "sp" )]
	public float Speed { get; set; } = 1f;

	/// <summary>How many bands its colour moves along the weapon in.</summary>
	[JsonPropertyName( "bd" )]
	public int Bands { get; set; } = DefaultBands;

	/// <summary>What is laid over the weapon; only right on a skin within bounds (see <see cref="Sanitized"/>).</summary>
	[JsonIgnore]
	public Pattern PatternIs => Parsed( PatternName, Pattern.None );

	/// <summary>How its colour moves.</summary>
	[JsonIgnore]
	public Motion AnimationIs => Parsed( Animation, Motion.None );

	/// <summary>Whether it paints anything: a pattern or a moving colour.</summary>
	[JsonIgnore]
	public bool IsPainted => PatternIs != Pattern.None || AnimationIs != Motion.None || Script is not null;

	/// <summary>
	/// The colours its motion goes through: its own palette, or the game's five; for <see cref="Motion.Cyln"/> the
	/// highlight then the rest, and for a pulse, glow or sheen the first one alone (white unless its palette says).
	/// </summary>
	public IReadOnlyList<string> Colours()
	{
		var motion = AnimationIs;
		if ( motion == Motion.Cyln )
			return Palette is { Count: >= 2 } ? Palette : new[] { ColorCycle.ScannerHighlight, ColorCycle.ScannerRest };

		if ( motion is Motion.Pulse or Motion.Glow or Motion.Shimmer )
			return Palette is { Count: > 0 } ? new[] { Palette[ 0 ] } : new[] { White };

		return Palette is { Count: > 0 } ? Palette : ColorCycle.GamePalette;
	}

	/// <summary>
	/// A copy within bounds, or null when it paints nothing: a pattern or motion this version doesn't know is none, colours
	/// valid hex colours (a palette keeps only those, at most <see cref="MaxPalette"/>), numbers limited, a script too long
	/// dropped (by its length alone, never read), and a drawn shape named only when <paramref name="drawn"/> says the look
	/// carries it (a star otherwise).
	/// </summary>
	public WeaponSkin Sanitized( Func<string, string> drawn = null )
	{
		var pattern = PatternIs;
		var motion = AnimationIs;
		var script = PlayerModels.Core.Script.ScriptLimits.Fits( Script ) ? Script : null;
		if ( pattern == Pattern.None && motion == Motion.None && script is null )
			return null;

		var palette = new List<string>();
		foreach ( var colour in Palette ?? new() )
		{
			if ( palette.Count < MaxPalette && PlayerLook.IsColour( colour ) )
				palette.Add( colour.ToLowerInvariant() );
		}

		var shape = pattern == Pattern.Shapes ? ShapeNamed( PatternShape, drawn ) : null;
		return new WeaponSkin
		{
			PatternName = pattern == Pattern.None ? null : pattern.ToString(),
			PatternColor = pattern == Pattern.None ? null : Colour( PatternColor, Black ),
			PatternColorEnd = pattern == Pattern.None ? null : Colour( PatternColorEnd, White ),
			PatternScale = Limited( PatternScale, MinScale, MaxScale, 1f ),
			PatternTurn = Limited( PatternTurn, 0f, 180f, 0f ),
			PatternScroll = Limited( PatternScroll, -MaxScroll, MaxScroll, 0f ),
			PatternSpin = Limited( PatternSpin, -MaxSpin, MaxSpin, 0f ),
			PatternShape = shape,
			Animation = motion == Motion.None ? null : motion.ToString(),
			Palette = (motion != Motion.None || script is not null) && palette.Count > 0 ? palette : null,
			Script = script,
			Speed = Limited( Speed, 0f, MaxSpeed, 1f ),
			Bands = Math.Clamp( Bands, MinBands, MaxBands ),
		};
	}

	// A standard shape or picture by its name, a drawn shape the look carries, or else a star
	private static string ShapeNamed( string name, Func<string, string> drawn )
	{
		if ( DrawnShape.IsReference( name ) )
			return drawn?.Invoke( name ) ?? nameof( StandardShape.Star5 );

		return name is not null && (Enum.IsDefined( typeof( StandardShape ), name ) || Enum.IsDefined( typeof( LayerPicture ), name ))
			? name
			: nameof( StandardShape.Star5 );
	}

	private static string Colour( string hex, string otherwise ) => PlayerLook.IsColour( hex ) ? hex.ToLowerInvariant() : otherwise;

	private static TEnum Parsed<TEnum>( string name, TEnum otherwise ) where TEnum : struct, Enum
	{
		return name is not null && Enum.TryParse<TEnum>( name, false, out var value ) && Enum.IsDefined( typeof( TEnum ), value ) ? value : otherwise;
	}

	private static float Limited( float value, float least, float most, float otherwise )
	{
		return float.IsFinite( value ) ? Math.Clamp( value, least, most ) : otherwise;
	}
}
