using System.Threading.Tasks;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Whether a model can be worn, checked the way it will be shown: loaded, then mapped onto the citizen's skeleton.
/// </summary>
internal static class ModelCheck
{
	/// <summary>Why <paramref name="model"/> can't be worn, or null when it can.</summary>
	internal static async Task<string> WhyNotWearableAsync( string model )
	{
		var citizen = await ModelLoader.LoadAsync( ModelLoader.CitizenModel );
		var target = await ModelLoader.LoadAsync( model );

		if ( citizen is null )
			return "The citizen model couldn't be loaded.";

		if ( target is null )
			return "This model couldn't be loaded.";

		return PoseTransfer.TryCreate( citizen, target, out var error ) is null ? error : null;
	}
}
