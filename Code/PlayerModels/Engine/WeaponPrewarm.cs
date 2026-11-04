using System.Collections.Generic;
using System.Threading.Tasks;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Gets a look's weapon designs ready before any of them is held, as soon as the look arrives (a player's own, or one
/// coming into view): each design's skin materials for its world model (see <see cref="WeaponSkins"/>), its props'
/// models loaded and its moving stickers' scripts compiled. Dressing a weapon when it is drawn, swapped or seen in first
/// person then finds them made and puts everything on in the same frame, rather than over the frames after. Once per
/// look change; nothing per frame. The materials are kept within <see cref="WeaponSkins"/>'s cap, the models within
/// <see cref="MostProps"/>.
/// </summary>
internal static class WeaponPrewarm
{
	/// <summary>The most prop models kept loaded for dressing: past it the one used longest ago is let go of.</summary>
	internal const int MostProps = 256;

	// Made afresh on a hot reload: what it keeps is made again as it is needed
	[SkipHotload]
	private static readonly RecentCache<string, Model> Props = new( MostProps );

	/// <summary>The prop model at <paramref name="path"/> if it was loaded ahead; null when it has to be loaded now.</summary>
	internal static Model PropModel( string path ) => path is not null && Props.TryGet( path, out var model ) && model.IsValid() ? model : null;

	/// <summary>
	/// The prop model at <paramref name="path"/>, loaded ahead or, for a stock prop (shipped with the game, so on disk),
	/// loaded now: a weapon is dressed whole in the frame it appears. Null for one that has to be fetched.
	/// </summary>
	internal static Model PropModelNow( string path )
	{
		if ( PropModel( path ) is { } kept )
			return kept;

		if ( !PropCatalog.IsStock( path ) || Model.Load( path ) is not { IsValid: true, IsError: false } model )
			return null;

		Keep( path, model );
		return model;
	}

	private static void Keep( string path, Model model ) => Props.Set( path, model );

	/// <summary>
	/// Readies every design of <paramref name="designs"/> (whose stickers and patterns name <paramref name="shapes"/>). A
	/// model that doesn't load is skipped: dressing tries it again.
	/// </summary>
	internal static async Task WarmAsync( IReadOnlyList<WeaponDesign> designs, IReadOnlyList<DrawnShape> shapes )
	{
		// Copied first: an editor changes a look's designs in place while the models load
		try
		{
			await WarmEachAsync( new List<WeaponDesign>( designs ), new List<DrawnShape>( shapes ) );
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"Player Model Selector: couldn't ready weapon designs ahead ({e.Message}); they are made when drawn." );
		}
	}

	private static async Task WarmEachAsync( List<WeaponDesign> designs, List<DrawnShape> shapes )
	{
		foreach ( var design in designs )
		{
			foreach ( var sticker in design.Stickers ?? new List<WeaponSticker>() )
			{
				if ( PresetScripts.StickerMotionScript( sticker.AnimationIs ) is { } script )
					ScriptPrograms.Cache.Compile( script, ScriptKind.Sticker );
			}

			foreach ( var prop in new List<WeaponProp>( design.Props ?? new List<WeaponProp>() ) )
			{
				if ( !PropCatalog.IsStock( prop.Model ) || PropModel( prop.Model ) is not null )
					continue;

				if ( await ModelLoader.LoadAsync( prop.Model ) is not { } model )
					continue;

				Keep( prop.Model, model );
			}

			// A design with stickers has them pressed into its skin, worked out where they lie on the weapon dressed: that
			// skin is made when it is, its weapon's mesh (where each sticker lands) read now
			if ( design.Stickers is { Count: > 0 } && await ModelLoader.LoadAsync( design.Weapon ) is { } stickered )
			{
				StickerSurface.Prepare( stickered );
				await WarmShaderAsync( stickered, design, shapes );
			}

			if ( design.Skin is null || design.Stickers is { Count: > 0 } || await ModelLoader.LoadAsync( design.Weapon ) is not { } weapon )
				continue;

			// The world model's own materials, as Skin makes them when it is dressed
			var key = WeaponSkins.KeyOf( design );
			var frame = WeaponSkins.Frame.Of( weapon.Bounds );
			foreach ( var material in weapon.Materials )
				WeaponSkins.For( material, design, key, frame, shapes, System.Array.Empty<WeaponSkins.Stuck>() );

			await WarmShaderAsync( weapon, design, shapes );
		}
	}

	// The graphics card readies a shader the first time it draws with it, which can cost that frame or leave what it draws
	// out: the skin's is drawn once, in this session's first frames with a design, by a copy of the weapon too small to see
	// right in front of the camera, so its first time is never a weapon's
	private const float WarmScale = 0.0005f;
	private const int WarmFrames = 3;
	private const int FrameMilliseconds = 16;
	private static bool _shaderWarmed;

	private static async Task WarmShaderAsync( Model weapon, WeaponDesign design, List<DrawnShape> shapes )
	{
		var scene = Game.ActiveScene;
		var camera = scene?.Camera;
		if ( _shaderWarmed || !camera.IsValid() || weapon.Materials.Length == 0 )
			return;

		var made = WeaponSkins.For( weapon.Materials[ 0 ], design, WeaponSkins.KeyOf( design ), WeaponSkins.Frame.Of( weapon.Bounds ), shapes, System.Array.Empty<WeaponSkins.Stuck>() );
		if ( made is null )
			return;

		_shaderWarmed = true;
		GameObject warm;
		using ( scene.Push() )
		{
			warm = new GameObject( false, "Weapon skin warm-up" );
			warm.NetworkMode = NetworkMode.Never;
			warm.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.Hidden;
			var renderer = warm.AddComponent<ModelRenderer>();
			renderer.Model = weapon;
			for ( var i = 0; i < weapon.Materials.Length; i++ )
				renderer.Materials.SetOverride( i, made );

			warm.WorldPosition = camera.WorldPosition + camera.WorldRotation.Forward * (camera.ZNear + 1f);
			warm.WorldScale = WarmScale;
			warm.Enabled = true;
		}

		await GameTask.Delay( WarmFrames * FrameMilliseconds );
		if ( warm.IsValid() )
			warm.Destroy();
	}
}
