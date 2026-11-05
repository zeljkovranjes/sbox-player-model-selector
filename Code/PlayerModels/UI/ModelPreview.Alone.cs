using System;
using System.Collections.Generic;
using PlayerModels.Components;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.UI;

// The weapon alone (see WeaponAlone): the weapon held, as the player sees it in first person: the game's own model of it,
// with its animations (a reload takes the magazine out and in, and a prop replacing it follows), dressed as the look
// designs it and wearing the effects that come from it, seen from behind and to one side. The citizen still holds it,
// unseen, so the test bar's actions tell the effects as the game does; an action the weapon has no animation for (or a
// weapon with no first-person model) moves it a little instead
public sealed partial class ModelPreview
{
	// What is in the scene but never drawn while the weapon shows alone: the citizen, its body and all it holds
	private const string UnseenTag = "pms_unseen";

	// The camera looks at the weapon from behind, to its left and a little above, this many of its sizes back
	private const float AloneDistance = 1f;

	// How quickly the camera follows the weapon as its animations move it (a share of the way each second, roughly)
	private const float AloneFollow = 3f;
	private const float AloneFieldOfView = 40f;

	// Where a target stands to shoot at: this far in front of the weapon
	private const float AloneTargetDistance = 90f;

	// The small motions: a kick for a shot or a swing, a tilt for a reload, lowered for put away and raised again
	private const float KickSeconds = 0.25f;
	private const float KickBack = 2f;
	private const float KickPitch = -8f;
	private const float ReloadSeconds = 1.2f;
	private const float ReloadDip = 3f;
	private const float ReloadRoll = 30f;
	private const float PutAwaySeconds = 0.4f;
	private const float PutAwayDrop = 10f;
	private const float PutAwayPitch = 35f;

	// The first-person model's own parameters for the actions
	private const string AttackParameter = "b_attack";
	private const string ReloadParameter = "b_reload";
	private const string HolsterParameter = "b_holster";

	[SkipHotload]
	private static readonly Vector3 AloneView = new Vector3( -0.6f, 0.75f, 0.3f ).Normal;

	/// <summary>Whether only the weapon held shows, close up and as it is in first person, with its own animations.</summary>
	internal bool WeaponAlone { get; set; }

	/// <summary>Where a target to shoot at stands while the weapon shows alone: in front of it; null otherwise.</summary>
	internal Transform? AloneTarget => _alone.IsValid()
		? new Transform( _alone.WorldPosition + _alone.WorldRotation.Forward * AloneTargetDistance, _alone.WorldRotation )
		: null;

	private GameObject _alone;
	private SkinnedModelRenderer _aloneRenderer;
	private readonly WeaponDresser _aloneDresser = new();
	private readonly HeldItems _aloneHeld = new();
	private EffectRack _aloneRack;
	private EffectWearer _aloneWearer;
	private PlayerLook _aloneLook;
	private Model _aloneFor;
	private bool _aloneAnimated;
	private Vector3 _aloneCenter;
	private float _aloneSize = 1f;
	private Vector3 _aloneAim;
	private bool _aloneAimed;
	private ScriptEvent? _nudge;
	private float _nudgeTime;

	/// <summary>
	/// Plays <paramref name="action"/> on the weapon shown alone: its own animation when its model has one (true), else
	/// a small motion (false).
	/// </summary>
	internal bool PlayAlone( ScriptEvent action )
	{
		if ( !_aloneRenderer.IsValid() )
			return false;

		// Taken out again from the start: a first-person model plays its deploy as it comes
		if ( action == ScriptEvent.Equip && _aloneAnimated )
		{
			_nudge = null;
			MakeAlone( _aloneFor );
			return true;
		}

		var parameter = action switch
		{
			ScriptEvent.Shoot or ScriptEvent.Hit => AttackParameter,
			ScriptEvent.Reload => ReloadParameter,
			ScriptEvent.Holster => HolsterParameter,
			_ => null,
		};

		if ( parameter is not null && _aloneAnimated && HasParameter( _aloneRenderer.Model, parameter ) )
		{
			_aloneRenderer.Write( parameter, true );
			return true;
		}

		_nudge = action;
		_nudgeTime = 0f;
		return false;
	}

	private static bool HasParameter( Model model, string name ) => model.HasGraph1Parameter( name );

	// Once a frame, after the citizen and what it holds: the weapon alone shown, dressed and placed, or taken away
	private void TickAlone()
	{
		_citizen.GameObject.Tags.Set( UnseenTag, WeaponAlone );
		if ( (_body.IsValid() ? _body.Effects : CitizenEffects.On( _citizen )?.Effects) is { } bodyRack )
			bodyRack.HidesWeaponEffects = WeaponAlone;

		if ( !WeaponAlone || Weapon is not { } held )
		{
			RemoveAlone();
			return;
		}

		if ( held.Model != _aloneFor || !_alone.IsValid() )
			MakeAlone( held.Model );

		if ( !ReferenceEquals( Look, _aloneLook ) )
			DressAlone();

		PlaceAlone();
		using ( _scene.RenderScene.Push() )
			_aloneRack?.Place( _citizen, _citizen, null, null, _aloneHeld );

		_aloneDresser.Animate();
	}

	private void MakeAlone( Model worldModel )
	{
		RemoveAlone();
		_aloneFor = worldModel;
		var firstPerson = FirstPersonModelOf( worldModel );
		using ( _scene.RenderScene.Push() )
		{
			_alone = new GameObject( true, "Weapon alone" );
			_alone.NetworkMode = NetworkMode.Never;
			_aloneRenderer = _alone.AddComponent<SkinnedModelRenderer>();
			_aloneRenderer.Model = firstPerson ?? worldModel;
			_aloneRenderer.UseAnimGraph = firstPerson.Graph1() is not null;

			// Props on a part hang off its bone, so they follow it through the animations
			_aloneRenderer.CreateBoneObjects = true;
		}

		_aloneAnimated = _aloneRenderer.UseAnimGraph;

		// Framed by the weapon itself, as the world model is laid onto the first-person one: that model may reach further
		var bounds = worldModel.Bounds;
		var onto = _aloneAnimated ? WeaponDresser.Mapping.Between( worldModel, _aloneRenderer.Model ).FromWorldModel : global::Transform.Zero;
		_aloneCenter = onto.PointToWorld( bounds.Center );
		_aloneSize = MathF.Max( bounds.Size.Length * onto.UniformScale, 1f );
		_aloneHeld.Hold( _alone );
		_aloneLook = null;
	}

	// Dressed as the look designs the weapon, wearing the effects that come from it (the look's and its design's)
	private void DressAlone()
	{
		_aloneLook = Look;
		if ( Look is null )
			return;

		var mapping = _aloneAnimated ? WeaponDresser.Mapping.Between( _aloneFor, _aloneRenderer.Model ) : (WeaponDresser.Mapping?)null;
		using ( _scene.RenderScene.Push() )
		{
			_aloneDresser.Apply( Look.Weapons, Look.DrawnShapes );
			_aloneDresser.Dress( _alone, null, _aloneFor.ResourcePath, mapping, true, _aloneRenderer );
			_aloneRack ??= new EffectRack { NearCameraShown = true };
			_aloneWearer ??= new EffectWearer { Aim = () => _alone.IsValid() ? _alone.WorldRotation.Forward : Vector3.Forward, Shooter = _citizen?.GameObject };
			_aloneRack.Apply( OnWeapon( Look.Effects ), null, Look.DrawnShapes, _alone, null, _citizen, _citizen.Model, null, _aloneWearer );
			_aloneRack.ApplyWeapon( _aloneDresser.Design?.Effects );
		}
	}

	// Its middle where the camera looks, turned as the preview is dragged, moved by the small motion playing
	private void PlaceAlone()
	{
		_nudgeTime += RealTime.Delta * _scene.RenderScene.TimeScale;
		var (offset, tilt) = Nudged();
		var turn = Rotation.FromYaw( _yaw - StartYaw ) * tilt;
		_alone.WorldRotation = turn;
		_alone.WorldPosition = offset - turn * _aloneCenter;
	}

	private (Vector3 Offset, Rotation Tilt) Nudged()
	{
		if ( _nudge is not { } action )
			return (Vector3.Zero, Rotation.Identity);

		switch ( action )
		{
			case ScriptEvent.Reload:
			{
				var k = _nudgeTime >= ReloadSeconds ? 0f : MathF.Sin( _nudgeTime / ReloadSeconds * MathF.PI );
				return (Vector3.Down * ReloadDip * k, Rotation.FromRoll( ReloadRoll * k ));
			}

			case ScriptEvent.Holster:
			case ScriptEvent.Equip:
			{
				var k = MathF.Min( _nudgeTime / PutAwaySeconds, 1f );
				k = action == ScriptEvent.Holster ? k : 1f - k;
				return (Vector3.Down * PutAwayDrop * k, Rotation.FromPitch( PutAwayPitch * k ));
			}

			default:
			{
				var k = MathF.Max( 0f, 1f - _nudgeTime / KickSeconds );
				return (Vector3.Backward * KickBack * k, Rotation.FromPitch( KickPitch * k ));
			}
		}
	}

	// From behind, to the left and above, far enough back for the whole weapon (the wheel zooms in)
	private bool PlaceAloneCamera()
	{
		if ( !_aloneRenderer.IsValid() )
			return false;

		// Looking at where the weapon is drawn now (its animation moves it away from where it was made), smoothly
		var drawn = _aloneRenderer.Bounds.Center;
		_aloneAim = _aloneAimed ? Vector3.Lerp( _aloneAim, drawn, 1f - MathF.Exp( -AloneFollow * RealTime.Delta ) ) : drawn;
		_aloneAimed = true;
		_camera.FovAxis = CameraComponent.Axis.Vertical;
		_camera.FieldOfView = AloneFieldOfView;
		_camera.WorldPosition = _aloneAim + AloneView * _aloneSize * AloneDistance / _zoom;
		_camera.WorldRotation = Rotation.LookAt( -AloneView );
		return true;
	}

	private void RemoveAlone()
	{
		_aloneDresser.Release();
		_aloneRack?.Release();
		_aloneHeld.Hold( null );
		if ( _alone.IsValid() )
		{
			using ( _scene.RenderScene.Push() )
				_alone.DestroyImmediate();
		}

		_alone = null;
		_aloneAimed = false;
		_aloneRenderer = null;
		_aloneFor = null;
		_aloneLook = null;
	}

	// The first-person model the game gives the weapon of this world model, read off a weapon of it in the game (the one
	// the player holds); null when there is none
	private static Model FirstPersonModelOf( Model worldModel )
	{
		if ( Game.ActiveScene is not { } scene )
			return null;

		foreach ( var weapon in scene.GetAll<BaseCombatWeapon>() )
		{
			var world = (weapon.WorldModel ?? weapon.WorldModelPrefab)?.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants )?.Model;
			if ( world?.ResourcePath != worldModel.ResourcePath )
				continue;

			if ( weapon.ViewModelPrefab?.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants )?.Model is { IsError: false } firstPerson )
				return firstPerson;
		}

		return null;
	}

	// The effects of a look that come from the weapon held: the only ones on the weapon alone
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
}
