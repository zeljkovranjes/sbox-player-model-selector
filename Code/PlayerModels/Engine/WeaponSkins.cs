using System;
using System.Collections.Generic;
using System.Text.Json;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The materials a weapon's skin (see <see cref="WeaponSkin"/>) is drawn with: each of the weapon's own materials made
/// again on the selector's skin shader, keeping its whole surface (colour, bumps, roughness, metalness, shadows and glow,
/// read as its own shader reads them), with the finish, tint and pattern as its settings, its moving colours drawn from
/// the skin's texture (see <see cref="SkinTextures"/>, filled by its scripts once a frame for everyone wearing it), and
/// the design's stickers pressed onto it (see <see cref="Stuck"/>). Nothing is set on a material per frame but a moving
/// sticker's colour. Made once for each
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

	// What the weapon's own materials keep their surface in, and how the skin shader is given it
	private const string ColorTexture = "g_tColor";
	private const string NormalTexture = "g_tNormal";
	private const string RmaTexture = "g_tRma";
	private const string OcclusionTexture = "g_tAmbientOcclusion";
	private const string GlowTexture = "g_tSelfIllumMask";
	private const string AlphaTestFeature = "F_ALPHA_TEST";
	private const string MetalnessTextureFeature = "F_METALNESS_TEXTURE";
	private const string GlowFeature = "F_SELF_ILLUM";

	// Which way the skin shader reads the weapon's own surface (see weapon_skin.shader): its colour alone, a complex
	// material's (bumps folded into two channels, roughness in the third), or a standard one's (roughness, metalness and
	// shadows in one texture)
	private enum SourceKind { ColourOnly, Complex, Standard }

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
		{
			Move( kept, design.Skin );
			return kept;
		}

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
		Surface( made, source );

		// The finish: its colour under the tint, how metal and rough in place of the surface's own; a tint alone keeps them
		var (metalness, roughness) = FinishMaterials.SurfaceOf( design.Finish );
		var finish = string.IsNullOrEmpty( design.Finish ) ? Color.White : FinishMaterials.ColourOf( design.Finish );
		var tint = Color.Parse( design.Tint ?? "" ) ?? Color.White;
		made.Set( "g_vSkinTint", Linear( finish ) * Linear( tint ) );
		made.Set( "g_flFinished", string.IsNullOrEmpty( design.Finish ) ? 0f : 1f );
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
		if ( skin.PatternIs == WeaponSkin.Pattern.Shapes )
			made.Set( "g_tPatternShape", PatternTexture( skin.PatternShape, shapes ) );

		Move( made, skin );
	}

	/// <summary>
	/// Gives <paramref name="made"/> the texture <paramref name="skin"/>'s moving colours are drawn from (see
	/// <see cref="SkinTextures"/>), or none when nothing moves; asked again each time a kept material is dressed, so one
	/// whose texture was let go of gets it back.
	/// </summary>
	private static void Move( Material made, WeaponSkin skin )
	{
		var moving = SkinTextures.For( skin );
		made.Set( "g_flSkinMoving", moving is null ? 0f : 1f );
		if ( moving is null )
			return;

		made.Set( "g_tSkinColours", moving.Texture );
		made.Set( "g_flSkinBanded", moving.Colours.Banded ? 1f : 0f );
		made.Set( "g_flSkinBands", (float)moving.Colours.Bands );
	}

	// The weapon's own surface, as its material keeps it: the skin draws it the same, so a sticker on a plain weapon is the
	// gun as it was plus the sticker
	private static void Surface( Material made, Material source )
	{
		var colour = Usable( source?.GetTexture( ColorTexture ) );
		var normal = Usable( source?.GetTexture( NormalTexture ) );
		var kind = SourceKindOf( source );
		var surface = kind == SourceKind.Complex ? Usable( source.GetTexture( OcclusionTexture ) ) : kind == SourceKind.Standard ? Usable( source.GetTexture( RmaTexture ) ) : null;
		var alphaTest = (source?.GetFeature( AlphaTestFeature ) ?? 0) > 0;
		made.Set( ColorTexture, colour ?? Texture.White );
		made.Set( NormalTexture, normal ?? Texture.White );
		made.Set( RmaTexture, surface ?? Texture.White );
		made.Set( "g_tSourceGlow", Texture.Black );
		made.Set( "g_flSourceKind", (float)kind );
		made.Set( "g_flSourceHasNormal", normal is null ? 0f : 1f );
		made.Set( "g_flAlphaTest", alphaTest ? 1f : 0f );
		if ( kind != SourceKind.Complex )
			return;

		// A cut-out's colour alpha is where it is cut, so its metalness is the one number
		made.Set( "g_flMetalnessInColor", source.GetFeature( MetalnessTextureFeature ) > 0 && !alphaTest ? 1f : 0f );
		made.Set( "g_flSourceMetalness", source.GetVector4( "g_flMetalness" ).x );
		made.Set( "g_flSourceRoughnessPower", RoughnessPower( source.GetVector4( "g_flRoughnessScaleFactor" ).x ) );
		var colourTint = source.GetVector4( "g_vColorTint" );
		made.Set( "g_vSourceTint", Linear( new Color( colourTint.x, colourTint.y, colourTint.z ) ) );

		// Its glow, as the complex shader lights it: the mask in the tint's colour, 2 to the brightness, at most its scale
		if ( source.GetFeature( GlowFeature ) <= 0 || Usable( source.GetTexture( GlowTexture ) ) is not { } glow )
			return;

		var glowTint = source.GetVector4( "g_vSelfIllumTint" );
		var strength = MathF.Pow( 2f, source.GetVector4( "g_flSelfIllumBrightness" ).x ) * Math.Clamp( source.GetVector4( "g_flSelfIllumScale" ).x, 0f, 1f );
		made.Set( "g_tSourceGlow", glow );
		made.Set( "g_vSourceGlow", Linear( new Color( glowTint.x, glowTint.y, glowTint.z ) ) * strength );
		made.Set( "g_flSourceGlowAlbedo", Math.Clamp( source.GetVector4( "g_flSelfIllumAlbedoFactor" ).x, 0f, 1f ) );
	}

	// Complex (and Half-Life: Alyx's vr_) materials fold their bumps and keep roughness beside them; standard ones keep a
	// roughness-metalness-shadow texture
	private static SourceKind SourceKindOf( Material source )
	{
		var shader = source?.ShaderName ?? "";
		if ( shader.Contains( "complex", StringComparison.OrdinalIgnoreCase ) || shader.Contains( "/vr_", StringComparison.OrdinalIgnoreCase ) )
			return SourceKind.Complex;

		return Usable( source?.GetTexture( RmaTexture ) ) is not null ? SourceKind.Standard : SourceKind.ColourOnly;
	}

	private static Texture Usable( Texture texture ) => texture is { IsValid: true, IsError: false } ? texture : null;

	// A material that never set its roughness scale reads as 0: as made
	private static float RoughnessPower( float scale ) => float.IsFinite( scale ) && scale > 0f ? Math.Clamp( scale, 0.05f, 8f ) : 1f;

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
