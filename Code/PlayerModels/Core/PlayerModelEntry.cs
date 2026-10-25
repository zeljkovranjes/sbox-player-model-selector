namespace PlayerModels.Core;

/// <summary>
/// A model players can pick in the Model window: the name on its tile and the model to load.
/// </summary>
public sealed class PlayerModelEntry
{
	/// <summary>The name shown on the tile.</summary>
	public string Title { get; set; }

	/// <summary>
	/// A model path that ships with the game (<c>models/citizen_human/citizen_human_female.vmdl</c>)
	/// or an sbox.game package ident (<c>org.model</c>). It has to be a rigged humanoid.
	/// </summary>
	public string Model { get; set; }
}
