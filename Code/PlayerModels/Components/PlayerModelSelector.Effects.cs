using System;
using System.Collections.Generic;
using System.Linq;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

// Effects: the host's switch and budget, kept with the other rules and synced from the host the same way, how much of
// them this player chose to see, and the effects that show on a key, which a player joining late sees for the time they
// have left. Nothing is taken from anyone's look: what isn't shown comes back once allowed.
public sealed partial class PlayerModelSelector
{
	/// <summary>
	/// Whether the effects players other than the host wear are kept off. The host sets it on the Server page; the
	/// effects stay with their looks and show again once it is off.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool BlockEffects { get; set; }

	/// <summary>How much of their effects each player shows on this server. The host sets it on the Server page.</summary>
	[Sync( SyncFlags.FromHost )] public EffectBudget.Level EffectsLevel { get; set; }

	/// <summary>The budget while <see cref="EffectsLevel"/> is <see cref="EffectBudget.Level.Custom"/>: layers, lights and sprites.</summary>
	[Sync( SyncFlags.FromHost )] public int EffectLayers { get; set; } = EffectBudget.NormalLayers;

	/// <inheritdoc cref="EffectLayers"/>
	[Sync( SyncFlags.FromHost )] public int EffectLights { get; set; } = EffectBudget.NormalLights;

	/// <inheritdoc cref="EffectLayers"/>
	[Sync( SyncFlags.FromHost )] public int EffectSprites { get; set; } = EffectBudget.NormalSprites;

	/// <summary>
	/// Whether effects from the Workshop (used or forked from a share), and Workshop shapes, are kept off for players other
	/// than the host. The host sets it on the Server page; nothing is taken from anyone's look.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool BlockWorkshopEffects { get; set; }

	/// <summary>Whether the lights of effects players other than the host wear are kept off. The host sets it on the Server page.</summary>
	[Sync( SyncFlags.FromHost )] public bool BlockEffectLights { get; set; }

	/// <summary>
	/// Whether shapes players other than the host drew show as plain ones (a circle, a glow). The host sets it on the Server
	/// page.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool BlockDrawnShapes { get; set; }

	/// <summary>Players (by Steam id) whose effects nobody sees on this server. The host mutes them on the Server page.</summary>
	[Sync( SyncFlags.FromHost )] public NetList<long> MutedEffects { get; set; } = new();

	/// <summary>
	/// Workshop shares (see <see cref="ShareOrigin.Key"/>) whose copies nobody but the host sees on this server: effects
	/// from them hidden, shapes from them plain. The host blocks them from the Workshop.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public NetList<string> BlockedShares { get; set; } = new();

	/// <summary>The budget every player's effects show within on this server, the host's too.</summary>
	public EffectBudget EffectsBudget => EffectBudget.Of( EffectsLevel, EffectLayers, EffectLights, EffectSprites );

	// What this player chose to see of effects, read from their machine once rather than each scan
	private EffectBudget.Quality? _effectsQuality;
	private HashSet<long> _hiddenEffects;

	// Host: when each player last pressed an effect key, and for which effect. One press per player per cooldown,
	// whichever of their effects it was for, so changing ids or keys gets around nothing
	private Dictionary<Guid, (string Effect, RealTimeSince Since)> _hostTriggers;

	// Host: the players who asked for the effects showing on a key when they joined; each is told once
	private HashSet<Guid> _toldShownKeys;

	// Every machine: when the host last said each player's effect was triggered, by player and effect id
	private Dictionary<(Guid Player, string Effect), RealTimeSince> _triggers;

	// This player: the keys of the effects they wear (key, effect id), read again when their look changes, and when they
	// last pressed one
	private List<(string Key, string Effect)> _ownKeys;
	private string _ownKeysFrom;
	private RealTimeSince? _sinceOwnTrigger;

	/// <summary>Shows the effects players other than the host wear, or keeps them off. Host only.</summary>
	public void SetAllowEffects( bool allow )
	{
		ChangeRules( rules => rules.AllowEffects = allow );
	}

	/// <summary>Sets the budget each player's effects show within. Host only.</summary>
	public void SetEffectsBudget( EffectBudget.Level level )
	{
		ChangeRules( rules => rules.EffectsBudget = level );
	}

	/// <summary>Sets the custom budget (see <see cref="EffectBudget.Level.Custom"/>), within <see cref="EffectBudget.Most"/>. Host only.</summary>
	public void SetCustomEffectsBudget( EffectBudget budget )
	{
		var within = budget.Within();
		ChangeRules( rules =>
		{
			rules.EffectLayers = within.Layers;
			rules.EffectLights = within.Lights;
			rules.EffectSprites = within.Sprites;
		} );
	}

	/// <summary>Lets players other than the host show effects and shapes from the Workshop, or keeps them off. Host only.</summary>
	public void SetAllowWorkshopEffects( bool allow )
	{
		ChangeRules( rules => rules.AllowWorkshopEffects = allow );
	}

	/// <summary>Lets the effects of players other than the host light up, or keeps their lights off. Host only.</summary>
	public void SetAllowEffectLights( bool allow )
	{
		ChangeRules( rules => rules.AllowEffectLights = allow );
	}

	/// <summary>Shows the shapes players other than the host drew, or plain ones in their place. Host only.</summary>
	public void SetAllowDrawnShapes( bool allow )
	{
		ChangeRules( rules => rules.AllowDrawnShapes = allow );
	}

	/// <summary>Whether the host muted <paramref name="connection"/>'s effects: nobody sees them.</summary>
	public bool IsEffectsMuted( Connection connection )
	{
		return connection is not null && Contains( MutedEffects, (long)connection.SteamId.Value );
	}

	/// <summary>Mutes <paramref name="connection"/>'s effects for everyone on this server, or lets them show again. Host only.</summary>
	public void SetEffectsMuted( Connection connection, bool muted )
	{
		if ( connection is null )
			return;

		var player = (long)connection.SteamId.Value;
		ChangeRules( rules =>
		{
			rules.MutedEffects.Remove( player );
			if ( muted )
				rules.MutedEffects.Add( player );
		} );
	}

	/// <summary>Whether the host blocked the Workshop share <paramref name="key"/> (see <see cref="ShareOrigin.Key"/>) on this server.</summary>
	public bool IsShareBlocked( string key )
	{
		return key is not null && Contains( BlockedShares, key );
	}

	/// <summary>
	/// Blocks the Workshop share <paramref name="key"/> (named <paramref name="name"/>, for the Server page to list) on this
	/// server, or allows it again. Host only.
	/// </summary>
	public void SetShareBlocked( string key, string name, bool blocked )
	{
		if ( string.IsNullOrEmpty( key ) || key.Length > MaxShareKeyLength )
			return;

		ChangeRules( rules =>
		{
			rules.BlockedShares.RemoveAll( x => x.Key == key );
			if ( blocked )
				rules.BlockedShares.Add( new BlockedShare { Key = key, Name = SharePost.Tidy( name, SharePost.MaxNameLength ) } );
		} );
	}

	// The longest share key kept: a kind and a record number
	private const int MaxShareKeyLength = 64;

	// The most players muted and shares blocked a server keeps
	private const int MaxMuted = 256;
	private const int MaxBlockedShares = 256;

	/// <summary>How much of everyone's effects this player sees, within the server's budget. Kept on their machine.</summary>
	internal EffectBudget.Quality EffectsQuality
	{
		get => _effectsQuality ??= LocalPrefs.EffectsQuality;
		set
		{
			LocalPrefs.EffectsQuality = value;
			_effectsQuality = value;
		}
	}

	/// <summary>Whether this player hid <paramref name="connection"/>'s effects (on their screen only).</summary>
	internal bool HidesEffectsOf( Connection connection )
	{
		_hiddenEffects ??= LocalPrefs.HiddenEffects();
		return connection is not null && _hiddenEffects.Contains( (long)connection.SteamId.Value );
	}

	/// <summary>Hides <paramref name="connection"/>'s effects from this player, or shows them again. Kept on their machine.</summary>
	internal void SetEffectsHidden( Connection connection, bool hidden )
	{
		if ( connection is null )
			return;

		var player = (long)connection.SteamId.Value;
		LocalPrefs.SetEffectsHidden( player, hidden );
		_hiddenEffects = LocalPrefs.HiddenEffects();
	}

	/// <summary>Seconds until this player may press an effect key again; 0 when they may now.</summary>
	internal float TriggerWait()
	{
		return _sinceOwnTrigger is { } since ? Math.Max( 0f, Effect.TriggerCooldownSeconds - since ) : 0f;
	}

	// Every frame: an effect key pressed asks the host to show that effect, at most once per cooldown
	private void PollEffectKeys()
	{
		var text = LookTextOf( KeyOf( Connection.Local ) );
		if ( text != _ownKeysFrom )
		{
			_ownKeysFrom = text;
			_ownKeys ??= new();
			_ownKeys.Clear();
			foreach ( var effect in PlayerLook.Parse( text ).Effects )
			{
				if ( effect.Key is not null && !effect.Off )
					_ownKeys.Add( (effect.Key, effect.Id) );
			}
		}

		if ( _ownKeys is not { Count: > 0 } || TriggerWait() > 0f )
			return;

		foreach ( var (key, effect) in _ownKeys )
		{
			if ( !Input.Keyboard.Pressed( key ) )
				continue;

			_sinceOwnTrigger = 0;
			TriggerOnHost( effect );
			return;
		}
	}

	// Host: shows the effect for everyone, if the caller wears it with a key and hasn't pressed one within the cooldown
	[Rpc.Host]
	private void TriggerOnHost( string effect )
	{
		if ( effect is null || effect.Length > Effect.MaxIdLength )
			return;

		var id = KeyOf( Rpc.Caller );
		_hostTriggers ??= new();
		if ( _hostTriggers.TryGetValue( id, out var last ) && last.Since < Effect.TriggerCooldownSeconds )
			return;

		if ( !LookOf( Rpc.Caller ).Effects.Exists( x => x.Id == effect && x.Key is not null && !x.Off ) )
			return;

		_hostTriggers[ id ] = (effect, 0f);
		EffectTriggered( id, effect, 0f );
	}

	// Shows player's effect with a key, already shown for elapsed seconds (a player who joined while it showed sees the
	// rest of it). Every client holds anything not from the host, or out of range, for nothing
	[Rpc.Broadcast]
	private void EffectTriggered( Guid player, string effect, float elapsed )
	{
		if ( Rpc.Caller is not { IsHost: true } || effect is null || effect.Length > Effect.MaxIdLength || !float.IsFinite( elapsed )
			|| elapsed < 0f || elapsed >= Effect.TriggerSeconds )
			return;

		_triggers ??= new();
		_triggers[ (player, effect) ] = elapsed;
	}

	// A client that just joined asks the host which effects with a key show now
	private void AskShownKeys()
	{
		ShownKeysOnHost();
	}

	// Host: tells the caller, once, of each effect with a key that shows now and how long it has shown
	[Rpc.Host]
	private void ShownKeysOnHost()
	{
		var caller = Rpc.Caller;
		_toldShownKeys ??= new();
		if ( caller is null || _hostTriggers is not { Count: > 0 } || !_toldShownKeys.Add( caller.Id ) )
			return;

		using ( Rpc.FilterInclude( x => x == caller ) )
		{
			foreach ( var (player, last) in _hostTriggers )
			{
				if ( last.Since < Effect.TriggerSeconds )
					EffectTriggered( player, last.Effect, last.Since );
			}
		}
	}

	/// <summary>
	/// Whether <paramref name="connection"/>'s effect with a key (by its id) shows now: its key was pressed within the
	/// last <see cref="Effect.TriggerSeconds"/>, as the host said. Read for every such effect every frame: no allocations.
	/// </summary>
	internal bool IsKeyShown( Connection connection, string effect )
	{
		return _triggers is { Count: > 0 } && effect is not null && _triggers.TryGetValue( (KeyOf( connection ), effect), out var since )
			&& since < Effect.TriggerSeconds;
	}

	// The budget this player sees connection's effects within: the server's, lowered to what they chose to see, and
	// none for a player whose effects they hid
	private EffectBudget ViewedBudget( Connection connection )
	{
		var budget = EffectsBudget.Min( EffectBudget.Of( EffectsQuality ) );
		var isLocal = connection is null || connection == Connection.Local;
		return !isLocal && HidesEffectsOf( connection ) ? EffectBudget.None : budget;
	}

	// What of the host's effects rules apply to connection, as one number to tell when they change: the switches, whether
	// they are muted, and which shares are blocked. Read for every player every scan: no allocations
	private int EffectsRulesFor( Connection connection )
	{
		var rules = (BlockWorkshopEffects ? 1 : 0) | (BlockEffectLights ? 2 : 0) | (BlockDrawnShapes ? 4 : 0) | (IsEffectsMuted( connection ) ? 8 : 0);
		var blocked = 0;
		for ( var i = 0; i < (BlockedShares?.Count ?? 0); i++ )
			blocked = HashCode.Combine( blocked, BlockedShares[ i ] );

		return HashCode.Combine( rules, blocked );
	}

	// look's effects as the host allows them for a player other than the host: none while they are muted, no lights while
	// lights are off, and without what came from the Workshop (or a blocked share), and drawn shapes plain, as the host says
	private PlayerLook EffectsAllowed( PlayerLook look, Connection connection, ref EffectBudget budget )
	{
		if ( IsEffectsMuted( connection ) )
			return look.WithoutEffects();

		if ( BlockEffectLights )
			budget = budget with { Lights = 0 };

		return look.WithEffectsAllowed( !BlockWorkshopEffects, !BlockDrawnShapes, IsShareBlocked );
	}

	private static bool Contains<T>( NetList<T> list, T value )
	{
		if ( list is null )
			return false;

		for ( var i = 0; i < list.Count; i++ )
		{
			if ( EqualityComparer<T>.Default.Equals( list[ i ], value ) )
				return true;
		}

		return false;
	}

	// The effects rules into rules, as the server has them now; the names of blocked shares from the host's own saved ones
	private void EffectsRulesInto( ServerRules rules )
	{
		rules.AllowWorkshopEffects = !BlockWorkshopEffects;
		rules.AllowEffectLights = !BlockEffectLights;
		rules.AllowDrawnShapes = !BlockDrawnShapes;
		rules.MutedEffects = new List<long>( MutedEffects ?? new NetList<long>() );

		var names = LocalPrefs.ServerRules.BlockedShares ?? new();
		rules.BlockedShares = (BlockedShares ?? new NetList<string>()).Select( key => names.Find( x => x?.Key == key ) ?? new BlockedShare { Key = key } ).ToList();
	}

	// The server's effects rules from saved ones, each within its range
	private void UseEffectsRules( ServerRules rules )
	{
		BlockEffects = !rules.AllowEffects;
		EffectsLevel = Enum.IsDefined( typeof( EffectBudget.Level ), rules.EffectsBudget ) ? rules.EffectsBudget : EffectBudget.Level.Normal;

		var custom = new EffectBudget( rules.EffectLayers, rules.EffectLights, rules.EffectSprites ).Within();
		EffectLayers = custom.Layers;
		EffectLights = custom.Lights;
		EffectSprites = custom.Sprites;

		BlockWorkshopEffects = !rules.AllowWorkshopEffects;
		BlockEffectLights = !rules.AllowEffectLights;
		BlockDrawnShapes = !rules.AllowDrawnShapes;

		MutedEffects ??= new();
		MutedEffects.Clear();
		foreach ( var player in (rules.MutedEffects ?? new()).Distinct().Take( MaxMuted ) )
			MutedEffects.Add( player );

		BlockedShares ??= new();
		BlockedShares.Clear();
		foreach ( var key in (rules.BlockedShares ?? new()).Select( x => x?.Key ).Where( x => !string.IsNullOrEmpty( x ) && x.Length <= MaxShareKeyLength ).Distinct().Take( MaxBlockedShares ) )
			BlockedShares.Add( key );
	}
}
