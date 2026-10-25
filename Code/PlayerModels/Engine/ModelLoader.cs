using System;
using System.Threading.Tasks;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Loads a model a <see cref="Core.PlayerModelEntry"/> names: a path that ships with the game, a path into a mounted
/// game, or an sbox.game ident.
/// </summary>
internal static class ModelLoader
{
	/// <summary>The citizen Sandbox's players are made of, and the pose source for every picked model.</summary>
	internal const string CitizenModel = "models/citizen_human/citizen_human_male.vmdl";

	/// <summary>The model, or null if it doesn't exist or failed to load.</summary>
	internal static async Task<Model> LoadAsync( string model )
	{
		if ( string.IsNullOrWhiteSpace( model ) )
			return null;

		Model loaded;
		if ( MountModels.IsMountPath( model ) )
			loaded = await ResourceLibrary.LoadAsync<Model>( model );
		else if ( model.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) )
			loaded = await Model.LoadAsync( model );
		else
			loaded = await Cloud.Load<Model>( model );

		return loaded is { IsValid: true, IsError: false } ? loaded : null;
	}
}
