using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlayerModels.Core;

/// <summary>
/// How a sticker colours its shape (see <see cref="DrawnShape"/>): filled in one colour with an outline of another
/// round it, and, if it is animated, its fill going round the colour wheel, through a palette the way World at War's clan
/// tags did (see <see cref="ColorCycle"/>), or pulsing. A shape without one is plain white, to take the colour of the
/// layer it is on.
/// </summary>
public sealed class Sticker
{
	/// <summary>How an animated sticker's fill changes over time.</summary>
	public enum Motion { None, Rainbow, Rain, Cycl, Cyln, Pulse }

	/// <summary>How wide the outline is at most, on the shape's square (2 across).</summary>
	public const float MaxOutline = 0.2f;

	/// <summary>How fast an animation runs at most: turns, beats or palette steps a second, relative to the game's.</summary>
	public const float MaxSpeed = 10f;

	private const string White = "#ffffff";
	private const string Black = "#000000";

	/// <summary>The fill's colour, as <c>#rrggbb</c>; an animated fill takes its colours from its animation.</summary>
	[JsonPropertyName( "f" )]
	public string Fill { get; set; } = White;

	/// <summary>The outline's colour, as <c>#rrggbb</c>.</summary>
	[JsonPropertyName( "o" )]
	public string Outline { get; set; } = Black;

	/// <summary>How wide the outline is, on the shape's square; 0 for none.</summary>
	[JsonPropertyName( "w" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float OutlineWidth { get; set; }

	/// <summary>Its <see cref="Motion"/> by name; null for none.</summary>
	[JsonPropertyName( "an" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Animation { get; set; }

	/// <summary>The colours a palette animation goes through, as <c>#rrggbb</c>; null for the game's.</summary>
	[JsonPropertyName( "pal" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<string> Palette { get; set; }

	/// <summary>How fast the animation runs: 1 is the game's own pace.</summary>
	[JsonPropertyName( "sp" )]
	public float Speed { get; set; } = 1f;

	/// <summary>How its fill changes; none on a sticker not within bounds.</summary>
	[JsonIgnore]
	public Motion AnimationIs => Animation is not null && Enum.TryParse<Motion>( Animation, false, out var motion ) && Enum.IsDefined( typeof( Motion ), motion )
		? motion
		: Motion.None;

	/// <summary>Whether its fill changes over time, so it is drawn white for the animation to colour.</summary>
	[JsonIgnore]
	public bool IsAnimated => AnimationIs != Motion.None;

	/// <summary>
	/// The colours its palette animation goes through: its own, or the game's five, and for <see cref="Motion.Cyln"/>
	/// the highlight then the rest.
	/// </summary>
	public IReadOnlyList<string> CycleColours()
	{
		if ( AnimationIs == Motion.Cyln )
			return Palette is { Count: >= 2 } ? Palette : new[] { ColorCycle.ScannerHighlight, ColorCycle.ScannerRest };

		return Palette is { Count: > 0 } ? Palette : ColorCycle.GamePalette;
	}

	// A rainbow sticker turns round the wheel this often a second, a pulsing one beats this often and this deep, at speed 1
	internal const float RainbowTurns = 0.5f;
	internal const float PulseBeats = 1f;
	internal const float PulseDepth = 0.5f;

	/// <summary>
	/// <paramref name="layer"/> as it shows with this sticker as its picture: its fill's animation as the layer's own
	/// colour cycle or rhythm, unless the layer has one of its own, which wins. A copy when anything changes.
	/// </summary>
	public EffectLayer Drive( EffectLayer layer )
	{
		var motion = AnimationIs;
		if ( motion == Motion.None || layer.ColorModeIs != LayerColorMode.Fixed || layer.RhythmIs != LayerRhythm.Steady )
			return layer;

		var driven = JsonSerializer.Deserialize<EffectLayer>( JsonSerializer.Serialize( layer ) );
		switch ( motion )
		{
			case Motion.Rainbow:
				driven.Rhythm = nameof( LayerRhythm.Rainbow );
				driven.RhythmSpeed = RainbowTurns * Speed;
				break;

			case Motion.Pulse:
				driven.Rhythm = nameof( LayerRhythm.Pulse );
				driven.RhythmSpeed = PulseBeats * Speed;
				driven.RhythmAmount = PulseDepth;
				break;

			default:
				driven.ColorMode = motion.ToString();
				driven.Palette = Palette is null ? null : new List<string>( Palette );
				break;
		}

		return driven;
	}

	/// <summary>
	/// A copy within bounds: colours valid hex colours (a palette keeps only those, at most
	/// <see cref="EffectLayer.MaxPalette"/>), the outline and speed limited, and an animation this version doesn't know
	/// none.
	/// </summary>
	public Sticker Sanitized()
	{
		var palette = new List<string>();
		foreach ( var colour in Palette ?? new() )
		{
			if ( palette.Count < EffectLayer.MaxPalette && PlayerLook.IsColour( colour ) )
				palette.Add( colour.ToLowerInvariant() );
		}

		var motion = AnimationIs;
		return new Sticker
		{
			Fill = PlayerLook.IsColour( Fill ) ? Fill.ToLowerInvariant() : White,
			Outline = PlayerLook.IsColour( Outline ) ? Outline.ToLowerInvariant() : Black,
			OutlineWidth = float.IsFinite( OutlineWidth ) ? Math.Clamp( OutlineWidth, 0f, MaxOutline ) : 0f,
			Animation = motion == Motion.None ? null : motion.ToString(),
			Palette = palette.Count > 0 && motion is Motion.Rain or Motion.Cycl or Motion.Cyln ? palette : null,
			Speed = float.IsFinite( Speed ) ? Math.Clamp( Speed, 0f, MaxSpeed ) : 1f,
		};
	}
}
