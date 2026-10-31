using System.Linq;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

/// <summary>
/// The worn model's own arms on a weapon's view model, in place of its first-person hands. The hands are bone-merged
/// onto the weapon, whose skeleton has the arm bones its animation moves; the hands are switched off and the model's
/// arms are posed from the weapon every frame. Made on the local client only, never networked: view models only exist
/// for their own player.
/// </summary>
public sealed class ModelArms : Component
{
	/// <summary>The name of the object the arms are made on, under the hands they stand in for.</summary>
	internal const string ObjectName = "Player Model Arms";

	/// <summary>The view model's hands the arms stand in for.</summary>
	public SkinnedModelRenderer Hands { get; private set; }

	/// <summary>The weapon the hands are bone-merged onto, which the arms are posed from.</summary>
	public SkinnedModelRenderer Weapon { get; private set; }

	/// <summary>The model and bone picks the arms were made for (see <see cref="ArmsPlan.KeyOf"/>).</summary>
	internal string Key { get; private set; } = "";

	private SkinnedModelRenderer _renderer;
	private PoseTransfer _transfer;
	private LookPainter _painter;

	/// <summary>
	/// Whether the worn model's arms can stand in for <paramref name="hands"/>: they are bone-merged onto a weapon whose
	/// skeleton has the citizen's arm bones.
	/// </summary>
	internal static bool CanStandIn( SkinnedModelRenderer hands, ArmsPlan plan )
	{
		return hands.BoneMergeTarget is { } weapon && weapon.IsValid() && plan.Transfer.CanPoseArmsFrom( weapon.Model );
	}

	/// <summary>
	/// Puts the arms of <paramref name="plan"/>'s model on the weapon <paramref name="hands"/> are merged onto (see
	/// <see cref="CanStandIn"/>), in the scene that is active now.
	/// </summary>
	internal static ModelArms Attach( SkinnedModelRenderer hands, ArmsPlan plan )
	{
		var weapon = hands.BoneMergeTarget;

		// On the weapon itself, so the arms share its space
		var go = new GameObject( true, ObjectName );
		go.NetworkMode = NetworkMode.Never;
		go.Flags |= GameObjectFlags.NotSaved;
		go.SetParent( weapon.GameObject, false );
		go.LocalTransform = global::Transform.Zero.WithScale( plan.Transfer.ArmsScale );

		var renderer = go.AddComponent<SkinnedModelRenderer>();
		renderer.Model = plan.Model;
		renderer.UseAnimGraph = false;
		StandInMaterials.Apply( renderer );

		// Drawn the way the hands are: in a weapon view model that is over the world, without shadows
		renderer.RenderType = hands.RenderType;
		renderer.RenderOptions.Game = hands.RenderOptions.Game;
		renderer.RenderOptions.Overlay = hands.RenderOptions.Overlay;

		var arms = go.AddComponent<ModelArms>();
		arms.Hands = hands;
		arms.Weapon = weapon;
		arms.Key = plan.Key;
		arms._renderer = renderer;
		arms._transfer = plan.Transfer;
		arms._painter = new LookPainter();
		arms.HideHands();
		arms.Tick();

		return arms;
	}

	/// <summary>Finds the arms already standing in for these hands, if any.</summary>
	internal static ModelArms On( SkinnedModelRenderer hands )
	{
		if ( hands.BoneMergeTarget is not { } weapon || !weapon.IsValid() )
			return null;

		foreach ( var child in weapon.GameObject.Children )
		{
			if ( child.Components.Get<ModelArms>() is { } arms && arms.Hands == hands )
				return arms;
		}

		return null;
	}

	/// <summary>Customises the arms like the body: skin, colours, body groups, morphs and the arm sliders of the body shape.</summary>
	internal void ApplyLook( PlayerLook look )
	{
		if ( !_renderer.IsValid() || look is null )
			return;

		// The height is left out: the arms are already scaled to the citizen's
		if ( _painter.Apply( _renderer, look ) )
			_transfer.Shape( look.Shape );
	}

	/// <summary>
	/// Poses the arms the way the weapon holds them right now. Runs every frame on its own; a scene that isn't rendered
	/// by the game (a UI preview) calls it itself.
	/// </summary>
	internal void Tick()
	{
		if ( !Hands.IsValid() || !Weapon.IsValid() || Hands.BoneMergeTarget != Weapon || !_renderer.IsValid()
			|| _transfer is not { IsIntact: true } || _painter is null )
		{
			TakeOff();
			return;
		}

		_transfer.ApplyArms( Weapon, _renderer );
	}

	protected override void OnPreRender()
	{
		Tick();
	}

	/// <summary>Takes the arms off and shows the hands again, now: a UI preview's scene never gets to removing objects.</summary>
	internal void TakeOff()
	{
		ShowHands();
		GameObject.Enabled = false;
		GameObject.Destroy();
	}

	protected override void OnDestroy()
	{
		ShowHands();
	}

	// The arms follow the weapon, not the hands, so the hands can be switched off: hiding them through their render
	// options doesn't stop a view model drawing them
	private void HideHands()
	{
		Hands.Enabled = false;
	}

	private void ShowHands()
	{
		if ( !Hands.IsValid() )
			return;

		// Arms put on in place of these keep the hands off: these may be removed only at the end of the frame they were
		// taken off in, after the new ones switched the hands off
		foreach ( var sibling in GameObject.Parent?.Children ?? Enumerable.Empty<GameObject>() )
		{
			if ( sibling != GameObject && sibling.Enabled && sibling.Components.Get<ModelArms>() is { } other && other.Hands == Hands )
				return;
		}

		Hands.Enabled = true;
	}
}
