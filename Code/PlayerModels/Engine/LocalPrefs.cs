using System;
using System.Collections.Generic;
using System.Linq;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// What this player keeps between sessions, on this machine: the model they wear, how they customised each model and the
/// looks they saved, their favourites, their first-person arms, which grid headings they collapsed, the mounted games'
/// folders they added, how they rated what others shared, how they sort it and whose they hid, how much of others'
/// effects they see and whose they hid, the effects and shapes they keep ("My effects", "My shapes"), how they like the
/// code editor (its theme, Auto-apply, where its window is) and the scripts they left as drafts, and, as a host, which
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
	private const string ModelFoldersCookie = "player_model_selector.model_folders";
	private const string VotesCookie = "player_model_selector.votes";
	private const string HiddenAuthorsCookie = "player_model_selector.hidden_authors";
	private const string SharesByNewestCookie = "player_model_selector.shares_by_newest";
	private const string EffectsQualityCookie = "player_model_selector.effects_quality";
	private const string HiddenEffectsCookie = "player_model_selector.hidden_effects";
	private const string MyEffectsCookie = "player_model_selector.my_effects";

	/// <summary>The most effects kept in My effects.</summary>
	internal const int MaxMyEffects = 50;

	private const string MyShapesCookie = "player_model_selector.my_shapes";
	private const string WeaponDesignsCookie = "player_model_selector.weapon_designs";
	private const string FirstPersonWeaponCookie = "player_model_selector.first_person_weapon";
	private const string CodeThemeCookie = "player_model_selector.code_theme";
	private const string CodeAutoApplyCookie = "player_model_selector.code_auto_apply";
	private const string CodeWindowCookie = "player_model_selector.code_window";

	// Script drafts can be long (a script each, as left), so they are kept in the game's data rather than its cookies
	private const string ScriptDraftsFile = "script_drafts.json";

	/// <summary>The most shapes kept in My shapes.</summary>
	internal const int MaxMyShapes = 50;

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

	/// <summary>
	/// The folders of mounted games this player added to where characters are looked for, as mount paths
	/// (<c>mount://deadlock/models/heroes_staging</c>).
	/// </summary>
	internal static List<string> ModelFolders
	{
		get => Game.Cookies.Get( ModelFoldersCookie, new List<string>() ) ?? new List<string>();
		set => Game.Cookies.Set( ModelFoldersCookie, value );
	}

	/// <summary>How <paramref name="model"/> was customised last, or the model as it comes.</summary>
	internal static PlayerLook LookFor( string model )
	{
		var looks = Game.Cookies.Get( LooksCookie, new Dictionary<string, string>() );
		var look = looks.TryGetValue( model ?? "", out var saved ) ? PlayerLook.Parse( saved ).WithModel( model ) : new PlayerLook { Model = model ?? "" };

		// Weapon designs are the player's, whichever model they wear: the ones kept last go on it
		return Game.Cookies.TryGetString( WeaponDesignsCookie, out _ ) ? look.WithWeapons( WeaponDesigns() ) : look;
	}

	/// <summary>
	/// Keeps <paramref name="look"/> as how its model is customised, and its weapon designs as the player's; for the
	/// default citizen, the effects and designs it wears (see <see cref="PlayerLook.EffectsAlone"/>), and nothing once it
	/// wears none.
	/// </summary>
	internal static void Remember( PlayerLook look )
	{
		Game.Cookies.Set( WeaponDesignsCookie, look.StandaloneWeapons() );

		var looks = Game.Cookies.Get( LooksCookie, new Dictionary<string, string>() );
		if ( !look.IsDefault )
			looks[ look.Model ] = look.Serialize();
		else if ( look.Effects.Count > 0 || look.Weapons.Count > 0 )
			looks[ "" ] = look.EffectsAlone().Serialize();
		else if ( !looks.Remove( "" ) )
			return;

		Game.Cookies.Set( LooksCookie, looks );
	}

	/// <summary>
	/// Whether the weapon in first person shows its design's props, stickers and effects (and the effects that come from
	/// the weapon held); off, it only shows its skin. On until the player turns it off.
	/// </summary>
	internal static bool ShowWeaponInFirstPerson
	{
		get => Game.Cookies.Get( FirstPersonWeaponCookie, true );
		set => Game.Cookies.Set( FirstPersonWeaponCookie, value );
	}

	/// <summary>The player's weapon designs as kept last, each on its own (carrying its drawn shapes), within bounds.</summary>
	internal static List<WeaponDesign> WeaponDesigns()
	{
		var kept = Game.Cookies.Get( WeaponDesignsCookie, new List<WeaponDesign>() ) ?? new List<WeaponDesign>();
		return kept.Select( x => x?.Sanitized() ).Where( x => x is not null ).Take( WeaponDesign.MaxPerLook ).ToList();
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

	/// <summary>How this player rated something shared (by table and record id, see SharedLibrary): 1 up, -1 down, 0 not at all.</summary>
	internal static int VoteOn( string key )
	{
		return Game.Cookies.Get( VotesCookie, new Dictionary<string, int>() ).GetValueOrDefault( key );
	}

	/// <summary>Remembers this player's rating of something shared (see <see cref="VoteOn"/>); 0 forgets it.</summary>
	internal static void SetVote( string key, int vote )
	{
		var votes = Game.Cookies.Get( VotesCookie, new Dictionary<string, int>() );
		if ( vote == 0 )
			votes.Remove( key );
		else
			votes[ key ] = vote;

		Game.Cookies.Set( VotesCookie, votes );
	}

	/// <summary>Whether the Workshops list what was shared newest first instead of the best rated first.</summary>
	internal static bool SharesByNewest
	{
		get => Game.Cookies.Get( SharesByNewestCookie, false );
		set => Game.Cookies.Set( SharesByNewestCookie, value );
	}

	/// <summary>The players (by Steam id) whose shares this player hid from their Workshops.</summary>
	internal static HashSet<long> HiddenAuthors()
	{
		return new HashSet<long>( Game.Cookies.Get( HiddenAuthorsCookie, new List<long>() ) ?? new List<long>() );
	}

	/// <summary>Hides what <paramref name="author"/> shares from this player's Workshops, or shows it again.</summary>
	internal static void SetHidden( long author, bool hidden )
	{
		var authors = HiddenAuthors();
		if ( hidden )
			authors.Add( author );
		else
			authors.Remove( author );

		Game.Cookies.Set( HiddenAuthorsCookie, authors.ToList() );
	}

	/// <summary>How much of everyone's effects this player sees, within what the host allows.</summary>
	internal static EffectBudget.Quality EffectsQuality
	{
		get => Game.Cookies.Get( EffectsQualityCookie, (int)EffectBudget.Quality.High ) is var quality && Enum.IsDefined( typeof( EffectBudget.Quality ), quality )
			? (EffectBudget.Quality)quality
			: EffectBudget.Quality.High;
		set => Game.Cookies.Set( EffectsQualityCookie, (int)value );
	}

	/// <summary>The players (by Steam id) whose effects this player hid.</summary>
	internal static HashSet<long> HiddenEffects()
	{
		return new HashSet<long>( Game.Cookies.Get( HiddenEffectsCookie, new List<long>() ) ?? new List<long>() );
	}

	/// <summary>Hides <paramref name="player"/>'s effects from this player, or shows them again.</summary>
	internal static void SetEffectsHidden( long player, bool hidden )
	{
		var players = HiddenEffects();
		if ( hidden )
			players.Add( player );
		else
			players.Remove( player );

		Game.Cookies.Set( HiddenEffectsCookie, players.ToList() );
	}

	/// <summary>
	/// The effects (and single layers) this player kept to put on again, in the order they were kept, each read within
	/// bounds; one that no longer reads is left out.
	/// </summary>
	internal static List<Effect> MyEffects()
	{
		var kept = Game.Cookies.Get( MyEffectsCookie, new List<Effect>() ) ?? new List<Effect>();
		return kept.Select( x => x?.Sanitized() ).Where( x => x is not null && !string.IsNullOrWhiteSpace( x.Name ) ).Take( MaxMyEffects ).ToList();
	}

	/// <summary>
	/// Keeps <paramref name="effect"/> (within bounds, as it would be shared) in My effects under its name, in place of one
	/// kept under that name. False when it has no name, or My effects is full.
	/// </summary>
	internal static bool SaveMyEffect( Effect effect )
	{
		var kept = MyEffects();
		var saved = effect?.Sanitized();
		if ( saved is null || string.IsNullOrWhiteSpace( saved.Name ) )
			return false;

		saved.Off = false;
		var index = kept.FindIndex( x => x.Name == saved.Name );
		if ( index >= 0 )
			kept[ index ] = saved;
		else if ( kept.Count >= MaxMyEffects )
			return false;
		else
			kept.Add( saved );

		Game.Cookies.Set( MyEffectsCookie, kept );
		return true;
	}

	/// <summary>Takes the effect kept as <paramref name="name"/> out of My effects.</summary>
	internal static void DeleteMyEffect( string name )
	{
		var kept = MyEffects();
		if ( kept.RemoveAll( x => x.Name == name ) > 0 )
			Game.Cookies.Set( MyEffectsCookie, kept );
	}

	/// <summary>
	/// The shapes and stickers this player made, used or forked, kept to use and change again (My shapes), in the order
	/// they were kept, each read within bounds; one that no longer reads is left out.
	/// </summary>
	internal static List<DrawnShape> MyShapes()
	{
		var kept = Game.Cookies.Get( MyShapesCookie, new List<DrawnShape>() ) ?? new List<DrawnShape>();
		return kept.Select( x => x?.Sanitized() ).Where( x => x is not null && x.Name is not null ).Take( MaxMyShapes ).ToList();
	}

	/// <summary>
	/// Keeps <paramref name="shape"/> in My shapes under its name, in place of one kept under that name. False when it has
	/// no name, or My shapes is full.
	/// </summary>
	internal static bool SaveMyShape( DrawnShape shape )
	{
		var kept = MyShapes();
		var saved = shape?.Sanitized();
		if ( saved?.Name is null )
			return false;

		var index = kept.FindIndex( x => x.Name == saved.Name );
		if ( index >= 0 )
			kept[ index ] = saved;
		else if ( kept.Count >= MaxMyShapes )
			return false;
		else
			kept.Add( saved );

		Game.Cookies.Set( MyShapesCookie, kept );
		return true;
	}

	/// <summary>Takes the shape kept as <paramref name="name"/> out of My shapes.</summary>
	internal static void DeleteMyShape( string name )
	{
		var kept = MyShapes();
		if ( kept.RemoveAll( x => x.Name == name ) > 0 )
			Game.Cookies.Set( MyShapesCookie, kept );
	}

	/// <summary>Whether the code editor is light (VS Code's Light+) rather than dark (Dark+, the default).</summary>
	internal static bool CodeLight
	{
		get => Game.Cookies.Get( CodeThemeCookie, "dark" ) == "light";
		set => Game.Cookies.Set( CodeThemeCookie, value ? "light" : "dark" );
	}

	/// <summary>Whether the code editor applies a script as it is typed (the default) rather than only on Save.</summary>
	internal static bool CodeAutoApply
	{
		get => Game.Cookies.Get( CodeAutoApplyCookie, true );
		set => Game.Cookies.Set( CodeAutoApplyCookie, value );
	}

	/// <summary>
	/// Where the code editor's window was (left, top, width, height, in the UI's units) and whether it was maximized;
	/// null when it was never moved.
	/// </summary>
	internal static (float Left, float Top, float Width, float Height, bool Maximized)? CodeWindow
	{
		get => Game.Cookies.Get<float[]>( CodeWindowCookie, null ) is { Length: 5 } kept && kept.All( float.IsFinite )
			? (kept[ 0 ], kept[ 1 ], kept[ 2 ], kept[ 3 ], kept[ 4 ] != 0f)
			: null;
		set => Game.Cookies.Set( CodeWindowCookie, value is { } window ? new[] { window.Left, window.Top, window.Width, window.Height, window.Maximized ? 1f : 0f } : null );
	}

	/// <summary>Counts up whenever a draft is kept or let go of: lists of them read them again when it changes.</summary>
	internal static int ScriptDraftsRevision { get; private set; }

	/// <summary>The scripts left as drafts, newest first (see <see cref="ScriptDraft"/>), each read within bounds.</summary>
	internal static List<ScriptDraft> ScriptDrafts()
	{
		return ScriptDraft.Within( FileSystem.Data.ReadJson<List<ScriptDraft>>( ScriptDraftsFile, null ) );
	}

	/// <summary>
	/// Keeps <paramref name="draft"/> (in place of one of its name, as edited now); gives the name of the oldest draft let
	/// go to make room, or null.
	/// </summary>
	internal static string KeepScriptDraft( ScriptDraft draft )
	{
		draft.Edited = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var kept = ScriptDraft.Keep( ScriptDrafts(), draft, out var dropped );
		FileSystem.Data.WriteJson( ScriptDraftsFile, kept );
		ScriptDraftsRevision++;
		return dropped;
	}

	/// <summary>Lets go of the draft named <paramref name="name"/> (finished, or deleted).</summary>
	internal static void DeleteScriptDraft( string name )
	{
		var kept = ScriptDrafts();
		if ( kept.RemoveAll( x => x.Name == name ) > 0 )
		{
			FileSystem.Data.WriteJson( ScriptDraftsFile, kept );
			ScriptDraftsRevision++;
		}
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
