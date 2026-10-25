using System.Collections.Generic;
using System.Linq;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// What this player keeps between sessions, on this machine: the model they wear, how they customised each model and the
/// looks they saved, their favourites, their first-person arms, which grid headings they collapsed and, as a host, which
/// models their server allows.
/// </summary>
internal static class LocalPrefs
{
	private const string PickCookie = "player_model_selector.pick";
	private const string LooksCookie = "player_model_selector.looks";
	private const string FavoritesCookie = "player_model_selector.favorites";
	private const string ViewModelCookie = "player_model_selector.viewmodel";
	private const string HeadingsCookie = "player_model_selector.headings";
	private const string SavedLooksCookie = "player_model_selector.saved_looks";
	private const string ModelArmsCookie = "player_model_selector.model_arms";
	private const string ServerRulesCookie = "player_model_selector.server_rules";

	/// <summary>The model worn last, empty for the default citizen.</summary>
	internal static string Pick
	{
		get => Game.Cookies.Get( PickCookie, "" );
		set => Game.Cookies.Set( PickCookie, value ?? "" );
	}

	/// <summary>The first-person arms picked, empty for Sandbox's own.</summary>
	internal static string ViewModel
	{
		get => Game.Cookies.Get( ViewModelCookie, "" );
		set => Game.Cookies.Set( ViewModelCookie, value ?? "" );
	}

	/// <summary>
	/// Whether first-person arms come from the worn model (when it isn't a stock character) instead of the arms picked
	/// on the View Model page.
	/// </summary>
	internal static bool UseModelArms
	{
		get => Game.Cookies.Get( ModelArmsCookie, true );
		set => Game.Cookies.Set( ModelArmsCookie, value );
	}

	/// <summary>The models this player's server allows, as they left it when they last hosted.</summary>
	internal static ServerRules ServerRules
	{
		get => Game.Cookies.Get( ServerRulesCookie, new ServerRules() ) ?? new ServerRules();
		set => Game.Cookies.Set( ServerRulesCookie, value );
	}

	/// <summary>How <paramref name="model"/> was customised last, or the model as it comes.</summary>
	internal static PlayerLook LookFor( string model )
	{
		var looks = Game.Cookies.Get( LooksCookie, new Dictionary<string, string>() );
		return looks.TryGetValue( model ?? "", out var saved ) ? PlayerLook.Parse( saved ).WithModel( model ) : new PlayerLook { Model = model ?? "" };
	}

	/// <summary>Keeps <paramref name="look"/> as how its model is customised.</summary>
	internal static void Remember( PlayerLook look )
	{
		if ( look.IsDefault )
			return;

		var looks = Game.Cookies.Get( LooksCookie, new Dictionary<string, string>() );
		looks[ look.Model ] = look.Serialize();
		Game.Cookies.Set( LooksCookie, looks );
	}

	/// <summary>The looks saved for <paramref name="model"/>, in the order they were saved.</summary>
	internal static List<SavedLook> SavedLooks( string model )
	{
		var all = Game.Cookies.Get( SavedLooksCookie, new Dictionary<string, List<SavedLook>>() );
		return all.TryGetValue( model ?? "", out var saved )
			? saved.Where( x => !string.IsNullOrWhiteSpace( x?.Name ) ).ToList()
			: new List<SavedLook>();
	}

	/// <summary>Saves <paramref name="look"/> for its model as <paramref name="name"/>, in place of a look saved under that name.</summary>
	internal static void SaveLook( string name, PlayerLook look )
	{
		if ( look.IsDefault || string.IsNullOrWhiteSpace( name ) )
			return;

		var all = Game.Cookies.Get( SavedLooksCookie, new Dictionary<string, List<SavedLook>>() );
		var saved = SavedLooks( look.Model );
		var entry = new SavedLook { Name = name.Trim(), Look = look.Serialize() };
		var index = saved.FindIndex( x => x.Name == entry.Name );

		if ( index >= 0 )
			saved[ index ] = entry;
		else
			saved.Add( entry );

		all[ look.Model ] = saved;
		Game.Cookies.Set( SavedLooksCookie, all );
	}

	/// <summary>Deletes the look saved for <paramref name="model"/> as <paramref name="name"/>.</summary>
	internal static void DeleteSavedLook( string model, string name )
	{
		var all = Game.Cookies.Get( SavedLooksCookie, new Dictionary<string, List<SavedLook>>() );
		var saved = SavedLooks( model );
		saved.RemoveAll( x => x.Name == name );

		if ( saved.Count == 0 )
			all.Remove( model ?? "" );
		else
			all[ model ?? "" ] = saved;

		Game.Cookies.Set( SavedLooksCookie, all );
	}

	/// <summary>The starred models, in the order they were starred.</summary>
	internal static List<FavoriteModel> Favorites()
	{
		return Game.Cookies.Get( FavoritesCookie, new List<FavoriteModel>() )
			.Where( x => !string.IsNullOrWhiteSpace( x?.Model ) )
			.ToList();
	}

	/// <summary>
	/// The grid headings the player collapsed (true) or opened (false), by key ("Mounts", "Mounts/Counter-Strike"). A
	/// heading not listed was never touched and keeps its default.
	/// </summary>
	internal static Dictionary<string, bool> Headings()
	{
		return Game.Cookies.Get( HeadingsCookie, new Dictionary<string, bool>() );
	}

	/// <summary>Collapses or opens the grid heading <paramref name="key"/>, and keeps it that way.</summary>
	internal static void SetCollapsed( string key, bool collapsed )
	{
		var headings = Headings();
		headings[ key ] = collapsed;
		Game.Cookies.Set( HeadingsCookie, headings );
	}

	/// <summary>
	/// Favorites <paramref name="model"/> (arms when <paramref name="viewModel"/>), or un-favorites it if it was one.
	/// Returns whether it is a favorite now.
	/// </summary>
	internal static bool ToggleFavorite( string model, string title, bool viewModel = false )
	{
		var favorites = Favorites();
		var existing = favorites.FirstOrDefault( x => x.Model == model );

		if ( existing is not null )
			favorites.Remove( existing );
		else
			favorites.Add( new FavoriteModel { Model = model, Title = title, ViewModel = viewModel } );

		Game.Cookies.Set( FavoritesCookie, favorites );
		return existing is null;
	}
}
