using System;
using System.Collections.Generic;
using System.Linq;
using PlayerModels.Core;
using PlayerModels.Engine;
using PlayerModels.UI;
using Sandbox;

namespace PlayerModels.Components;

/// <summary>
/// The Player Model Selector spawned from Sandbox's spawn menu. While one is in the world, everyone has a Player Model
/// window in the C menu and every player's pick shows on their body for everyone. The host keeps the picks; each client
/// loads and poses the models itself. Removing it puts everyone back to the citizen.
/// </summary>
[Title( "Player Model Selector" ), Icon( "accessibility_new" )]
public sealed class PlayerModelSelector : Component
{
	// How often bodies are checked against the picks, for respawns, new ragdolls and changed picks. Short, because a
	// new ragdoll shows the citizen until the scan finds it.
	private const float ScanInterval = 0.1f;

	/// <summary>
	/// Models always offered, after the default citizen: the stock characters, or any you choose. The defaults live here
	/// rather than in the prefab: a model path the prefab names is packed into the published entity with everything it uses.
	/// </summary>
	[Property] public List<PlayerModelEntry> Models { get; set; } = new()
	{
		new PlayerModelEntry { Title = "Human Female", Model = "models/citizen_human/citizen_human_female.vmdl" },
		new PlayerModelEntry { Title = "Classic Citizen", Model = "models/citizen/citizen.vmdl" },
	};

	/// <summary>The sbox.game search the window fills itself from. Only the rigged humanoids it finds are shown.</summary>
	[Property, Group( "sbox.game" )] public string CloudSearch { get; set; } = "type:model playermodel sort:popular";

	/// <summary>How many search results the window looks at.</summary>
	[Property, Group( "sbox.game" ), Range( 0, 100 )] public int CloudCount { get; set; } = 50;

	/// <summary>
	/// A second search for Facepunch's own characters, which the player model search doesn't bring; its rigged humanoids
	/// fill the Facepunch category. Empty turns it off.
	/// </summary>
	[Property, Group( "sbox.game" )] public string FacepunchSearch { get; set; } = "type:model org:facepunch human";

	/// <summary>
	/// The searches the View Model grid fills itself from. Only first-person arms Sandbox's weapons can move are shown.
	/// </summary>
	[Property, Group( "sbox.game" )] public List<string> ViewModelSearches { get; set; } = new() { "type:model first person arms", "type:model hands" };

	/// <summary>
	/// Each player's look by connection id (a <see cref="PlayerLook"/> as text), written by the host. No entry means the
	/// default citizen.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public NetDictionary<Guid, string> Picks { get; set; } = new();

	// Blocks rather than allows: a selector kept by a hot reload gets false for state added since, which then allows

	/// <summary>Whether models from sbox.game are kept off this server. The host sets it on the Server page.</summary>
	[Sync( SyncFlags.FromHost )] public bool BlockCloudModels { get; set; }

	/// <summary>Whether models from mounted games are kept off this server. The host sets it on the Server page.</summary>
	[Sync( SyncFlags.FromHost )] public bool BlockMountedModels { get; set; }

	/// <summary>Models nobody may wear on this server, whatever else allows them. The host blocks them from the grid.</summary>
	[Sync( SyncFlags.FromHost )] public NetList<string> BlockedModels { get; set; } = new();

	/// <summary>Whether players other than the host are kept from picking bones by hand. The host sets it on the Server page.</summary>
	[Sync( SyncFlags.FromHost )] public bool BlockArmature { get; set; }

	/// <summary>
	/// Looks the host put on players in place of their own picks, by connection id (a <see cref="PlayerLook"/> as text).
	/// A player's own pick shows again once the host reverts it.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public NetDictionary<Guid, string> HostPicks { get; set; } = new();

	private readonly PlayerBodies _bodies = new();
	private readonly ViewModelHands _hands = new();
	private TimeSince _sinceScan;
	private bool _restored;

	/// <summary>The selector in this scene, or null when nobody has spawned one.</summary>
	public static PlayerModelSelector Find( Scene scene )
	{
		return scene?.GetAll<PlayerModelSelector>().FirstOrDefault( x => x.IsValid() && x.Active );
	}

	/// <summary>How this connection looks: the model the host chose for them, or the one they picked and how they customised it.</summary>
	public PlayerLook LookOf( Connection connection )
	{
		var id = KeyOf( connection );
		if ( HostPicks?.TryGetValue( id, out var chosen ) == true )
			return PlayerLook.Parse( chosen );

		return PlayerLook.Parse( Picks.TryGetValue( id, out var look ) ? look : null );
	}

	/// <summary>Whether the host chose this connection's model in place of their own pick.</summary>
	public bool IsPickedByHost( Connection connection )
	{
		return HostPicks?.ContainsKey( KeyOf( connection ) ) == true;
	}

	/// <summary>
	/// Shows <paramref name="model"/> (empty for the citizen) on <paramref name="connection"/> for everyone, in place of
	/// their own pick. Host only; <see cref="RevertPick"/> puts their own back.
	/// </summary>
	public void PickFor( Connection connection, string model )
	{
		if ( !Networking.IsHost || connection is null || !string.IsNullOrEmpty( model ) && !Offers( model ) )
			return;

		HostPicks ??= new();
		HostPicks[ KeyOf( connection ) ] = new PlayerLook { Model = model ?? "" }.Serialize();
	}

	/// <summary>Shows <paramref name="connection"/>'s own pick again in place of the one the host chose. Host only.</summary>
	public void RevertPick( Connection connection )
	{
		if ( Networking.IsHost && connection is not null )
			HostPicks?.Remove( KeyOf( connection ) );
	}

	/// <summary>The model this connection picked, or null for the default citizen.</summary>
	public string PickOf( Connection connection )
	{
		var look = LookOf( connection );
		return look.IsDefault ? null : look.Model;
	}

	/// <summary>
	/// Whether <paramref name="model"/> may be picked: one of <see cref="Models"/>, an sbox.game package or a model of a
	/// mounted game, as far as the host allows them and hasn't blocked it (the picking client checked it can be worn;
	/// every client checks again before showing it, and one without that game mounted shows the citizen).
	/// </summary>
	public bool Offers( string model )
	{
		if ( string.IsNullOrEmpty( model ) )
			return false;

		// The listed models can't be blocked, so they always are
		if ( Models.Any( x => x.Model == model ) )
			return true;

		if ( BlockedModels?.Contains( model ) == true )
			return false;

		if ( CloudModels.IsPackageIdent( model ) )
			return !BlockCloudModels;

		return MountModels.IsMountPath( model ) && !BlockMountedModels;
	}

	/// <summary>Lets players wear models from sbox.game, or stops them. Host only; anyone wearing one goes back to the citizen.</summary>
	public void SetAllowCloudModels( bool allow )
	{
		ChangeRules( rules => rules.AllowCloudModels = allow );
	}

	/// <summary>Lets players other than the host pick bones by hand, or stops them (dropping the bones they picked). Host only.</summary>
	public void SetAllowArmature( bool allow )
	{
		ChangeRules( rules => rules.AllowArmature = allow );
	}

	/// <summary>Lets players wear models from mounted games, or stops them. Host only.</summary>
	public void SetAllowMountedModels( bool allow )
	{
		ChangeRules( rules => rules.AllowMountedModels = allow );
	}

	/// <summary>Whether the host can block <paramref name="model"/>: the default citizen and the listed models never are, so there is always something to wear.</summary>
	public bool CanBlock( string model )
	{
		return !string.IsNullOrEmpty( model ) && Models.All( x => x.Model != model );
	}

	/// <summary>Blocks <paramref name="model"/> on this server, or allows it again. Host only; see <see cref="CanBlock"/>.</summary>
	public void SetBlocked( string model, bool blocked )
	{
		if ( blocked && !CanBlock( model ) )
			return;

		ChangeRules( rules =>
		{
			rules.Blocked.Remove( model );
			if ( blocked )
				rules.Blocked.Add( model );
		} );
	}

	// The host's rules are kept on their machine, so their server allows the same models next time
	private void ChangeRules( Action<ServerRules> change )
	{
		if ( !Networking.IsHost )
			return;

		var rules = LocalPrefs.ServerRules;
		change( rules );
		LocalPrefs.ServerRules = rules;
		UseRules( rules );
	}

	private void UseRules( ServerRules rules )
	{
		BlockCloudModels = !rules.AllowCloudModels;
		BlockMountedModels = !rules.AllowMountedModels;
		BlockArmature = !rules.AllowArmature;

		BlockedModels ??= new();
		BlockedModels.Clear();
		foreach ( var model in rules.Blocked.Where( x => !string.IsNullOrEmpty( x ) ).Distinct() )
			BlockedModels.Add( model );

		// Whoever wears what is no longer allowed goes back to the citizen, or to their own pick
		foreach ( var id in Picks.Keys.ToList() )
		{
			var look = PlayerLook.Parse( Picks[ id ] );
			if ( !Offers( look.Model ) )
				Picks.Remove( id );
			else if ( BlockArmature && id != KeyOf( Connection.Local ) && look.Bones.Count > 0 )
				Picks[ id ] = WithoutBones( look ).Serialize();
		}

		HostPicks ??= new();
		foreach ( var id in HostPicks.Keys.ToList() )
		{
			var model = PlayerLook.Parse( HostPicks[ id ] ).Model;
			if ( !string.IsNullOrEmpty( model ) && !Offers( model ) )
				HostPicks.Remove( id );
		}
	}

	private static PlayerLook WithoutBones( PlayerLook look )
	{
		var copy = look.Sanitized();
		copy.Bones.Clear();
		return copy;
	}

	/// <summary>
	/// Shows <paramref name="model"/> on the local player for everyone, customised the way the player left it last time.
	/// Null or empty is the default citizen.
	/// </summary>
	public void Pick( string model )
	{
		Wear( string.IsNullOrEmpty( model ) ? new PlayerLook() : LocalPrefs.LookFor( model ) );
	}

	/// <summary>Shows <paramref name="look"/> on the local player for everyone, and keeps it for next time.</summary>
	public void Wear( PlayerLook look )
	{
		LocalPrefs.Pick = look.Model;
		LocalPrefs.Remember( look );
		PickOnHost( look.Serialize() );
	}

	[Rpc.Host]
	private void PickOnHost( string text )
	{
		var id = KeyOf( Rpc.Caller );
		var look = PlayerLook.Parse( text );

		// Bones picked by hand only count from the host while the host keeps players from picking them
		if ( BlockArmature && Rpc.Caller is { IsHost: false } )
			look = WithoutBones( look );

		if ( look.IsDefault )
			Picks.Remove( id );
		else if ( Offers( look.Model ) )
			Picks[ id ] = look.Serialize();
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		// One selector is enough; a second one would only split the picks
		if ( Scene.GetAll<PlayerModelSelector>().Any( x => x != this && x.IsValid() ) )
		{
			Log.Info( "Player Model Selector: one is already in the world." );
			GameObject.Destroy();
			return;
		}

		UseRules( LocalPrefs.ServerRules );
	}

	protected override void OnUpdate()
	{
		if ( _sinceScan < ScanInterval )
			return;

		_sinceScan = 0;
		RestoreSavedPick();
		_bodies.Update( Scene, this );
		_hands.Update( Scene, LocalPrefs.ViewModel, ArmsLook() );
		PlayerModelWindow.AddTo( Scene );
	}

	// Bodies other addons show players as are looked for every frame, so each shows its player's pick from its first frame
	protected override void OnPreRender()
	{
		_bodies.ShowNewStandIns( Scene, this );
	}

	protected override void OnDestroy()
	{
		_bodies.Clear( Scene );
		_hands.Update( Scene, "", null );

		if ( Find( Scene ) is null )
			PlayerModelWindow.RemoveFrom( Scene );
	}

	/// <summary>
	/// The look whose model gives the local player's first-person arms: theirs, when they want their model's arms and it
	/// isn't a stock character (those wear Sandbox's own arms, which match). Null for the arms picked on the View Model page.
	/// </summary>
	internal PlayerLook ArmsLook()
	{
		if ( !LocalPrefs.UseModelArms )
			return null;

		var look = LookOf( Connection.Local );
		return look.IsDefault || ModelOutfit.IsStock( look.Model ) ? null : look;
	}

	// Without a lobby there is no caller and no owner: both are the local player
	private static Guid KeyOf( Connection connection )
	{
		return (connection ?? Connection.Local)?.Id ?? Guid.Empty;
	}

	// The model this player wore last time, picked again once per session when a selector appears
	private void RestoreSavedPick()
	{
		if ( _restored )
			return;

		_restored = true;

		var saved = LocalPrefs.Pick;
		if ( !string.IsNullOrEmpty( saved ) && Offers( saved ) && PickOf( Connection.Local ) != saved )
			Pick( saved );
	}
}
