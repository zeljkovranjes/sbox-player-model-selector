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

	// What it was last updated with, the local player and the body's dresser it follows, and what they were then: a
	// weapon swapped on the body, or a switch between first and third person, is followed the frame it happens (see
	// Place), not at the next update
	private Scene _scene;
	private PlayerLook _lastLook;
	private bool _lastExtras;
	private Player _player;
	private WeaponDresser _worn;
	private string _wornWeapon;
	private bool _thirdPerson;

	// Whether its effects show now: in first person alone
	private bool _shown;

	/// <summary>
	/// Dresses the local player's view model in <paramref name="scene"/> as <paramref name="look"/> (theirs, as shown) says,
	/// for the weapon they hold; <paramref name="extras"/> says whether props, stickers and effects show too. A few times a
	/// second, and the frame the weapon or the view changes. In third person the view model is dressed all the same,
	/// unseen, so switching to first person shows it dressed from its first frame; its effects show in first person alone.
	/// </summary>
	internal void Update( Scene scene, PlayerLook look, bool extras )
	{
		(_scene, _lastLook, _lastExtras) = (scene, look, extras);
		var player = _player = LocalPlayer( scene );
		var citizen = player?.Controller?.Renderer;
		var viewModel = ViewModelOf( scene );
		var worn = citizen.IsValid() ? Worn( citizen ) : default;
		var renderer = viewModel.IsValid() ? viewModel.Components.Get<ViewModel>()?.Renderer : null;
		_worn = worn.Dresser;
		_wornWeapon = worn.Dresser?.Weapon;
		_thirdPerson = player?.Controller?.ThirdPerson == true;

		if ( look is null || worn.Dresser?.Renderer is not { Model: { } worldModel } )
		{
			Release();
			return;
		}

		// Worked out again only when the look, the view model or the choice changes: the selector hands out the same look
		// until it does
		if ( viewModel.IsValid() && renderer.IsValid()
			&& (!ReferenceEquals( look, _look ) || viewModel != _viewModel || citizen != _citizen || extras != _extras || worn.Dresser.Weapon != _weapon) )
		{
			_look = look;
			_citizen = citizen;
			_viewModel = viewModel;
			_extras = extras;
			_weapon = worn.Dresser.Weapon;
			_held.Hold( renderer.GameObject );
			_shown = false;
			_dresser.Apply( look.Weapons, look.DrawnShapes );
			_dresser.Dress( viewModel, null, _weapon, WeaponDresser.Mapping.Between( worldModel, renderer.Model ), extras, renderer );
		}

		var shows = !_thirdPerson && viewModel.IsValid() && viewModel == _viewModel;
		if ( shows && !_shown )
			ShowEffects( player, citizen, viewModel, look, extras );
		else if ( !shows && _shown )
			HideEffects();

		// The world model's weapon effects would show a second time, from where the hidden world model is
		if ( _bodyRack != worn.Rack && _bodyRack is not null )
			_bodyRack.HidesWeaponEffects = false;

		_bodyRack = worn.Rack;
		if ( _bodyRack is not null )
			_bodyRack.HidesWeaponEffects = _shown && extras;
	}

	/// <summary>
	/// Follows a weapon swapped or a view switched this frame, places the first-person weapon's effects and colours its
	/// moving stickers. Every frame: no allocations, but on the frame something changes.
	/// </summary>
	internal void Place()
	{
		var thirdPerson = _player.IsValid() && _player.Controller.IsValid() && _player.Controller.ThirdPerson;
		if ( _scene.IsValid() && (_worn is not null && _worn.Weapon != _wornWeapon || _player.IsValid() && thirdPerson != _thirdPerson) )
			Update( _scene, _lastLook, _lastExtras );

		if ( !_viewModel.IsValid() )
			return;

		if ( _shown && _citizen.IsValid() )
			_rack?.Place( _citizen, _citizen, null, null, _held );

		_dresser.Animate();
	}

	/// <summary>Takes it all off the view model, and lets the world model show its weapon effects again.</summary>
	internal void Release()
	{
		_dresser.Release();
		HideEffects();
		if ( _bodyRack is not null )
			_bodyRack.HidesWeaponEffects = false;

		_held.Hold( null );
		_bodyRack = null;
		_viewModel = null;
		_citizen = null;
		_look = null;
		_weapon = null;
	}

	// The effects that come from the weapon, the look's and its design's, worn on the view model as it shows
	private void ShowEffects( Player player, SkinnedModelRenderer citizen, GameObject viewModel, PlayerLook look, bool extras )
	{
		_shown = true;
		_rack ??= new EffectRack();
		_rack.SizeScale = EffectsScale;
		_rack.NearCameraShown = true;
		_rack.Apply( extras ? OnWeapon( look.Effects ) : null, null, look.DrawnShapes, viewModel, null, citizen, citizen.Model, null,
			new EffectWearer { Aim = () => player.IsValid() && player.Controller.IsValid() ? player.Controller.EyeAngles.Forward : Vector3.Forward, Shooter = player.GameObject } );
		_rack.ApplyWeapon( extras ? _dresser.Design?.Effects : null );
	}

	private void HideEffects()
	{
		_shown = false;
		_rack?.Release();
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
