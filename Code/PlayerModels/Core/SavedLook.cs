namespace PlayerModels.Core;

/// <summary>A look the player saved under a name, to put on again later. Kept per model on the player's machine.</summary>
public sealed class SavedLook
{
	/// <summary>The name the player gave it.</summary>
	public string Name { get; set; } = "";

	/// <summary>The look, as <see cref="PlayerLook.Serialize"/> wrote it.</summary>
	public string Look { get; set; } = "";
}
