namespace PlayerModels.Core;

/// <summary>A model the player favorited: shown first in the grid, even when a search doesn't find it.</summary>
public sealed class FavoriteModel
{
	/// <summary>The model (see <see cref="PlayerModelEntry.Model"/>).</summary>
	public string Model { get; set; }

	/// <summary>The name on its tile.</summary>
	public string Title { get; set; }

	/// <summary>First-person arms rather than a player model: listed in the View Model grid.</summary>
	public bool ViewModel { get; set; }
}
