using System;
using System.Collections.Generic;
using System.Text.Json;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The effects a body wears (see <see cref="Effect"/>), each layer on an object of its own under the body (see
/// <see cref="EffectLayerObject"/>). Built only when the effects change, from objects the scene keeps for reuse, and
/// only as many a frame as <see cref="EffectSchedule"/> lets through, so a crowd joining at once doesn't stall a frame;
/// how much of them shows is the schedule's to say too. Every frame each layer is placed on its joint (see
/// <see cref="EffectJoints"/>) without allocating.
/// </summary>
internal sealed class EffectRack
{
	// The share of sprites shown changes in steps this fine: smaller changes aren't worth starting the emitters again
	private const float ShareSteps = 20f;

	private readonly List<EffectLayerObject> _layers = new();
	private IReadOnlyList<Effect> _wanted = Array.Empty<Effect>();
	private string _key = "";
	private bool _pending;
	private GameObject _parent;
	private EffectSchedule _schedule;
	private EffectJoints _joints;

	// What this client shows of it (see EffectSchedule): the part of its sprites, how many lights, and whether it is
	// paused out of view
	private float _share = 1f;
	private int _lights = int.MaxValue;
	private bool _paused;

	/// <summary>How many sprites it has out at once, all shown; and how many lights it has.</summary>
	internal float WantedSprites { get; private set; }
	internal int WantedLights { get; private set; }

	/// <summary>Whether it shows anything.</summary>
	internal bool IsWorn => _layers.Count > 0;

	/// <summary>How many layers it shows now.</summary>
	internal int LayerCount => _layers.Count;

	/// <summary>
	/// Wears <paramref name="effects"/> on <paramref name="parent"/>, in place of what it wore. They are built on a later
	/// <see cref="Place"/>, once the schedule lets them through; the same effects again change nothing.
	/// </summary>
	internal void Apply( IReadOnlyList<Effect> effects, GameObject parent, PoseTransfer transfer, Model citizen, Model model )
	{
		_wanted = effects ?? Array.Empty<Effect>();
		_parent = parent;

		var key = _wanted.Count == 0 ? "" : JsonSerializer.Serialize( _wanted );
		if ( key == _key )
			return;

		_key = key;
		_joints ??= new EffectJoints( transfer, citizen, model );
		_pending = true;
	}

	/// <summary>
	/// Places every layer on its joint and beats its rhythm, after the model is posed by <paramref name="transfer"/>; and
	/// builds the effects first when they changed and the schedule lets them through. Runs every frame the body is posed.
	/// </summary>
	internal void Place( SkinnedModelRenderer citizen, SkinnedModelRenderer model, PoseTransfer transfer )
	{
		if ( _pending && _parent.IsValid() && (Schedule()?.TryBuild( Count( _wanted ) ) ?? true) )
			Build( model );

		if ( _layers.Count == 0 )
			return;

		_joints.NextFrame();
		var now = RealTime.Now;
		for ( var i = 0; i < _layers.Count; i++ )
		{
			var layer = _layers[ i ];
			if ( layer.Root.IsValid() && _joints.TryGet( layer.Joint, citizen, model, transfer, out var joint ) )
				layer.Place( joint, now );
		}
	}

	/// <summary>
	/// Shows <paramref name="share"/> of its sprites (0 to 1), at most <paramref name="lights"/> of its lights, and
	/// pauses it while <paramref name="paused"/>. Changes too small to see are let go.
	/// </summary>
	internal void UseShare( float share, int lights, bool paused )
	{
		var stepped = MathF.Round( Math.Clamp( share, 0f, 1f ) * ShareSteps ) / ShareSteps;
		if ( stepped == _share && lights == _lights && paused == _paused )
			return;

		var rates = stepped != _share;
		_share = stepped;
		_lights = lights;
		_paused = paused;
		ApplyShare( rates );
	}

	/// <summary>Gives every layer back to the scene for reuse; nothing shows until effects are applied again.</summary>
	internal void Release()
	{
		ReturnLayers();
		_key = "";
		_pending = false;
	}

	private EffectSchedule Schedule()
	{
		if ( _schedule is null && _parent.IsValid() )
			_schedule = _parent.Scene.GetSystem<EffectSchedule>();

		return _schedule;
	}

	private static int Count( IReadOnlyList<Effect> effects )
	{
		var layers = 0;
		foreach ( var effect in effects )
			layers += effect.Off ? 0 : effect.Layers.Count;

		return layers;
	}

	private void ReturnLayers()
	{
		var schedule = Schedule();
		foreach ( var layer in _layers )
		{
			if ( schedule is not null )
				schedule.Return( layer );
			else if ( layer.Root.IsValid() )
				layer.Root.Destroy();
		}

		_layers.Clear();
		WantedSprites = 0f;
		WantedLights = 0;
	}

	private void Build( SkinnedModelRenderer model )
	{
		_pending = false;
		ReturnLayers();

		var schedule = Schedule();
		var motion = _joints.BodyScale * model.WorldScale.x;
		var index = 0;

		using ( _parent.Scene.Push() )
		{
			foreach ( var effect in _wanted )
			{
				if ( effect.Off )
					continue;

				foreach ( var data in effect.Layers )
				{
					var isLight = data.KindIs == LayerKind.Light;
					var layer = schedule?.Take( isLight ) ?? EffectLayerObject.Make( isLight );
					layer.Root.SetParent( _parent, false );
					layer.Configure( effect, data, index++, motion );
					layer.Root.Enabled = true;
					_layers.Add( layer );

					WantedSprites += layer.WantedSprites;
					WantedLights += layer.IsLight ? 1 : 0;
				}
			}
		}

		ApplyShare( true );
	}

	// The share and lights this client shows; rates is whether the share changed, which starts the emitters again
	private void ApplyShare( bool rates )
	{
		var lights = 0;
		foreach ( var layer in _layers )
		{
			if ( layer.Root.IsValid() )
				layer.Share( _share, rates, _paused, layer.IsLight && _share > 0f && lights++ < _lights );
		}
	}
}
