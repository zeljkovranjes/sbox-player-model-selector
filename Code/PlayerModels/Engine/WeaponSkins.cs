using System;
using System.Collections.Generic;
using System.Text.Json;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The materials a weapon's skin (see <see cref="WeaponSkin"/>) is drawn with: each of the weapon's own materials made
/// again on the selector's skin shader, keeping its colour and bumps, with the finish, tint, pattern and moving colours
/// as its settings, and the design's stickers pressed onto it (see <see cref="Stuck"/>). The shader moves the colours
/// itself, from the time of the frame, so nothing is set per frame but a moving sticker's colour. Made once for each
/// material, skin and set of stickers and shared by everyone showing the same: a hundred players with the same skin on
/// the same weapon draw with one set of materials.
/// </summary>
internal static class WeaponSkins
{
	/// <summary>The skin shader: a weapon's own surface, finished, tinted and painted (Assets/shaders/weapon_skin.shader).</summary>
	internal const string Shader = "shaders/weapon_skin.shader";

	// The most materials kept: past it they are all let go of and made again as they are needed
	private const int MostKept = 256;

	// A pattern repeats this many units on the weapon at scale 1
	private const float PatternUnits = 4f;

	private const string ColorTexture = "g_tColor";
	private const string NormalTexture = "g_tNormal";
	private const string AlphaTestFeature = "F_ALPHA_TEST";

	private static readonly Dictionary<string, Material> Kept = new();
	private static int _made;

	/// <summary>The most stickers the skin draws, as many as a design has (see <see cref="WeaponDesign.MaxStickers"/>).</summary>
	internal const int MostStickers = 8;

	// The shader's names for each sticker's settings, made once so setting them allocates nothing
	private static readonly string[] RowX = Names( "g_vStickerX" ), RowY = Names( "g_vStickerY" ), RowZ = Names( "g_vStickerZ" ), Look = Names( "g_vStickerLook" ),
		TintOf = Names( "g_vStickerTint" ), Pictures = Names( "g_vStickerPictures" );

	private static string[] Names( string start )
	{
		var names = new string[ MostStickers ];
		for ( var i = 0; i < MostStickers; i++ )
			names[ i ] = start + i;

		return names;
	}

	/// <summary>
	/// A sticker as the skin draws it: the map from the model's own space to the box it is pressed in (rows
	/// <paramref name="X"/>, into it; <paramref name="Y"/>, across; <paramref name="Z"/>, up), how it looks
	/// (<paramref name="Look"/>: its <see cref="StickerFinish"/>, how scraped, where its wear lies, a die-cut border), its
	/// colour and its pictures (a lenticular one's second look in <paramref name="Second"/>).
	/// </summary>
	internal readonly record struct Stuck( Vector4 X, Vector4 Y, Vector4 Z, Vector4 Look, Color Tint, Texture First, Texture Second );

	/// <summary>What tells a set of stickers from another's, as text: where each is and how it looks.</summary>
	internal static string KeyOf( IReadOnlyList<Stuck> stickers )
	{
		var key = new System.Text.StringBuilder();
		foreach ( var sticker in stickers )
			key.Append( '|' ).Append( sticker.X ).Append( sticker.Y ).Append( sticker.Z ).Append( sticker.Look ).Append( sticker.Tint ).Append( sticker.First?.ResourceName ).Append( sticker.Second?.ResourceName );

		return key.ToString();
	}

	/// <summary>Sets sticker <paramref name="index"/>'s pictures on <paramref name="made"/>, one of the skin's materials: its picture drawn since.</summary>
	internal static void Retexture( Material made, int index, Texture first, Texture second )
	{
		if ( index >= MostStickers || !made.IsValid() )
			return;

		// Bindless: the shader reads each picture by its index, so eight stickers take no texture slots
		var main = first ?? Texture.Transparent;
		made.Set( Pictures[ index ], new Vector4( main.Index, (second ?? main).Index, 0f, 0f ) );
	}

	/// <summary>Sets sticker <paramref name="index"/>'s colour on <paramref name="made"/>: a moving sticker's, each frame. No allocations.</summary>
	internal static void Tint( Material made, int index, Color colour )
	{
		if ( index < MostStickers && made.IsValid() )
			made.Set( TintOf[ index ], new Vector4( Linear( colour ), 1f ) );
	}

	/// <summary>
	/// Where a weapon lies in its model's own space, for a skin's bands and patterns: its middle, the long way along it,
	/// and where that starts and ends.
	/// </summary>
	internal readonly record struct Frame( Vector3 Center, Vector3 Axis, Vector2 Range )
	{
		/// <summary>The frame of a model with <paramref name="bounds"/>: along its longest side.</summary>
		internal static Frame Of( BBox bounds )
		{
			var size = bounds.Size;
			var axis = size.x >= size.y && size.x >= size.z ? Vector3.Forward : size.y >= size.z ? Vector3.Left : Vector3.Up;
			return new Frame( bounds.Center, axis, new Vector2( Vector3.Dot( bounds.Mins, axis ), Vector3.Dot( bounds.Maxs, axis ) ) );
		}
	}

	/// <summary>
	/// The material <paramref name="source"/> (one of a weapon's) is drawn with under <paramref name="design"/>'s skin,
	/// finish and tint, lying along <paramref name="frame"/>; <paramref name="key"/> is the design's skin as text (see
	/// <see cref="KeyOf"/>), worked out once for all its materials. Null when the shader can't be had.
	/// </summary>
	internal static Material For( Material source, WeaponDesign design, string key, Frame frame, IReadOnlyList<DrawnShape> shapes, IReadOnlyList<Stuck> stickers )
	{
		var name = $"{source?.ResourcePath ?? source?.Name}|{key}|{frame}";
		if ( Kept.TryGetValue( name, out var kept ) && kept.IsValid() )
			return kept;

		if ( Kept.Count >= MostKept )
			Kept.Clear();

		var made = Material.Create( $"weapon_skin_{_made++}", Shader );
		if ( made is null )
		{
			Log.Warning( $"Player Model Selector: couldn't make a weapon skin with {Shader}." );
			return null;
		}

		Dress( made, source, design, frame, shapes );
		Press( made, stickers );
		Kept[ name ] = made;
		return made;
	}

	/// <summary>What tells <paramref name="design"/>'s surface from another's: its skin, finish and tint, as text.</summary>
	internal static string KeyOf( WeaponDesign design ) => $"{JsonSerializer.Serialize( design.Skin )}|{design.Finish}|{design.Tint}";

	private static void Dress( Material made, Material source, WeaponDesign design, Frame frame, IReadOnlyList<DrawnShape> shapes )
	{
		var colour = source?.GetTexture( ColorTexture );
		made.Set( ColorTexture, colour is { IsValid: true, IsError: false } ? colour : Texture.White );
		if ( source?.GetTexture( NormalTexture ) is { IsValid: true, IsError: false } normal )
			made.Set( NormalTexture, normal );

		made.Set( "g_flAlphaTest", (source?.GetFeature( AlphaTestFeature ) ?? 0) > 0 ? 1f : 0f );

		// The finish: its colour under the tint, how metal and rough
		var (metalness, roughness) = FinishMaterials.SurfaceOf( design.Finish );
		var finish = string.IsNullOrEmpty( design.Finish ) ? Color.White : FinishMaterials.ColourOf( design.Finish );
		var tint = Color.Parse( design.Tint ?? "" ) ?? Color.White;
		made.Set( "g_vSkinTint", Linear( finish ) * Linear( tint ) );
		made.Set( "g_flSkinMetalness", metalness );
		made.Set( "g_flSkinRoughness", roughness );

		made.Set( "g_vCenter", frame.Center );
		made.Set( "g_vAxis", frame.Axis );
		made.Set( "g_vAxisRange", frame.Range );

		var skin = design.Skin;
		if ( skin is null )
			return;

		made.Set( "g_flPattern", (float)skin.PatternIs );
		made.Set( "g_vPatternA", Rgb( skin.PatternColor ) );
		made.Set( "g_vPatternB", Rgb( skin.PatternColorEnd ) );
		made.Set( "g_flPatternSize", PatternUnits * skin.PatternScale );
		made.Set( "g_flPatternTurn", skin.PatternTurn * MathF.PI / 180f );
		made.Set( "g_flPatternScroll", skin.PatternScroll );
		made.Set( "g_flPatternSpin", skin.PatternSpin );
		if ( skin.PatternIs == WeaponSkin.Pattern.Shapes )
			made.Set( "PatternShape", PatternTexture( skin.PatternShape, shapes ) );

		made.Set( "g_flMotion", (float)skin.AnimationIs );
		var colours = skin.Colours();
		made.Set( "g_flPaletteCount", (float)Math.Clamp( colours.Count, 1, WeaponSkin.MaxPalette ) );
		for ( var i = 0; i < WeaponSkin.MaxPalette; i++ )
			made.Set( $"g_vPalette{i}", Rgb( colours[ i % colours.Count ] ) );

		made.Set( "g_flSpeed", skin.Speed );
		made.Set( "g_flBands", (float)skin.Bands );
	}

	// The stickers on the surface, each as it is placed, looks and is coloured
	private static void Press( Material made, IReadOnlyList<Stuck> stickers )
	{
		var count = Math.Min( stickers.Count, MostStickers );
		made.Set( "g_flStickerCount", (float)count );
		for ( var i = 0; i < count; i++ )
		{
			var sticker = stickers[ i ];
			made.Set( RowX[ i ], sticker.X );
			made.Set( RowY[ i ], sticker.Y );
			made.Set( RowZ[ i ], sticker.Z );
			made.Set( Look[ i ], sticker.Look );
			Tint( made, i, sticker.Tint );
			Retexture( made, i, sticker.First, sticker.Second );
		}
	}

	// A drawn shape's picture once it is drawn (a star meanwhile), or a picture or standard shape's.
	// shortcut: a skin made before its drawn shape is drawn keeps the star until the skin changes; ask again on
	// ShapeTextures.Generation if drawn patterns turn out to be used much
	
	private static Texture PatternTexture( string name, IReadOnlyList<DrawnShape> shapes )
	{
		if ( DrawnShape.IsReference( name ) && shapes is not null )
		{
			foreach ( var shape in shapes )
			{
				if ( shape.Reference == name )
					return ShapeTextures.TextureOf( shape ) ?? EffectAtlas.TextureOf( nameof( StandardShape.Star5 ) );
			}
		}

		return EffectAtlas.TextureOf( name );
	}

	private static Vector3 Rgb( string hex ) => Linear( Color.Parse( hex ?? "" ) ?? Color.White );

	// The shader lights colours as linear light, as it reads textures; a colour picked (or a finish's, which the standard
	// shader turns linear itself) is sRGB, and given as it is would show washed out
	private static Vector3 Linear( Color colour ) => new( Linear( colour.r ), Linear( colour.g ), Linear( colour.b ) );

	private static float Linear( float srgb ) => srgb <= 0.04045f ? srgb / 12.92f : MathF.Pow( (srgb + 0.055f) / 1.055f, 2.4f );
}
