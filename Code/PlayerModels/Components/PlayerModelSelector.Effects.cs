using System;
using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

// Effects: the host's switch and budget, kept with the other rules and synced from the host the same way, and how
// much of them this player chose to see. Nothing is taken from anyone's look: what isn't shown comes back once allowed.
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
