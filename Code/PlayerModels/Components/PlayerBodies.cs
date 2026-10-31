using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

/// <summary>
/// Keeps every body in the scene showing its owner's pick: living players, the ragdolls Sandbox makes when one dies,
/// and the bodies other addons show players as. Each client does this for itself from the picks the host shares.
/// </summary>
internal sealed class PlayerBodies
{
	/// <summary>
	/// The tag another addon gives a body of its own that shows a player in their place (a skateboard's rider, while they
	/// ride). It shows the pick of the player owning the network object it is part of, dressed as their own body is. Only
	/// the object with the body's renderer counts: its clothes have the tag through it.
	/// </summary>
	internal const string StandInTag = "player_body";

	private readonly HashSet<SkinnedModelRenderer> _loading = new();

	// Picks that failed once (missing, not a humanoid), so they aren't loaded again every scan; by model and bone picks,
	// since picking the right bones by hand can make a model usable
	private readonly HashSet<string> _unusable = new();

	private static string UsableKey( string model, string bones ) => $"{model}|{bones}";

	// The models picks loaded, so a body put on for a model already worn goes on the moment it is found. Both made where
	// used: a hot reload keeps this object but not fields added since
	private Dictionary<string, Model> _models;
	private List<SkinnedModelRenderer> _renderers;

	/// <summary>Puts each body in step with its owner's pick in <paramref name="selector"/>.</summary>
	internal void Update( Scene scene, PlayerModelSelector selector )
	{
		foreach ( var player in scene.GetAll<Player>() )
		{
			if ( player.Controller.IsValid() && player.Controller.Renderer.IsValid() )
				Show( player.Controller.Renderer, selector.ShownLookOf( player.Network.Owner ), player.Network.Owner );
		}

		// Sandbox's ragdolls are made on every client by a broadcast, each with the dead player's connection
		foreach ( var corpse in scene.GetAll<DeathCameraTarget>() )
		{
			if ( corpse.Components.Get<SkinnedModelRenderer>() is { } renderer )
				Show( renderer, selector.ShownLookOf( corpse.Connection ), corpse.Connection );
		}

		ShowStandIns( scene, selector, false );
	}

	/// <summary>
	/// Puts its player's pick on each stand-in (see <see cref="StandInTag"/>) not showing one yet. Every frame: a stand-in
	/// takes its player's place the frame it shows, and would show the citizen it is made of until the next scan.
	/// </summary>
	internal void ShowNewStandIns( Scene scene, PlayerModelSelector selector )
	{
		ShowStandIns( scene, selector, true );
	}

	// A stand-in is made on each client like the body it stands in for, under a network object its player owns (nobody
	// without a lobby: the local player)
	private void ShowStandIns( Scene scene, PlayerModelSelector selector, bool newOnly )
	{
		_renderers ??= new();
		_renderers.Clear();
		scene.GetAll( _renderers );

		foreach ( var body in _renderers )
		{
			if ( !body.IsValid() || !body.GameObject.Tags.Has( StandInTag, false ) || newOnly && ModelBody.On( body ).IsValid() )
				continue;

			// One that isn't networked in a lobby belongs to nobody we know
			var owner = body.GameObject.Network.Owner;
			if ( owner is null && Networking.IsActive )
				continue;

			Show( body, selector.ShownLookOf( owner ), owner, PlayerBodyOf( scene, owner ) );
		}
	}

	// The body of the player a stand-in shows. A stock character on the stand-in is dressed from it: the stand-in's own
	// clothes are copies, and a dresser on it needn't know the outfit
	private static SkinnedModelRenderer PlayerBodyOf( Scene scene, Connection owner )
	{
		var id = owner?.Id ?? Guid.Empty;
		foreach ( var player in scene.GetAll<Player>() )
		{
			if ( player.Network.OwnerId == id && player.Controller.IsValid() && player.Controller.Renderer.IsValid() )
				return player.Controller.Renderer;
		}

		return null;
	}

	/// <summary>Takes every picked model off, showing the citizens again.</summary>
	internal void Clear( Scene scene )
	{
		foreach ( var body in scene.GetAll<ModelBody>() )
			body.TakeOff();

		_loading.Clear();
	}

	// dressedAs is the body whose outfit a stock character wears: the citizen itself when null
	private void Show( SkinnedModelRenderer citizen, PlayerLook look, Connection owner, SkinnedModelRenderer dressedAs = null )
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
			_ = ShowAsync( citizen, look, owner, dressedAs ?? citizen );
	}

	private async Task ShowAsync( SkinnedModelRenderer citizen, PlayerLook look, Connection owner, SkinnedModelRenderer dressedAs )
	{
		var pick = look.Model;
		var bones = look.BonesKey();

		try
		{
			// A model already loaded goes on now, not a frame or more later once loading it again returns
			_models ??= new();
			var model = _models.TryGetValue( pick, out var loaded ) && loaded is { IsValid: true } ? loaded : await ModelLoader.LoadAsync( pick );
			if ( !citizen.IsValid() )
				return;

			if ( model is null )
			{
				Log.Warning( $"Player Model Selector: couldn't load the model '{pick}'." );
				_unusable.Add( UsableKey( pick, bones ) );
				return;
			}

			_models[ pick ] = model;

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
			var dressed = dressedAs.IsValid() ? dressedAs : citizen;
			var outfit = ModelOutfit.IsStock( pick ) ? ModelOutfit.For( pick, ModelOutfit.OutfitOf( dressed, owner ) ) : null;

			using ( citizen.Scene.Push() )
				ModelBody.Attach( citizen, pick, bones, model, transfer, outfit );
		}
		finally
		{
			_loading.Remove( citizen );
		}
	}
}
