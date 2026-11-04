using System;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

/// <summary>
/// The effects a citizen wears when its player picked no model: the default citizen's look carries effects and nothing
/// else (see <see cref="PlayerLook.EffectsAlone"/>), shown on the citizen's own bones. Made on each client for each body,
/// never networked, like <see cref="ModelBody"/>, and taken off when a model goes on in its place.
/// </summary>
public sealed class CitizenEffects : Component
{
	// How often what the citizen holds is looked for again, for effects on the weapon held
	private const float FindInterval = 0.5f;

	/// <summary>The name of the object they are worn on, under the citizen.</summary>
	internal const string ObjectName = "Citizen Effects";

	/// <summary>The citizen renderer wearing them.</summary>
	public SkinnedModelRenderer Source { get; private set; }

	/// <summary>The effects worn; null before a look is first put on.</summary>
	internal EffectRack Effects => _effects;

	/// <summary>Dresses the weapon held as its design says; null before a look is first put on.</summary>
	internal WeaponDresser Weapons => _weapons;

	/// <summary>The look put on last, the very object the selector handed out, so a citizen already wearing it is passed over.</summary>
	internal PlayerLook ShownLook { get; set; }

	private EffectRack _effects;
	private HeldItems _held;
	private WeaponDresser _weapons;
	private TimeSince _sinceFound;
	private TimeSince _sincePlaced;

	/// <summary>The effects worn on <paramref name="source"/>, if any.</summary>
	internal static CitizenEffects On( SkinnedModelRenderer source )
	{
		foreach ( var child in source.GameObject.Children )
		{
			if ( child.Components.Get<CitizenEffects>() is { } worn && worn.Source == source )
				return worn;
		}

		return null;
	}

	/// <summary>
	/// Puts <paramref name="look"/>'s effects on <paramref name="source"/>, in the scene that is active now; see
	/// <see cref="ModelBody.ApplyLook"/> for <paramref name="keyShown"/> and <paramref name="wearer"/>.
	/// </summary>
	internal static CitizenEffects Wear( SkinnedModelRenderer source, PlayerLook look, Func<string, bool> keyShown, EffectWearer wearer )
	{
		var worn = On( source );
		if ( !worn.IsValid() )
		{
			var go = new GameObject( true, ObjectName );
			go.NetworkMode = NetworkMode.Never;
			go.Flags |= GameObjectFlags.NotSaved;
			go.SetParent( source.GameObject, false );
			go.LocalTransform = global::Transform.Zero;

			worn = go.AddComponent<CitizenEffects>();
			worn.Source = source;
		}

		worn.Apply( look, keyShown, wearer );
		return worn;
	}

	/// <summary>Wears <paramref name="look"/>'s effects in place of what it wore.</summary>
	internal void Apply( PlayerLook look, Func<string, bool> keyShown = null, EffectWearer wearer = null )
	{
		_effects ??= new EffectRack();
		_held ??= new HeldItems();
		_effects.Apply( look?.Effects, null, look?.DrawnShapes, GameObject, null, Source, Source.Model, keyShown, wearer );
		_weapons ??= new WeaponDresser();
		_weapons.Apply( look?.Weapons, look?.DrawnShapes );
		_weapons.Dress( _held.Holding, _effects );
		ShownLook = look;
	}

	/// <summary>Takes the effects off now, giving their objects back to the scene.</summary>
	internal void TakeOff()
	{
		_weapons?.Release();
		_effects?.Release();
		GameObject.Enabled = false;
		GameObject.Destroy();
	}

	/// <summary>Places the effects on the citizen as it is posed now. Runs every frame on its own; a preview calls it itself.</summary>
	internal void Tick()
	{
		if ( !Source.IsValid() )
		{
			TakeOff();
			return;
		}

		if ( _sinceFound > FindInterval )
		{
			_sinceFound = 0;
			_held?.Find( Source, null, GameObject );
			_weapons?.Dress( _held?.Holding, _effects );
		}

		_effects?.Place( Source, Source, null, null, _held );
		_weapons?.Animate();
	}

	// Bodies far from the camera or out of view are placed less often (see PoseSchedule)
	protected override void OnPreRender()
	{
		var interval = Source.IsValid() ? Scene.GetSystem<PoseSchedule>()?.IntervalFor( Source.Bounds ) ?? 0f : 0f;
		if ( _sincePlaced < interval )
			return;

		_sincePlaced = 0;
		Tick();
	}

	protected override void OnDestroy()
	{
		_weapons?.Release();
		_effects?.Release();
	}
}
