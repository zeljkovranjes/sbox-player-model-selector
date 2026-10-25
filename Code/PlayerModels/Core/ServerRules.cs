using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>Which models a host lets players wear on their server. Kept on the host's machine between sessions.</summary>
public sealed class ServerRules
{
	/// <summary>Whether models from sbox.game may be worn.</summary>
	public bool AllowCloudModels { get; set; } = true;

	/// <summary>Whether models from mounted games may be worn.</summary>
	public bool AllowMountedModels { get; set; } = true;

	/// <summary>Whether players other than the host may give body parts a bone by hand on the Armature page.</summary>
	public bool AllowArmature { get; set; } = true;

	/// <summary>Models nobody may wear, whatever else allows them.</summary>
	public List<string> Blocked { get; set; } = new();
}
