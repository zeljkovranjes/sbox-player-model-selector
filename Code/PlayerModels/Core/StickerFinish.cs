using System;
using System.Text.Json;

namespace PlayerModels.Core;

/// <summary>
/// What a sticker is printed on, as CS:GO's stickers are: plain paper, a holographic film whose rainbow shifts as it is
/// turned, shiny metal foil, glitter that twinkles, gold foil, or a lenticular print showing one of two looks by the angle
/// it is seen from (its layers marked for each, see <see cref="EmblemLayer.Side"/>). Saved by name; a sticker without one
/// is paper.
/// </summary>
public enum StickerFinish { Paper, Holo, Foil, Glitter, Gold, Lenticular }

/// <summary>Reading a <see cref="StickerFinish"/> saved by name.</summary>
public static class StickerFinishes
{
	/// <summary>The finish named <paramref name="name"/>; paper for none or one this version doesn't know.</summary>
	public static StickerFinish Of( string name ) => name is not null && Enum.TryParse<StickerFinish>( name, false, out var finish ) && Enum.IsDefined( finish )
		? finish
		: StickerFinish.Paper;

	/// <summary>The name a finish is saved by: null for paper, so a plain sticker saves as it did before finishes.</summary>
	public static string NameOf( StickerFinish finish ) => finish == StickerFinish.Paper ? null : finish.ToString();

	/// <summary><paramref name="name"/> kept within bounds: a finish this version knows, or null for paper.</summary>
	public static string Sanitized( string name ) => NameOf( Of( name ) );

	/// <summary>
	/// <paramref name="layer"/> as it shows with a sticker of <paramref name="finish"/> as its picture: sprites face the
	/// camera, so a finish is shown by how it changes over time (a holo's rainbow going round, foil and gold catching the
	/// light, glitter twinkling) unless the layer has a rhythm or colour cycle of its own, which wins. A copy when anything
	/// changes; a lenticular sticker's sprites take its two looks, swapping as the camera goes round and as time passes (see
	/// the engine's EffectPictures).
	/// </summary>
	public static EffectLayer Drive( EffectLayer layer, StickerFinish finish )
	{
		if ( finish is StickerFinish.Paper or StickerFinish.Lenticular || layer.ColorModeIs != LayerColorMode.Fixed || layer.RhythmIs != LayerRhythm.Steady )
			return layer;

		var driven = JsonSerializer.Deserialize<EffectLayer>( JsonSerializer.Serialize( layer ) );
		(driven.Rhythm, driven.RhythmSpeed, driven.RhythmAmount) = finish switch
		{
			StickerFinish.Holo => (nameof( LayerRhythm.Rainbow ), HoloTurns, 0f),
			StickerFinish.Glitter => (nameof( LayerRhythm.Flicker ), GlitterBeats, GlitterDepth),
			_ => (nameof( LayerRhythm.Pulse ), ShineBeats, ShineDepth),
		};

		if ( finish == StickerFinish.Gold )
			driven.Color = GoldTint;

		return driven;
	}

	// How a finish changes on sprites: a holo's rainbow turns this often a second, foil and gold shine this often and
	// deep, glitter twinkles this often and deep; gold tints the print
	private const float HoloTurns = 0.3f;
	private const float ShineBeats = 1.5f;
	private const float ShineDepth = 0.25f;
	private const float GlitterBeats = 6f;
	private const float GlitterDepth = 0.35f;
	private const string GoldTint = "#ffd27a";
}
