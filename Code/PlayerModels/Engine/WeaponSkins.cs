using System;
using System.Collections.Generic;
using System.Text.Json;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The materials a weapon's skin (see <see cref="WeaponSkin"/>) is drawn with: each of the weapon's own materials made
/// again on the selector's skin shader, keeping its colour and bumps, with the finish, tint, pattern and moving colours
/// as its settings. The shader moves the colours itself, from the time of the frame, so nothing is set per frame. Made
/// once for each material and skin and shared by everyone showing the same: a hundred players with the same skin on the
/// same weapon draw with one set of materials.
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
	internal static Material For( Material source, WeaponDesign design, string key, Frame frame, IReadOnlyList<DrawnShape> shapes )
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
		made.Set( "g_vSkinTint", new Vector3( finish.r * tint.r, finish.g * tint.g, finish.b * tint.b ) );
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

	private static Vector3 Rgb( string hex )
	{
		var colour = Color.Parse( hex ?? "" ) ?? Color.White;
		return new Vector3( colour.r, colour.g, colour.b );
	}
}
