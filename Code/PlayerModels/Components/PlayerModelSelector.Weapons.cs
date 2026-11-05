using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Components;

// Weapon designs: the host's switches, kept with the other rules and synced from the host the same way. Nothing is taken
// from anyone's look: what isn't shown comes back once allowed. Mute, Block and a viewer's Hide cover designs too.
public sealed partial class PlayerModelSelector
{
	/// <summary>Whether the designs players other than the host gave their weapons are kept off. The host sets it on the Server page.</summary>
	[Sync( SyncFlags.FromHost )] public bool BlockWeaponDesigns { get; set; }

	/// <summary>Whether those designs' props are kept off.</summary>
	[Sync( SyncFlags.FromHost )] public bool BlockWeaponProps { get; set; }

	/// <summary>Whether those designs' effects are kept off.</summary>
	[Sync( SyncFlags.FromHost )] public bool BlockWeaponEffects { get; set; }

	/// <summary>Shows the weapon designs of players other than the host, or keeps them off. Host only.</summary>
	public void SetAllowWeaponDesigns( bool allow )
	{
		ChangeRules( rules => rules.AllowWeaponDesigns = allow );
	}

	/// <summary>Shows the props on players' weapons, or keeps them off. Host only.</summary>
	public void SetAllowWeaponProps( bool allow )
	{
		ChangeRules( rules => rules.AllowWeaponProps = allow );
	}

	/// <summary>Shows the effects of players' weapons, or keeps them off. Host only.</summary>
	public void SetAllowWeaponEffects( bool allow )
	{
		ChangeRules( rules => rules.AllowWeaponEffects = allow );
	}

	// The weapon rules as one number, for telling when what shows changes
	private int WeaponRules => (BlockWeaponDesigns ? 1 : 0) | (BlockWeaponProps ? 2 : 0) | (BlockWeaponEffects ? 4 : 0);

	// look's weapon designs as this player sees them: none from a player the host muted or this player hid, and for anyone
	// but the host none while designs are off, without props or effects while those are (effects off counts too)
	private PlayerLook WeaponsAllowed( PlayerLook look, bool isHost, Connection connection )
	{
		var isLocal = connection is null || connection == Connection.Local;
		if ( !isLocal && HidesEffectsOf( connection ) || !isHost && (BlockWeaponDesigns || IsEffectsMuted( connection )) )
			return look.WithoutWeapons();

		return isHost ? look : look.WithWeaponsAllowed( !BlockWeaponProps, !BlockWeaponEffects && !BlockEffects );
	}

	private void WeaponRulesInto( ServerRules rules )
	{
		rules.AllowWeaponDesigns = !BlockWeaponDesigns;
		rules.AllowWeaponProps = !BlockWeaponProps;
		rules.AllowWeaponEffects = !BlockWeaponEffects;
	}

	private void UseWeaponRules( ServerRules rules )
	{
		BlockWeaponDesigns = !rules.AllowWeaponDesigns;
		BlockWeaponProps = !rules.AllowWeaponProps;
		BlockWeaponEffects = !rules.AllowWeaponEffects;
	}
}
