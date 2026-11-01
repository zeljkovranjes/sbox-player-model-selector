using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The items a look can wear: the citizen's stock clothes and the clothes published on the Workshop. Nothing else is ever
/// put on, whatever a look says: a Workshop package is only used if it really is clothing.
/// </summary>
internal static class AccessoryCatalog
{
	private const string StockFolder = "models/citizen_clothes/";
	private const string ClothingPackage = "clothing";
	private const string WorkshopQuery = "type:clothing sort:popular";

	// A model with no mesh to measure counts as standing as tall as the citizen
	private const float FallbackHeight = 72f;

	// Workshop items loaded so far, by ident: their names and pictures are shown without asking again
	private static readonly Dictionary<string, Item> Loaded = new();

	/// <summary>An item: what a look keeps of it, what it is called, what it is offered as, its model and its picture.</summary>
	internal sealed record Item( string Path, string Title, string Group, string Model, string Icon )
	{
		/// <summary>Whether it bends with the whole body rather than sitting on one joint.</summary>
		internal bool Follows => !Accessory.IsPinned( Group );
	}

	/// <summary>A page of the Workshop's clothes, and how many there are in all.</summary>
	internal sealed record WorkshopPage( List<Item> Items, int Total );

	/// <summary>Every stock item, by title.</summary>
	internal static List<Item> All()
	{
		return ResourceLibrary.GetAll<Clothing>()
			.Where( x => x.ResourcePath?.StartsWith( StockFolder ) == true )
			.Select( x => ItemOf( x, x.ResourcePath ) )
			.Where( x => x is not null )
			.OrderBy( x => x.Title )
			.ToList();
	}

	/// <summary>
	/// A page of the Workshop's clothes, the most popular first, matching <paramref name="search"/> if there is one. Fails
	/// with the backend's own exception. Their groups and models aren't known until each is loaded (see <see cref="LoadAsync"/>).
	/// </summary>
	internal static async Task<WorkshopPage> SearchAsync( string search, int page, int perPage )
	{
		var query = string.IsNullOrWhiteSpace( search ) ? WorkshopQuery : $"{WorkshopQuery} {search.Trim()}";
		var found = await Package.FindAsync( query, perPage, page * perPage );
		var items = (found?.Packages ?? System.Array.Empty<Package>())
			.Select( x => new Item( x.FullIdent, x.Title, null, null, x.Thumb ) )
			.ToList();

		return new WorkshopPage( items, found?.TotalCount ?? 0 );
	}

	/// <summary>The item <paramref name="path"/> names if it is one known here already, or null (see <see cref="LoadAsync"/>).</summary>
	internal static Item Find( string path )
	{
		if ( string.IsNullOrEmpty( path ) )
			return null;

		if ( Loaded.TryGetValue( path, out var loaded ) )
			return loaded;

		return ResourceLibrary.TryGet<Clothing>( path, out var clothing ) && path.StartsWith( StockFolder ) ? ItemOf( clothing, path ) : null;
	}

	/// <summary>
	/// The item <paramref name="path"/> names, downloading it if it comes from the Workshop, or null when it isn't one or
	/// can't be had. Fails with the backend's own exception.
	/// </summary>
	internal static async Task<Item> LoadAsync( string path )
	{
		if ( Find( path ) is { } known )
			return known;

		if ( string.IsNullOrEmpty( path ) || path.Contains( '/' ) || !Package.TryParseIdent( path, out _ ) )
			return null;

		// Asked before anything downloads: a look comes from any player, and only clothing is worn
		var package = await Package.FetchAsync( path, true );
		if ( package?.TypeName != ClothingPackage )
			return null;

		var clothing = await Cloud.Load<Clothing>( path );
		var item = clothing is null ? null : ItemOf( clothing, path );
		if ( item is null )
			return null;

		// What it is shown as on the Workshop: published clothes rarely come with an icon of their own
		item = item with { Icon = package.Thumb ?? item.Icon };

		Loaded[ path ] = item;
		return item;
	}

	/// <summary>How tall <paramref name="model"/> stands, as made.</summary>
	internal static float HeightOf( Model model )
	{
		var height = model?.Bounds.Size.z ?? 0f;
		return height > 0f ? height : FallbackHeight;
	}

	/// <summary>The length of <paramref name="model"/>'s longest side, as made.</summary>
	internal static float SizeOf( Model model )
	{
		var size = model?.Bounds.Size ?? Vector3.Zero;
		return System.Math.Max( size.x, System.Math.Max( size.y, size.z ) );
	}

	private static Item ItemOf( Clothing clothing, string path )
	{
		var model = string.IsNullOrEmpty( clothing.Model ) ? clothing.HumanAltModel : clothing.Model;
		var group = Accessory.GroupOf( clothing.Category.ToString() );
		if ( group is null || string.IsNullOrEmpty( model ) )
			return null;

		var icon = string.IsNullOrEmpty( clothing.Icon.Path ) ? $"thumb:{model}" : clothing.Icon.Path;
		return new Item( path, clothing.Title, group, model, icon );
	}
}
