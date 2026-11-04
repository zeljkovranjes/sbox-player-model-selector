using System;
using System.Collections.Generic;
using System.Linq;
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
internal sealed class EffectRack
{
	// The share of sprites shown changes in steps this fine: smaller changes aren't worth starting the emitters again
	private const float ShareSteps = 20f;

	// An effect with a key fades in and out over this long
	private const float KeyFadeSeconds = 0.5f;

	// How far a shot reaches, and a swing (from a weapon that marks no muzzle: a crowbar, a sword)
	private const float ShotReach = 4096f;
	private const float SwingReach = 72f;

	// Where bare hands swing from, above the feet
	private const float PunchHeight = 50f;

	private readonly List<EffectLayerObject> _layers = new();
	private readonly EffectSignals _signals = new();
	private IReadOnlyList<Effect> _wanted = Array.Empty<Effect>();
	private IReadOnlyList<Effect> _weaponEffects;

	// Read through: a hot reload keeps this object but not fields added since
	private IReadOnlyList<Effect> WeaponEffects => _weaponEffects ?? Array.Empty<Effect>();
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
	private bool[] _onWeapon = Array.Empty<bool>();
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

	// Whether any layer shows along a shot or where it hits, or when it hits, or lies along a swing: only then is a shot
	// traced
	private bool _wantsShot;

	// Where the tip of what is held was last frame, and how fast it moved: the way a swing sweeps
	private Vector3 _lastTip;
	private Vector3 _tipVelocity;
	private bool _hasTip;

	/// <summary>The citizen the effects are worn over: where they are, for how much of them shows.</summary>
	internal SkinnedModelRenderer Source { get; private set; }

	/// <summary>How much smaller (or larger) than on a body the effects are made: a first-person weapon's are smaller, as it is right by the eye.</summary>
	internal float SizeScale { get; set; } = 1f;

	/// <summary>Whether its layers show whole however near the camera they are (see <see cref="EffectLayerObject.NearCameraShown"/>).</summary>
	internal bool NearCameraShown { get; set; }

	/// <summary>
	/// Whether the effects coming from the weapon held send nothing: their player sees them on their first-person weapon
	/// instead. Changing it never builds anything again.
	/// </summary>
	internal bool HidesWeaponEffects { get; set; }

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
	/// <paramref name="keyShown"/> says whether an effect with a key shows now (null: always), and <paramref name="wearer"/>
	/// who wears them (null: nobody in particular, see <see cref="EffectWearer"/>). They are built on a later
	/// <see cref="Place"/>, once the schedule lets them through; the same effects again change nothing.
	/// </summary>
	internal void Apply( IReadOnlyList<Effect> effects, IReadOnlyList<Accessory> items, IReadOnlyList<DrawnShape> shapes, GameObject parent,
		PoseTransfer transfer, SkinnedModelRenderer citizen, Model model, Func<string, bool> keyShown, EffectWearer wearer )
	{
		_wanted = effects ?? Array.Empty<Effect>();
		_items = items ?? Array.Empty<Accessory>();
		_shapes = shapes ?? Array.Empty<DrawnShape>();
		_parent = parent;
		_keyShown = keyShown;
		_signals.Wearer = wearer ?? EffectWearer.None;
		Source = citizen;
		_joints ??= new EffectJoints( transfer, citizen.Model, model );
		Rebuild();
	}

	/// <summary>
	/// Wears <paramref name="effects"/> beside the body's, coming from the weapon held: the design's of the weapon held
	/// now (see <see cref="WeaponDesign.Effects"/>), or none. Built like the body's, when they change.
	/// </summary>
	internal void ApplyWeapon( IReadOnlyList<Effect> effects )
	{
		_weaponEffects = effects;
		if ( _joints is not null )
			Rebuild();
	}

	// Built again on the next Place when what is worn changed
	private void Rebuild()
	{
		var key = _wanted.Count + WeaponEffects.Count == 0 ? "" : JsonSerializer.Serialize( _wanted ) + JsonSerializer.Serialize( WeaponEffects ) + ItemsKey( _wanted, _items );
		if ( key == _key )
			return;

		_key = key;
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
		if ( _pending && _parent.IsValid() && (Schedule()?.TryBuild( Count( _wanted ) + Count( WeaponEffects ) ) ?? true) )
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
		if ( _wantsShot )
			FollowTip( delta );

		if ( _wantsShot && _signals.Has( LayerWhen.OnShoot ) )
			Shoot();

		for ( var e = 0; e < _effects; e++ )
		{
			var shows = (!_keyed[ e ] || (_keyShown?.Invoke( _ids[ e ] ) ?? true)) && !(HidesWeaponEffects && _onWeapon[ e ]);
			_gates[ e ] = Approach( _gates[ e ], shows ? 1f : 0f, delta / KeyFadeSeconds );
			_from[ e ] = Anchored( e, citizen, model, transfer );
		}

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
		_signals.Read( Source, _lastRead < 0f ? 0f : now - _lastRead, _held?.Holding );
		_lastRead = now;
	}

	// How fast the tip of what is held moves, for the way a swing sweeps. Every frame while a layer needs it: no allocations
	private void FollowTip( float delta )
	{
		if ( _held is null || !_held.TryGetTip( out var tip ) )
		{
			_hasTip = false;
			_tipVelocity = Vector3.Zero;
			return;
		}

		if ( _hasTip && delta > 0f )
			_tipVelocity = (tip.Position - _lastTip) / delta;

		_lastTip = tip.Position;
		_hasTip = true;
	}

	// Works out where the shot just fired went: from the weapon's muzzle along where the wearer aims, one trace that
	// passes through the shooter, to what it hit or as far as it reaches (a swing reaches only so far). Skipped for a body
	// out of view or too far to show effects: nothing there would show it
	private void Shoot()
	{
		if ( _paused || _share <= 0f )
			return;

		// Bare hands punch from the chest
		var tip = _held is not null && _held.TryGetTip( out var held ) ? held : new Transform( Source.WorldPosition + Vector3.Up * PunchHeight, Source.WorldRotation );
		var direction = _signals.Wearer.Aim?.Invoke() ?? tip.Rotation.Forward;
		var reach = _held is not null && _held.HoldsMuzzle ? ShotReach : SwingReach;
		var shooter = _signals.Wearer.Shooter.IsValid() ? _signals.Wearer.Shooter : Source.GameObject.Root;
		var trace = Source.Scene.Trace.Ray( tip.Position, tip.Position + direction * reach ).IgnoreGameObjectHierarchy( shooter ).Run();
		_signals.Shot = new EffectSignals.ShotPath
		{
			Origin = tip.Position, Direction = direction, Hit = trace.Hit, HitPosition = trace.HitPosition, HitNormal = trace.Normal, Swing = _tipVelocity,
			Distance = trace.Hit ? trace.Fraction * reach : reach,
		};

		if ( trace.Hit )
			_signals.Hit();
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

		for ( var i = 0; i < Stickers.Count; i++ )
			ShapeTextures.Count( Stickers[ i ].Content, distance );
	}

	/// <summary>The drawn shapes the stickers on the weapon held are (see <see cref="WeaponDresser"/>), counted with its layers'.</summary>
	internal IReadOnlyList<DrawnShape> Stickers { get; set; } = Array.Empty<DrawnShape>();

	/// <summary>Gives every layer back to the scene for reuse; nothing shows until effects are applied again.</summary>
	internal void Release()
	{
		ReturnLayers();
		_signals.Release();
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
		WantedSprites = 0f;
		WantedLights = 0;
		schedule?.TakeOff( this );
	}

	private void Build( SkinnedModelRenderer model )
	{
		_pending = false;
		ReturnLayers();

		var schedule = Schedule();
		var motion = _joints.BodyScale * model.WorldScale.x * SizeScale;
		var index = 0;
		Size( _wanted.Count + WeaponEffects.Count );
		_wantsShot = false;

		using ( _parent.Scene.Push() )
		{
			foreach ( var effect in _wanted.Concat( WeaponEffects ) )
			{
				if ( effect.Off )
					continue;

				var shown = _effects++;
				_ids[ shown ] = effect.Id;
				_anchors[ shown ] = effect.Anchor ?? new EffectAnchor();
				_itemIndices[ shown ] = IndexOfItem( effect.Anchor, _items );
				_anchorJoints[ shown ] = JointOf( _anchors[ shown ] );
				_keyed[ shown ] = effect.Key is not null;
				_onWeapon[ shown ] = _anchors[ shown ].KindIs == AnchorKind.HeldWeapon;
				_gates[ shown ] = _keyed[ shown ] ? 0f : 1f;

				foreach ( var data in effect.Layers )
				{
					if ( data.Off )
						continue;

					var kind = data.KindIs;
					var layer = schedule?.Take( kind ) ?? EffectLayerObject.Make( kind );
					layer.Root.SetParent( _parent, false );
					layer.NearCameraShown = NearCameraShown;
					layer.Configure( effect, shown, data, index++, motion, _shapes, FootOf( effect, data ) );
					_wantsShot |= data.ShapeIs == LayerShape.Shot || data.AtHit || data.WhenIs == LayerWhen.OnHit || data.AlignIs == LayerAlign.Swing;
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
		// A rack kept by a hot reload may lack arrays added since
		if ( _ids.Length >= effects && _onWeapon is not null )
			return;

		_ids = new string[ effects ];
		_anchors = new EffectAnchor[ effects ];
		_itemIndices = new int[ effects ];
		_anchorJoints = new int[ effects ];
		_keyed = new bool[ effects ];
		_onWeapon = new bool[ effects ];
		_gates = new float[ effects ];
		_from = new Transform[ effects ];
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

	// Which foot a layer's steps are (0 left, 1 right): a layer on a foot steps with that foot alone; -1 for either
	private static int FootOf( Effect effect, EffectLayer layer )
	{
		var anchorJoint = effect.Anchor?.KindIs == AnchorKind.Joint ? effect.Anchor.Joint : null;
		return (layer.Joint ?? anchorJoint) switch
		{
			nameof( Effect.Joint.LeftFoot ) => 0,
			nameof( Effect.Joint.RightFoot ) => 1,
			_ => -1,
		};
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
