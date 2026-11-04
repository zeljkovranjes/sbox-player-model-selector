using System;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Components;

// Effects: the host's switch and budget, kept with the other rules and synced from the host the same way. Nothing is
// taken from anyone's look: what isn't shown comes back once allowed.
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
