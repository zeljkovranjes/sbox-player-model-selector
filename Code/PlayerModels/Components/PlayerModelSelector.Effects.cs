using System;
using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

// Effects: the host's switch and budget, kept with the other rules and synced from the host the same way, how much of
// them this player chose to see, and the effects that show on a key. Nothing is taken from anyone's look: what isn't
// shown comes back once allowed.
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

	/// <summary>The budget every player's effects show within on this server, the host's too.</summary>
	public EffectBudget EffectsBudget => EffectBudget.Of( EffectsLevel, EffectLayers, EffectLights, EffectSprites );

	// What this player chose to see of effects, read from their machine once rather than each scan
	private EffectBudget.Quality? _effectsQuality;
	private HashSet<long> _hiddenEffects;

	// Host: when each player last pressed an effect key. One press per player per cooldown, whichever of their
	// effects it was for, so changing ids or keys gets around nothing
	private Dictionary<Guid, RealTimeSince> _hostTriggers;

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
		if ( _hostTriggers.TryGetValue( id, out var since ) && since < Effect.TriggerCooldownSeconds )
			return;

		if ( !LookOf( Rpc.Caller ).Effects.Exists( x => x.Id == effect && x.Key is not null && !x.Off ) )
			return;

		_hostTriggers[ id ] = 0;
		EffectTriggered( id, effect );
	}

	[Rpc.Broadcast]
	private void EffectTriggered( Guid player, string effect )
	{
		if ( Rpc.Caller is not { IsHost: true } || effect is null || effect.Length > Effect.MaxIdLength )
			return;

		_triggers ??= new();
		_triggers[ (player, effect) ] = 0;
	}

	// Which of the look's effects (a bit each, by place) were triggered within the last TriggerSeconds. Runs for every
	// player every scan: no allocations
	private int TriggeredOf( Guid player, PlayerLook look )
	{
		if ( _triggers is not { Count: > 0 } )
			return 0;

		var triggered = 0;
		for ( var i = 0; i < look.Effects.Count; i++ )
		{
			if ( look.Effects[ i ].Key is not null && _triggers.TryGetValue( (player, look.Effects[ i ].Id), out var since )
				&& since < Effect.TriggerSeconds )
				triggered |= 1 << i;
		}

		return triggered;
	}

	// The budget this player sees connection's effects within: the server's, lowered to what they chose to see, and
	// none for a player whose effects they hid
	private EffectBudget ViewedBudget( Connection connection )
	{
		var budget = EffectsBudget.Min( EffectBudget.Of( EffectsQuality ) );
		var isLocal = connection is null || connection == Connection.Local;
		return !isLocal && HidesEffectsOf( connection ) ? EffectBudget.None : budget;
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
	}
}
