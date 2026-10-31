using System;
using System.Threading.Tasks;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Whether a model can be worn, checked the way it will be shown: loaded, big enough to be a body, then mapped onto the
/// citizen's skeleton.
/// </summary>
internal static class ModelCheck
{
	/// <summary>
	/// The smallest a body can be along its longest side, in inches (about 1.2 m): a shirt, a cap or a pair of trousers
	/// rigged to the citizen's skeleton is smaller, a character standing head to feet isn't.
	/// </summary>
	internal const float MinBodySize = 48f;

	/// <summary>Whether something <paramref name="size"/> across (its bounds) is big enough to be a body.</summary>
	internal static bool IsBodySized( Vector3 size )
	{
		return Math.Max( size.x, Math.Max( size.y, size.z ) ) >= MinBodySize;
	}

	/// <summary>Why <paramref name="model"/> can't be worn, or null when it can.</summary>
	internal static async Task<string> WhyNotWearableAsync( string model )
	{
		var citizen = await ModelLoader.LoadAsync( ModelLoader.CitizenModel );
		var target = await ModelLoader.LoadAsync( model );

		if ( citizen is null )
			return "The citizen model couldn't be loaded.";

		if ( target is null )
			return "This model couldn't be loaded.";

		if ( !IsBodySized( target.Bounds.Size ) )
			return $"It is under {MinBodySize} units across, too small to be a body: a part or something worn.";

		return PoseTransfer.TryCreate( citizen, target, out var error ) is null ? error : null;
	}
}
