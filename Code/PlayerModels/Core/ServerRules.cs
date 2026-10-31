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
