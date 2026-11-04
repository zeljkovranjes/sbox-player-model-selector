using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

/// <summary>
/// The local player's weapon in first person (Sandbox's view model, a mesh of its own) wearing their design of the
/// weapon they hold: its skin always, and while they want them (see <see cref="LocalPrefs.ShowWeaponInFirstPerson"/>)
/// its props and stickers, mapped onto it from the world model and hung off the bones that move each part, and the
/// effects that come from the weapon (the design's, and those of the look on the weapon held), from its muzzle, smaller,
/// as it is right by the eye. Their body's world-model weapon sends none of those meanwhile, so nothing shows twice.
/// View models exist for their own player alone: nothing here is networked. Body effects never show in first person.
/// </summary>
internal sealed class FirstPersonWeapon
{
	// Effects on a first-person weapon are this much smaller than on a body, so none covers the view
	private const float EffectsScale = 0.5f;

	private readonly WeaponDresser _dresser = new();
	private readonly HeldItems _held = new();
	private EffectRack _rack;
	private EffectRack _bodyRack;
	private SkinnedModelRenderer _citizen;
	private GameObject _viewModel;
	private PlayerLook _look;
	private string _weapon;
	private bool _extras;

	/// <summary>
	/// Dresses the local player's view model in <paramref name="scene"/> as <paramref name="look"/> (theirs, as shown) says,
	/// for the weapon they hold; <paramref name="extras"/> says whether props, stickers and effects show too. A few times a
	/// second.
	/// </summary>
	internal void Update( Scene scene, PlayerLook look, bool extras )
	{
		var player = LocalPlayer( scene );
		var citizen = player?.Controller?.Renderer;
		var viewModel = ViewModelOf( scene );
		var worn = citizen.IsValid() ? Worn( citizen ) : default;
		var renderer = viewModel.IsValid() ? viewModel.Components.Get<ViewModel>()?.Renderer : null;

		if ( look is null || !viewModel.IsValid() || !renderer.IsValid() || worn.Dresser?.Renderer is not { Model: { } worldModel } )
		{
			Release();
			return;
		}

		// Worked out again only when the look, the view model or the choice changes: the selector hands out the same look
		// until it does
		if ( !ReferenceEquals( look, _look ) || viewModel != _viewModel || citizen != _citizen || extras != _extras || worn.Dresser.Weapon != _weapon )
		{
			_look = look;
			_citizen = citizen;
			_viewModel = viewModel;
			_extras = extras;
			_weapon = worn.Dresser.Weapon;
			_held.Hold( renderer.GameObject );
			_rack ??= new EffectRack();
			_rack.SizeScale = EffectsScale;
			_rack.NearCameraShown = true;
			_rack.Apply( extras ? OnWeapon( look.Effects ) : null, null, look.DrawnShapes, viewModel, null, citizen, citizen.Model, null,
				new EffectWearer { Aim = () => player.IsValid() && player.Controller.IsValid() ? player.Controller.EyeAngles.Forward : Vector3.Forward, Shooter = player.GameObject } );

			_dresser.Apply( look.Weapons, look.DrawnShapes );
			_dresser.Dress( viewModel, _rack, _weapon, WeaponDresser.Mapping.Between( worldModel, renderer.Model ), extras, renderer );
		}

		// The world model's weapon effects would show a second time, from where the hidden world model is
		if ( _bodyRack != worn.Rack && _bodyRack is not null )
			_bodyRack.HidesWeaponEffects = false;

		_bodyRack = worn.Rack;
		if ( _bodyRack is not null )
			_bodyRack.HidesWeaponEffects = extras;
	}

	/// <summary>Places the first-person weapon's effects and colours its moving stickers. Every frame: no allocations.</summary>
	internal void Place()
	{
		if ( !_viewModel.IsValid() || !_citizen.IsValid() )
			return;

		_rack?.Place( _citizen, _citizen, null, null, _held );
		_dresser.Animate();
	}

	/// <summary>Takes it all off the view model, and lets the world model show its weapon effects again.</summary>
	internal void Release()
	{
		_dresser.Release();
		_rack?.Release();
		_held.Hold( null );
		if ( _bodyRack is not null )
			_bodyRack.HidesWeaponEffects = false;

		_bodyRack = null;
		_viewModel = null;
		_citizen = null;
		_look = null;
		_weapon = null;
	}

	// The effects of a look that come from the weapon held: the only ones that show in first person
	private static List<Effect> OnWeapon( List<Effect> effects )
	{
		var onWeapon = new List<Effect>();
		foreach ( var effect in effects )
		{
			if ( effect.Anchor?.KindIs == AnchorKind.HeldWeapon )
				onWeapon.Add( effect );
		}

		return onWeapon;
	}

	// The local player's body's dresser (which knows the world model held) and effects
	private static (WeaponDresser Dresser, EffectRack Rack) Worn( SkinnedModelRenderer citizen )
	{
		if ( ModelBody.On( citizen ) is { } body && body.IsValid() )
			return (body.Weapons, body.Effects);

		return CitizenEffects.On( citizen ) is { } worn && worn.IsValid() ? (worn.Weapons, worn.Effects) : default;
	}

	private static Player LocalPlayer( Scene scene )
	{
		foreach ( var player in scene.GetAll<Player>() )
		{
			if ( player.IsLocalPlayer )
				return player;
		}

		return null;
	}

	// The view model shows only in first person; Sandbox makes one for the weapon drawn
	private static GameObject ViewModelOf( Scene scene )
	{
		foreach ( var viewModel in scene.GetAll<ViewModel>() )
		{
			if ( viewModel.IsValid() && viewModel.GameObject.Active )
				return viewModel.GameObject;
		}

		return null;
	}
}
