using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

/// <summary>
/// Dresses the local player's weapon view models: the worn model's own arms when the player wants them and they can be
/// posed, otherwise the picked first-person arms, and Sandbox's own back when neither is wanted. Each view model has a
/// "Hands" renderer bone-merged onto the weapon; picked arms only change its model, the worn model's arms stand in for
/// it (see <see cref="ModelArms"/>). View models only exist for their own player, so nothing here is networked.
/// </summary>
internal sealed class ViewModelHands
{
	private const string HandsObject = "Hands";

	// The arms each hands renderer came with, to put back
	private readonly Dictionary<Guid, Model> _original = new();

	private string _pick = "";
	private Model _arms;

	// The worn model whose arms are wanted (see ArmsPlan.KeyOf), its plan once loaded, and models whose arms can't be posed
	private string _bodyKey = "";
	private ArmsPlan _plan;
	private readonly HashSet<string> _unusable = new();

	/// <summary>
	/// Wears <paramref name="pick"/> (a model, empty for Sandbox's own arms) on every view model in the scene, or the
	/// arms of the model <paramref name="body"/> wears when that isn't null and its arms can be posed.
	/// </summary>
	internal void Update( Scene scene, string pick, PlayerLook body )
	{
		pick ??= "";
		if ( pick != _pick )
		{
			_pick = pick;
			_arms = null;

			if ( pick != "" )
				_ = LoadArmsAsync( pick );
		}

		// A hot reload keeps the plan but not state new code added to it: it is made again
		var bodyKey = ArmsPlan.KeyOf( body );
		if ( bodyKey != _bodyKey || _plan is { Transfer.IsIntact: false } )
		{
			_bodyKey = bodyKey;
			_plan = null;

			if ( bodyKey != "" && !_unusable.Contains( bodyKey ) )
				_ = LoadPlanAsync( body );
		}

		foreach ( var viewModel in scene.GetAll<ViewModel>() )
		{
			// Found switched off too: the worn model's arms switch the hands off while they stand in for them
			var hands = viewModel.GameObject.Children.FirstOrDefault( x => x.Name == HandsObject )?.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelf );
			if ( !hands.IsValid() )
				continue;

			if ( !_original.ContainsKey( hands.Id ) )
				_original[ hands.Id ] = hands.Model;

			var model = _arms ?? _original[ hands.Id ];
			if ( hands.Model != model )
				hands.Model = model;

			WearModelArms( scene, hands, body );
		}
	}

	private void WearModelArms( Scene scene, SkinnedModelRenderer hands, PlayerLook body )
	{
		// A weapon whose skeleton lacks the citizen's arm bones keeps the picked arms
		var plan = _plan is not null && ModelArms.CanStandIn( hands, _plan ) ? _plan : null;

		var worn = ModelArms.On( hands );
		if ( worn.IsValid() && (plan is null || worn.Key != plan.Key) )
		{
			worn.TakeOff();
			worn = null;
		}

		if ( plan is null )
			return;

		if ( !worn.IsValid() )
		{
			using ( scene.Push() )
				worn = ModelArms.Attach( hands, plan );
		}

		worn.ApplyLook( body );
	}

	private async Task LoadArmsAsync( string pick )
	{
		var arms = await ModelLoader.LoadAsync( pick );

		// Picked something else while this loaded
		if ( pick == _pick )
			_arms = arms;
	}

	private async Task LoadPlanAsync( PlayerLook body )
	{
		var key = ArmsPlan.KeyOf( body );
		var plan = await ArmsPlan.LoadAsync( body );

		if ( plan is null )
		{
			_unusable.Add( key );
			return;
		}

		// Wore something else while this loaded
		if ( key == _bodyKey )
			_plan = plan;
	}
}
