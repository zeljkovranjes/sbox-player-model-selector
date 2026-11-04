using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The materials of the stock finishes (see <see cref="MaterialFinish"/>): the engine's own materials, shipped with every
/// install, made metal and coloured. They replace a material outright, so unlike a colour they also go on the materials
/// of mounted games, which can't be copied.
/// </summary>
internal static class FinishMaterials
{
	private const string Shiny = "materials/shine.vmat";
	private const string Plain = "materials/default.vmat";
	private const string Black = "materials/black_matte.vmat";

	private const string TintAttribute = "g_vColorTint";
	private const string MetalnessAttribute = "g_flMetalness";
	private const string ColorTexture = "g_tColor";
	private const string NormalTexture = "g_tNormal";

	// Features deciding which pixels draw, from the alpha of the colour texture: hair cards would be solid sheets without
	[SkipHotload]
	private static readonly string[] CutOutFeatures = { "F_ALPHA_TEST", "F_TRANSLUCENT" };
	private const string AlphaTestFeature = "F_ALPHA_TEST";

	// How rough each kind of finish is, where it is drawn by a shader of its own (a weapon's skin)
	private const float ShinyRoughness = 0.25f;
	private const float PlainRoughness = 0.6f;
	private const float MatteRoughness = 0.85f;

	private readonly record struct Recipe( string Material, Color Colour, float Metalness, float Roughness );

	private static Recipe RecipeOf( string finish ) => finish switch
	{
		"Gold" => new( Shiny, new Color( 1f, 0.77f, 0.3f ), 1f, ShinyRoughness ),
		"Silver" => new( Shiny, new Color( 0.85f, 0.87f, 0.9f ), 1f, ShinyRoughness ),
		"Chrome" => new( Shiny, Color.White, 1f, ShinyRoughness ),
		"Copper" => new( Shiny, new Color( 0.85f, 0.45f, 0.27f ), 1f, ShinyRoughness ),
		"Bronze" => new( Shiny, new Color( 0.55f, 0.36f, 0.18f ), 1f, ShinyRoughness ),
		"Gloss" => new( Shiny, Color.White, 0f, ShinyRoughness ),
		"Matte black" => new( Black, new Color( 0.08f, 0.08f, 0.08f ), 0f, MatteRoughness ),
		_ => new( Plain, Color.White, 0f, PlainRoughness ),
	};

	/// <summary>The colour <paramref name="finish"/> has until the player colours it.</summary>
	internal static Color ColourOf( string finish ) => RecipeOf( finish ).Colour;

	/// <summary>How metal and how rough <paramref name="finish"/> is (null or empty: a plain surface).</summary>
	internal static (float Metalness, float Roughness) SurfaceOf( string finish )
	{
		var recipe = RecipeOf( finish );
		return (recipe.Metalness, recipe.Roughness);
	}

	/// <summary>
	/// A material of <paramref name="finish"/> to stand in for <paramref name="worn"/>, keeping its bumps and which of its
	/// pixels draw. A new one each time: each part is coloured on its own. Null when the engine's material is missing.
	/// </summary>
	internal static Material Make( string finish, Material worn )
	{
		var recipe = RecipeOf( finish );
		var made = Material.Load( recipe.Material )?.CreateCopy();
		if ( made is null )
		{
			Log.Warning( $"Player Model Selector: couldn't copy {recipe.Material} for the {finish} finish." );
			return null;
		}

		made.Set( MetalnessAttribute, recipe.Metalness );
		made.Set( TintAttribute, recipe.Colour );

		// Cut out everywhere: the engine's no-draw material still draws, in tool yellow, once it is copied
		if ( finish == MaterialFinish.Hidden )
		{
			made.SetFeature( AlphaTestFeature, 1 );
			made.Set( ColorTexture, Texture.Transparent );
			return made;
		}

		if ( worn is null )
			return made;

		if ( worn.GetTexture( NormalTexture ) is { IsValid: true, IsError: false } normal )
			made.Set( NormalTexture, normal );

		var cutOut = false;
		foreach ( var feature in CutOutFeatures )
		{
			var value = worn.GetFeature( feature );
			if ( value <= 0 )
				continue;

			made.SetFeature( feature, value );
			cutOut = true;
		}

		// The shape cut out is in the colour texture's alpha, so it stays and the finish tints it
		if ( cutOut && worn.GetTexture( ColorTexture ) is { IsValid: true, IsError: false } color )
			made.Set( ColorTexture, color );

		return made;
	}
}
