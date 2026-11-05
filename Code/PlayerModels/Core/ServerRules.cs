using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>Which models a host lets players wear on their server. Kept on the host's machine between sessions.</summary>
public sealed class ServerRules
{
	/// <summary>Whether models from sbox.game may be worn.</summary>
	public bool AllowCloudModels { get; set; } = true;

	/// <summary>Whether models from mounted games may be worn.</summary>
	public bool AllowMountedModels { get; set; } = true;

	/// <summary>
	/// Whether players other than the host may use the folder button, which puts a mounted game's folders first in its
	/// group. Off until the host turns it on.
	/// </summary>
	public bool AllowModelFolders { get; set; }

	/// <summary>Whether the body groups players other than the host choose are shown.</summary>
	public bool AllowBodyGroups { get; set; } = true;

	/// <summary>Whether players other than the host may give body parts a bone by hand on the Armature page.</summary>
	public bool AllowArmature { get; set; } = true;

	/// <summary>Whether players other than the host may wear items (glasses, hats) on their models.</summary>
	public bool AllowAccessories { get; set; } = true;

	/// <summary>Whether the effects players other than the host wear (glowing eyes, a halo) are shown.</summary>
	public bool AllowEffects { get; set; } = true;

	/// <summary>How much of their effects each player shows (see <see cref="EffectBudget"/>).</summary>
	public EffectBudget.Level EffectsBudget { get; set; } = EffectBudget.Level.Normal;

	/// <summary>Whether effects from the Workshop (used or forked from a share) show, and the shapes from it.</summary>
	public bool AllowWorkshopEffects { get; set; } = true;

	/// <summary>Whether effects' lights show.</summary>
	public bool AllowEffectLights { get; set; } = true;

	/// <summary>Whether shapes players drew show; off, only the standard shapes do.</summary>
	public bool AllowDrawnShapes { get; set; } = true;

	/// <summary>Whether the designs players other than the host gave their weapons show.</summary>
	public bool AllowWeaponDesigns { get; set; } = true;

	/// <summary>Whether those designs' props show.</summary>
	public bool AllowWeaponProps { get; set; } = true;

	/// <summary>Whether those designs' effects show.</summary>
	public bool AllowWeaponEffects { get; set; } = true;

	/// <summary>
	/// Whether effects run their scripts (see <see cref="Effect.Script"/>); off, each shows what its script built when it
	/// was saved, with nothing of it running.
	/// </summary>
	public bool AllowScripts { get; set; } = true;

	/// <summary>
	/// The most a script runs at, in UTF-8 bytes minified: at most <see cref="Script.ScriptLimits.MaxScriptBytes"/>, which
	/// it can only lower. A longer one shows what it built, as with scripts off.
	/// </summary>
	public int MaxScriptBytes { get; set; } = Script.ScriptLimits.MaxScriptBytes;

	/// <summary>Players (by Steam id) whose effects nobody sees here.</summary>
	public List<long> MutedEffects { get; set; } = new();

	/// <summary>Workshop shares (see <see cref="ShareOrigin.Key"/>) whose copies nobody sees here, with their names.</summary>
	public List<BlockedShare> BlockedShares { get; set; } = new();

	/// <summary>The budget when <see cref="EffectsBudget"/> is <see cref="EffectBudget.Level.Custom"/>: layers, lights and sprites.</summary>
	public int EffectLayers { get; set; } = EffectBudget.NormalLayers;
	public int EffectLights { get; set; } = EffectBudget.NormalLights;
	public int EffectSprites { get; set; } = EffectBudget.NormalSprites;

	/// <summary>
	/// How many sbox.game models new to the server each player other than the host may put on per
	/// <see cref="DownloadMinutes"/>: everyone here downloads each one.
	/// </summary>
	public int DownloadLimit { get; set; } = DefaultDownloadLimit;

	/// <summary>The minutes <see cref="DownloadLimit"/> counts over.</summary>
	public int DownloadMinutes { get; set; } = DefaultDownloadMinutes;

	/// <summary>What <see cref="DownloadLimit"/> and <see cref="DownloadMinutes"/> start at.</summary>
	public const int DefaultDownloadLimit = 3;
	public const int DefaultDownloadMinutes = 5;

	/// <summary>The most either can be set to.</summary>
	public const int MaxDownloadLimit = 50;
	public const int MaxDownloadMinutes = 60;

	/// <summary>Mounted games nobody may wear models from, by mount ident ("deadlock").</summary>
	public List<string> BlockedMounts { get; set; } = new();

	/// <summary>Models nobody may wear, whatever else allows them.</summary>
	public List<string> Blocked { get; set; } = new();
}

/// <summary>A Workshop share a host blocked: which it is (see <see cref="ShareOrigin.Key"/>), and its name to list it by.</summary>
public sealed class BlockedShare
{
	public string Key { get; set; } = "";
	public string Name { get; set; } = "";
}
