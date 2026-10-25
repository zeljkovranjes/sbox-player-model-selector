using System.Collections.Generic;
using System.Threading.Tasks;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

/// <summary>
/// Keeps every body in the scene showing its owner's pick: living players, and the ragdolls Sandbox makes when one
/// dies. Each client does this for itself from the picks the host shares.
/// </summary>
internal sealed class PlayerBodies
{
	private readonly HashSet<SkinnedModelRenderer> _loading = new();

	// Picks that failed once (missing, not a humanoid), so they aren't loaded again every scan; by model and bone picks,
	// since picking the right bones by hand can make a model usable
	private readonly HashSet<string> _unusable = new();

	private static string UsableKey( string model, string bones ) => $"{model}|{bones}";

	/// <summary>Puts each body in step with its owner's pick in <paramref name="selector"/>.</summary>
	internal void Update( Scene scene, PlayerModelSelector selector )
	{
		foreach ( var player in scene.GetAll<Player>() )
		{
			if ( player.Controller.IsValid() && player.Controller.Renderer.IsValid() )
				Show( player.Controller.Renderer, selector.LookOf( player.Network.Owner ), player.Network.Owner );
		}

		// Sandbox's ragdolls are made on every client by a broadcast, each with the dead player's connection
		foreach ( var corpse in scene.GetAll<DeathCameraTarget>() )
		{
			if ( corpse.Components.Get<SkinnedModelRenderer>() is { } renderer )
				Show( renderer, selector.LookOf( corpse.Connection ), corpse.Connection );
		}
	}

	/// <summary>Takes every picked model off, showing the citizens again.</summary>
	internal void Clear( Scene scene )
	{
		foreach ( var body in scene.GetAll<ModelBody>() )
			body.TakeOff();

		_loading.Clear();
	}

	private void Show( SkinnedModelRenderer citizen, PlayerLook look, Connection owner )
	{
		var body = ModelBody.On( citizen );
		var pick = look.Model;
		var bones = look.BonesKey();

		if ( string.IsNullOrEmpty( pick ) || _unusable.Contains( UsableKey( pick, bones ) ) )
		{
			if ( body.IsValid() )
				body.TakeOff();

			return;
		}

		// The same model posed the same way only needs the rest of the look; bones picked differently need a new pose plan
		if ( body.IsValid() && body.ModelId == pick && body.BonesKey == bones )
		{
			body.ApplyLook( look );
			return;
		}

		if ( _loading.Add( citizen ) )
			_ = ShowAsync( citizen, look, owner );
	}

	private async Task ShowAsync( SkinnedModelRenderer citizen, PlayerLook look, Connection owner )
	{
		var pick = look.Model;
		var bones = look.BonesKey();

		try
		{
			var model = await ModelLoader.LoadAsync( pick );
			if ( !citizen.IsValid() )
				return;

			if ( model is null )
			{
				Log.Warning( $"Player Model Selector: couldn't load the model '{pick}'." );
				_unusable.Add( UsableKey( pick, bones ) );
				return;
			}

			var transfer = PoseTransfer.TryCreate( citizen.Model, model, look.Bones, out var error );
			if ( transfer is null )
			{
				Log.Warning( $"Player Model Selector: '{pick}' can't be worn. {error}" );
				_unusable.Add( UsableKey( pick, bones ) );
				return;
			}

			// Looked up after loading: a body may have been put on meanwhile
			var old = ModelBody.On( citizen );
			if ( old.IsValid() )
				old.TakeOff();

			// Stock characters wear the player's own outfit; anything else keeps its own look
			var outfit = ModelOutfit.IsStock( pick ) ? ModelOutfit.For( pick, ModelOutfit.OutfitOf( citizen, owner ) ) : null;

			using ( citizen.Scene.Push() )
				ModelBody.Attach( citizen, pick, bones, model, transfer, outfit );
		}
		finally
		{
			_loading.Remove( citizen );
		}
	}
}
