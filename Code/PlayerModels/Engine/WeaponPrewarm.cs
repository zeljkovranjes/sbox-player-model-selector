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
	/// <summary>The most prop models kept loaded for dressing: past it they are let go of and loaded again when needed.</summary>
	internal const int MostProps = 256;

	private static readonly Dictionary<string, Model> Props = new();

	/// <summary>The prop model at <paramref name="path"/> if it was loaded ahead; null when it has to be loaded now.</summary>
	internal static Model PropModel( string path ) => path is not null && Props.TryGetValue( path, out var model ) && model.IsValid() ? model : null;

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

				if ( Props.Count >= MostProps )
					Props.Clear();

				Props[ prop.Model ] = model;
			}

			if ( design.Skin is null || await ModelLoader.LoadAsync( design.Weapon ) is not { } weapon )
				continue;

			// The world model's own materials, as Skin makes them when it is dressed
			var key = WeaponSkins.KeyOf( design );
			var frame = WeaponSkins.Frame.Of( weapon.Bounds );
			foreach ( var material in weapon.Materials )
				WeaponSkins.For( material, design, key, frame, shapes );
		}
	}
}
