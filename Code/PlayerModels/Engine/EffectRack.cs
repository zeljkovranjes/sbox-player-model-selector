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
/// how much of them shows is the schedule's to say too. Every frame each effect finds where it comes from (a joint, an
/// item worn or the weapon held, see <see cref="EffectAnchor"/>), what the body is doing is read once (see
/// <see cref="EffectSignals"/>), an effect with a key fades in or out as its key shows it, and each layer is placed
/// without allocating.
/// </summary>
internal sealed partial class EffectRack
{
	// The share of sprites shown changes in steps this fine: smaller changes aren't worth starting the emitters again
	private const float ShareSteps = 20f;

	// An effect with a key fades in and out over this long
	private const float KeyFadeSeconds = 0.5f;

	private readonly List<EffectLayerObject> _layers = new();
	private readonly EffectSignals _signals = new();
	private IReadOnlyList<Effect> _wanted = Array.Empty<Effect>();
	private IReadOnlyList<Accessory> _items = Array.Empty<Accessory>();
	private IReadOnlyList<DrawnShape> _shapes = Array.Empty<DrawnShape>();
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

	// Each effect shown, in order: its id, where it comes from (and, for an item, the item's place in the look), whether
	// it shows on a key, how far it shows now (0 to 1), and where it came from this frame
	private string[] _ids = Array.Empty<string>();
	private EffectAnchor[] _anchors = Array.Empty<EffectAnchor>();
	private int[] _itemIndices = Array.Empty<int>();
	private int[] _anchorJoints = Array.Empty<int>();
	private bool[] _keyed = Array.Empty<bool>();
	private float[] _gates = Array.Empty<float>();
	private Transform[] _from = Array.Empty<Transform>();
	private int _effects;

	// Whether an effect with a key shows now, by id: the game's keys; null shows them all (a preview)
	private Func<string, bool> _keyShown;

	// What the layers were last placed with, for an anchor worked out between frames (see TryGetFrame)
	private SkinnedModelRenderer _model;
	private PoseTransfer _transfer;
	private AccessoryRack _worn;
	private HeldItems _held;
	private float _lastPlaced = -1f;
	private float _lastRead = -1f;

	// Whether the schedule reads the body each frame; a scene that doesn't run it (a preview) has it read when placed
	private bool _readEachFrame;

	/// <summary>The citizen the effects are worn over: where they are, for how much of them shows.</summary>
	internal SkinnedModelRenderer Source { get; private set; }

	/// <summary>How many sprites it has out at once, all shown; and how many lights it has.</summary>
	internal float WantedSprites { get; private set; }
	internal int WantedLights { get; private set; }

	/// <summary>Whether it shows anything.</summary>
	internal bool IsWorn => _layers.Count > 0;

	/// <summary>How many layers it shows now.</summary>
	internal int LayerCount => _layers.Count;

	/// <summary>
	/// Wears <paramref name="effects"/> on <paramref name="parent"/>, in place of what it wore, over the citizen
	/// <paramref name="citizen"/> posing <paramref name="model"/> through <paramref name="transfer"/> (no transfer: the
	/// citizen wears them itself). <paramref name="items"/> are the items worn, which effects can come from, and
	/// <paramref name="shapes"/> the drawn shapes the look carries, which its layers can name;
	/// <paramref name="keyShown"/> says whether an effect with a key shows now (null: always), and <paramref name="health"/>
	/// reads the wearer's health (null: never hurt). They are built on a later <see cref="Place"/>, once the schedule lets
	/// them through; the same effects again change nothing.
	/// </summary>
	internal void Apply( IReadOnlyList<Effect> effects, IReadOnlyList<Accessory> items, IReadOnlyList<DrawnShape> shapes, GameObject parent,
		PoseTransfer transfer, SkinnedModelRenderer citizen, Model model, Func<string, bool> keyShown, Func<float> health )
	{
		_wanted = effects ?? Array.Empty<Effect>();
		_items = items ?? Array.Empty<Accessory>();
		_shapes = shapes ?? Array.Empty<DrawnShape>();
		_parent = parent;
		_keyShown = keyShown;
		_signals.Health = health;
		Source = citizen;

		var key = _wanted.Count == 0 ? "" : JsonSerializer.Serialize( _wanted ) + ItemsKey( _wanted, _items );
		if ( key == _key )
			return;

		_key = key;
		_joints ??= new EffectJoints( transfer, citizen.Model, model );
		_pending = true;
	}

	/// <summary>
	/// Places every layer where it comes from and beats its rhythm, after the model is posed by <paramref name="transfer"/>
	/// (none: the citizen wears the effects itself, <paramref name="model"/> is the citizen); <paramref name="worn"/> and
	/// <paramref name="held"/> are the items and what the body holds, where effects can come from. Builds the effects first
	/// when they changed and the schedule lets them through. Runs every frame the body is posed.
	/// </summary>
	internal void Place( SkinnedModelRenderer citizen, SkinnedModelRenderer model, PoseTransfer transfer, AccessoryRack worn, HeldItems held )
	{
		if ( _pending && _parent.IsValid() && (Schedule()?.TryBuild( Count( _wanted ) ) ?? true) )
			Build( model );

		if ( _layers.Count == 0 )
			return;

		_model = model;
		_transfer = transfer;
		_worn = worn;
		_held = held;

		var now = RealTime.Now;
		var delta = _lastPlaced < 0f ? 0f : now - _lastPlaced;
		_lastPlaced = now;

		if ( !_readEachFrame )
			ReadNow();

		_joints.NextFrame();
		for ( var e = 0; e < _effects; e++ )
		{
			var shows = !_keyed[ e ] || (_keyShown?.Invoke( _ids[ e ] ) ?? true);
			_gates[ e ] = Approach( _gates[ e ], shows ? 1f : 0f, delta / KeyFadeSeconds );
			_from[ e ] = Anchored( e, citizen, model, transfer );
		}

		RunScripts( citizen, model, delta );

		for ( var i = 0; i < _layers.Count; i++ )
		{
			var layer = _layers[ i ];
			if ( !layer.Root.IsValid() )
				continue;

			var from = _from[ layer.Effect ];
			if ( layer.Joint >= 0 && _joints.TryGet( layer.Joint, citizen, model, transfer, out var joint ) )
				from = joint;

			var lineEnd = from.Position;
			if ( layer.ToJoint >= 0 && _joints.TryGet( layer.ToJoint, citizen, model, transfer, out var end ) )
				lineEnd = end.Position;

			if ( layer.Kind == LayerKind.Ghost )
				layer.GhostOf = GhostOf( layer.Effect, model );

			layer.Place( from, lineEnd, now, delta, _signals, _gates[ layer.Effect ] );
		}

		_signals.Seen();
	}

	/// <summary>
	/// Reads what the body is doing this frame (see <see cref="EffectSignals"/>): before its animation runs, which clears
	/// the moments the game told it of. Every frame, from the schedule.
	/// </summary>
	internal void Read()
	{
		_readEachFrame = true;
		ReadNow();
	}

	private void ReadNow()
	{
		if ( !Source.IsValid() )
			return;

		var now = RealTime.Now;
		_signals.Read( Source, _lastRead < 0f ? 0f : now - _lastRead );
		_lastRead = now;
	}

	/// <summary>
	/// Where an effect would come from this frame with <paramref name="anchor"/>, before its own offset: for moving it
	/// between joints, items and the weapon while keeping it where it is. False before the effects are first placed.
	/// </summary>
	internal bool TryGetFrame( EffectAnchor anchor, out Transform frame )
	{
		frame = default;
		if ( _joints is null || !Source.IsValid() || !_model.IsValid() )
			return false;

		frame = FrameOf( anchor, IndexOfItem( anchor, _items ), JointOf( anchor ), Source, _model, _transfer );
		return true;
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

	/// <summary>
	/// Counts the drawn shapes its sprites look like as showing <paramref name="distance"/> from the camera (see
	/// <see cref="ShapeTextures.Count"/>). A few times a second, nearest body first: no allocations.
	/// </summary>
	internal void CountShapes( float distance )
	{
		foreach ( var layer in _layers )
		{
			layer.Distance = distance;
			if ( layer.DrawnContent is { } content )
				ShapeTextures.Count( content, distance );
		}
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
			layers += effect.Off ? 0 : effect.LayersOn();

		return layers;
	}

	// Which items the effects come from, by their places: the same item moved to another place builds them again
	private static string ItemsKey( IReadOnlyList<Effect> effects, IReadOnlyList<Accessory> items )
	{
		var key = "";
		foreach ( var effect in effects )
		{
			if ( effect.Anchor?.KindIs == AnchorKind.Item )
				key += $"|{IndexOfItem( effect.Anchor, items )}";
		}

		return key;
	}

	// The place in the look of the first item worn that an item anchor names; -1 for none
	private static int IndexOfItem( EffectAnchor anchor, IReadOnlyList<Accessory> items )
	{
		if ( anchor?.KindIs != AnchorKind.Item )
			return -1;

		for ( var i = 0; i < items.Count; i++ )
		{
			if ( items[ i ]?.Item == anchor.Item )
				return i;
		}

		return -1;
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
		_effects = 0;
		ForgetRuns();
		WantedSprites = 0f;
		WantedLights = 0;
		schedule?.TakeOff( this );
	}

	private void Build( SkinnedModelRenderer model )
	{
		_pending = false;
		ReturnLayers();

		var schedule = Schedule();
		var motion = _joints.BodyScale * model.WorldScale.x;
		var index = 0;
		Size( _wanted.Count );

		using ( _parent.Scene.Push() )
		{
			foreach ( var effect in _wanted )
			{
				if ( effect.Off )
					continue;

				var shown = _effects++;
				_ids[ shown ] = effect.Id;
				_anchors[ shown ] = effect.Anchor ?? new EffectAnchor();
				_itemIndices[ shown ] = IndexOfItem( effect.Anchor, _items );
				_anchorJoints[ shown ] = JointOf( _anchors[ shown ] );
				_keyed[ shown ] = effect.Key is not null;
				_gates[ shown ] = _keyed[ shown ] ? 0f : 1f;
				var run = _runs[ shown ] = RunFor( effect, shown );

				// A look not built here first (see EffectScripts) has its script's layers in the build's order
				var unmarked = run is not null && effect.Layers.Count == run.Machine.LayerCount && effect.Layers.TrueForAll( x => x.ScriptLayer < 0 );
				for ( var place = 0; place < effect.Layers.Count; place++ )
				{
					var data = effect.Layers[ place ];
					if ( unmarked )
						data.ScriptLayer = place;

					if ( data.Off )
						continue;

					_wantsShots |= ShowsOnKill( data );

					var kind = data.KindIs;
					var layer = schedule?.Take( kind ) ?? EffectLayerObject.Make( kind );
					layer.Root.SetParent( _parent, false );
					layer.Configure( effect, shown, data, index++, motion, _shapes, run );
					run?.Drive( data.ScriptLayer, layer );
					layer.Root.Enabled = true;
					_layers.Add( layer );

					WantedSprites += layer.WantedSprites;
					WantedLights += layer.IsLight ? 1 : 0;
				}
			}
		}

		if ( _layers.Count > 0 )
			schedule?.Wear( this );

		ApplyShare( true );
	}

	// Room for every effect, made only when there are more than ever before
	private void Size( int effects )
	{
		if ( _ids.Length >= effects )
			return;

		_ids = new string[ effects ];
		_anchors = new EffectAnchor[ effects ];
		_itemIndices = new int[ effects ];
		_anchorJoints = new int[ effects ];
		_keyed = new bool[ effects ];
		_gates = new float[ effects ];
		_from = new Transform[ effects ];
		_runs = new EffectScriptRun[ effects ];
		_anchorWas = new Vector3[ effects ];
		_anchorSpeeds = new float[ effects ];
	}

	// Where effect e comes from this frame, its offset included
	private Transform Anchored( int e, SkinnedModelRenderer citizen, SkinnedModelRenderer model, PoseTransfer transfer )
	{
		var anchor = _anchors[ e ];
		var frame = FrameOf( anchor, _itemIndices[ e ], _anchorJoints[ e ], citizen, model, transfer );
		return anchor.Forward == 0f && anchor.Left == 0f && anchor.Up == 0f
			? frame
			: frame.ToWorld( new Transform( new Vector3( anchor.Forward, anchor.Left, anchor.Up ) ) );
	}

	// Where an anchor is: the middle of its item (turned as the item is), the tip of the weapon held, or its joint, which
	// also stands in while the item isn't worn or nothing is held. Every frame: no allocations
	private Transform FrameOf( EffectAnchor anchor, int item, int joint, SkinnedModelRenderer citizen, SkinnedModelRenderer model, PoseTransfer transfer )
	{
		var scale = _joints.BodyScale * model.WorldScale.x;
		switch ( anchor.KindIs )
		{
			case AnchorKind.Item when item >= 0 && _worn is not null && _worn.TryGetPlacement( item, out var placement ) && placement.Renderer.IsValid():
				return new Transform( placement.Renderer.Bounds.Center, placement.Renderer.WorldRotation, scale );

			case AnchorKind.HeldWeapon when _held is not null && _held.TryGetTip( out var tip ):
				return new Transform( tip.Position, tip.Rotation, scale );
		}

		return _joints.TryGet( joint, citizen, model, transfer, out var placed ) ? placed : new Transform( model.WorldPosition, model.WorldRotation, scale );
	}

	private static int JointOf( EffectAnchor anchor )
	{
		return Enum.TryParse<Effect.Joint>( anchor.Joint, out var joint ) ? (int)joint : (int)Effect.Joint.Head;
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

	private static float Approach( float value, float target, float step )
	{
		return value < target ? MathF.Min( value + step, target ) : MathF.Max( value - step, target );
	}
}
