using System;
using System.Collections.Generic;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The object one layer of a worn effect shows on (see <see cref="EffectLayer"/>): a particle effect with its emitters and
/// sprite renderer, a light that casts no shadows, or a trail. Made once and kept for reuse (see
/// <see cref="EffectSchedule"/>), set up again for each layer it shows. Each frame it is placed where it comes from, beats
/// its rhythm, shows only when its layer says (see <see cref="LayerWhen"/>), fades with its key and with how fast its
/// place moves, and a colour cycle colours each of its sprites (see <see cref="ColorCycle"/>), all without allocating.
/// </summary>
internal sealed class EffectLayerObject
{
	// All around the body: an upright ball, this much narrower than it is tall
	private static readonly Vector3 AroundBodyBias = new( 0.35f, 0.35f, 1f );

	// A ring's sprites come from this close around its line, for its size
	private const float RingThickness = 0.06f;

	// Sprites live and are sized each a little apart from the next, so they don't move in step
	private const float LifetimeSpread = 0.2f;
	private const float SizeSpread = 0.2f;

	// Sprites of a picture with a way up (a shape, a drawn shape or sticker, a flame) stay near upright; the rest turn at
	// random
	private const float UprightRoll = 8f;

	// An emitter counts what it made from when it last started, starting again each loop: long, so that never shows
	private const float EmitterLoop = 600f;

	// Each layer beats this far behind the one before it, so layers alike don't beat in step
	private const float PhaseStep = 0.618f;

	private const float MillisecondsPerSecond = 1000f;

	// A jiggle wobbles this far at its fullest, in the citizen's units
	private const float JiggleReach = 4f;

	// A light switching on or off for its moment, or its key, takes this long to fade
	private const float FadeSeconds = 0.3f;

	// Near the camera a layer fades: all of it shows from this far away, none of it this close, so nobody's effect (one's
	// own in third person, or another player's standing in one's face) can fill the view or blind it. Sprites fade each
	// on its own, on the GPU; a light or a trail by where its place is
	private const float NearFadeFull = 60f;
	private const float NearFadeGone = 20f;

	// Fading with speed: the place moving this fast (citizen units a second) shows fully; how quickly the speed read
	// follows it
	private const float FullSpeed = 160f;
	private const float SpeedFollow = 15f;

	// A swing's tip moving slower than this (units a second) says nothing about its plane: it sweeps across the aim,
	// down from high on the right this far (degrees), the way most swings go
	private const float MinSwingSpeed = 30f;
	private const float SwingTilt = 35f;

	// A trail has at most this many points, one every this many units (citizen units)
	private const int TrailPoints = 32;
	private const float TrailPointDistance = 2f;

	// A trail's opacity is picked from this many steps, made once, so fading it allocates nothing
	private const int TrailShades = 10;

	// Sprites fade in over the first part of their life and out over the last, so none pops in or out
	private static readonly Curve Fade = new( new Curve.Frame( 0f, 0f ), new Curve.Frame( 0.15f, 1f ), new Curve.Frame( 0.6f, 1f ), new Curve.Frame( 1f, 0f ) );

	/// <summary>The object itself.</summary>
	internal GameObject Root { get; }

	/// <summary>Whether it shows whole however near the camera it is: a first-person weapon's, which is always near.</summary>
	internal bool NearCameraShown { get; set; }

	/// <summary>What it shows: sprites, a light or a trail.</summary>
	internal LayerKind Kind { get; }

	/// <summary>Whether it is a light.</summary>
	internal bool IsLight => Kind == LayerKind.Light;

	/// <summary>The <see cref="Effect.Joint"/> of its own it comes from, as set up last; -1 for its effect's anchor.</summary>
	internal int Joint { get; private set; }

	/// <summary>The joint a line runs to, as set up last; -1 for none.</summary>
	internal int ToJoint { get; private set; }

	/// <summary>Which of the body's effects it belongs to, by place among those shown.</summary>
	internal int Effect { get; private set; }

	/// <summary>How many sprites it has out at once, all shown.</summary>
	internal float WantedSprites { get; private set; }

	/// <summary>How far its body is from the camera, as last counted: who gets a drawn shape's texture first.</summary>
	internal float Distance { get; set; }

	/// <summary>What the drawn shape its sprites look like is (see <see cref="DrawnShape.Content"/>); null for none.</summary>
	internal string DrawnContent => _drawn?.Content;

	private readonly ParticleEffect _particles;
	private readonly ParticleSphereEmitter _sphere;
	private readonly ParticleRingEmitter _ring;
	private readonly ParticleBoxEmitter _box;
	private readonly ParticleConeEmitter _cone;
	private readonly EffectPointsEmitter _points;
	private readonly ParticleSpriteRenderer _renderer;
	private readonly PointLight _light;
	private readonly TrailRenderer _trail;
	private ParticleEmitter _emitter;

	// Where it sits from its place and the way it faces, its rhythm, its colour (a light's at full brightness), how many
	// sprites it makes a second, the part of them this client shows and how many it may have out
	private Vector3 _offset;
	private Rotation _facing = Rotation.Identity;
	private LayerRhythm _rhythm;
	private float _beatSpeed;
	private float _beatAmount;
	private float _phase;
	private Color _colour;
	private float _rate;
	private float _share = 1f;
	private int _most;
	private float _motion = 1f;

	// When it shows: the moment's burst and how long it shows after, when the last moment ends, whether it is sending
	// sprites (or drawing its trail) now, a light's level as it fades, and whether it fades with speed
	private LayerWhen _when;
	private float _burst;
	private float _showFor;
	private float _momentUntil;
	private bool _bursting;
	private bool _emitting = true;
	private float _level = 1f;
	private bool _fadesWithSpeed;

	// Which foot its steps are (0 left, 1 right, -1 either); whether it runs along the last shot, or shows where it hit;
	// and how far apart sprites along a shot are, for its size
	private int _foot = -1;
	private bool _alongShot;
	private bool _atHit;
	private bool _alongSwing;
	private Rotation _swing = Rotation.Identity;
	private float _shotSpacing = 1f;
	private Transform _hit;

	// How fast its place moves, and where it was last frame
	private float _speed;
	private Vector3 _lastPosition;
	private bool _hasLastPosition;
	private float _lastShown = -1f;

	// A trail's colours at each step of opacity, made when it is set up
	private readonly Gradient[] _trailShades = new Gradient[ TrailShades + 1 ];
	private int _trailShade = -1;

	// The drawn shape its sprites look like, and the textures' generation it was last looked up at (see ShapeTextures):
	// a plain circle shows until it is drawn, and when it is let go of for a nearer body
	private DrawnShape _drawn;
	private int _drawnGeneration = -1;

	// A colour cycle: which (none for fixed colours), whether it is a mix (each sprite one colour for life), its colours,
	// how many sprites it counts through, when it started and how fast it runs, how far it is this frame, the rhythm's
	// light or dark over it, and the next sprite's place in the count. The particle threads colour the sprites from these,
	// so only the main thread writes them, before they step
	private ColorCycle.Kind? _cycle;
	private bool _mix;
	private Color[] _palette = Array.Empty<Color>();
	private int _cycleCount = 1;
	private float _cycleStart;
	private float _cycleSpeed = 1f;
	private long _cycleMs;
	private Color _tint = Color.White;
	private int _spawned;

	private EffectLayerObject( GameObject root, LayerKind kind )
	{
		Root = root;
		Kind = kind;
		switch ( kind )
		{
			case LayerKind.Light:
				_light = root.AddComponent<PointLight>();
				_light.Shadows = false;
				return;

			case LayerKind.Trail:
				_trail = root.AddComponent<TrailRenderer>();
				return;
		}

		_particles = root.AddComponent<ParticleEffect>();
		_sphere = root.AddComponent<ParticleSphereEmitter>();
		_ring = root.AddComponent<ParticleRingEmitter>();
		_box = root.AddComponent<ParticleBoxEmitter>();
		_cone = root.AddComponent<ParticleConeEmitter>();
		_points = root.AddComponent<EffectPointsEmitter>();
		_renderer = root.AddComponent<ParticleSpriteRenderer>();
	}

	/// <summary>A new object of <paramref name="kind"/>, in the scene pushed now, switched off for <see cref="Configure"/> to set up.</summary>
	internal static EffectLayerObject Make( LayerKind kind )
	{
		var root = new GameObject( false, kind switch { LayerKind.Light => "Effect light", LayerKind.Trail => "Effect trail", _ => "Effect sprites" } );
		root.NetworkMode = NetworkMode.Never;
		root.Flags |= GameObjectFlags.NotSaved;
		return new EffectLayerObject( root, kind );
	}

	/// <summary>
	/// Sets it up as <paramref name="data"/> of <paramref name="effect"/> says, as the <paramref name="index"/>th layer of
	/// the body and of its <paramref name="effectIndex"/>th effect shown; <paramref name="motion"/> is how many world units
	/// a citizen's unit is on this body, and <paramref name="shapes"/> the drawn shapes its look carries. Runs when the look
	/// changes: what it works out (a formation's points) is kept.
	/// </summary>
	internal void Configure( Effect effect, int effectIndex, EffectLayer data, int index, float motion, IReadOnlyList<DrawnShape> shapes, int foot = -1 )
	{
		_foot = foot;
		_alongShot = data.ShapeIs == LayerShape.Shot;
		_atHit = data.AtHit;
		_hit = default;
		_alongSwing = data.AlignIs == LayerAlign.Swing;
		_swing = Rotation.Identity;
		_drawn = Kind == LayerKind.Sprite ? Find( shapes, data.DrawnPicture ) : null;
		_drawnGeneration = -1;
		if ( _drawn?.Sticker is { } sticker )
			data = sticker.Drive( data );
		else if ( _drawn is not null )
			data = StickerFinishes.Drive( data, _drawn.FinishIs );

		Effect = effectIndex;
		Joint = data.Joint is null ? -1 : (int)effect.JointOf( data );
		ToJoint = data.ShapeIs == LayerShape.Line && data.ToJoint is not null && Enum.TryParse<Effect.Joint>( data.ToJoint, out var to ) ? (int)to : -1;
		_offset = new Vector3( data.Forward, data.Left, data.Up );
		_facing = FacingOf( data.FacingIs );
		_rhythm = data.RhythmIs;
		_beatSpeed = data.RhythmSpeed * effect.SpeedFactor;
		_beatAmount = data.RhythmAmount;
		_phase = index * PhaseStep;
		_motion = motion;
		_when = data.WhenIs;
		_burst = data.Burst * effect.AmountFactor;
		_showFor = data.ShowFor;
		_momentUntil = 0f;
		_bursting = false;
		_fadesWithSpeed = data.FadeWithSpeed;
		_hasLastPosition = false;
		_speed = 0f;
		_lastShown = -1f;
		_level = _when == LayerWhen.Always ? 1f : 0f;
		UseCycle( effect, data );
		if ( _drawn?.Sticker is { IsAnimated: true } animated && data.ColorModeIs != LayerColorMode.Fixed )
			_cycleSpeed *= animated.Speed;

		var colour = Hued( data.Color, effect.Hue );
		var colourEnd = data.ColorEnd is null ? colour : Hued( data.ColorEnd, effect.Hue );

		// A rainbow beats through the colours itself, so under it a layer is only light or dark
		if ( _rhythm == LayerRhythm.Rainbow )
		{
			colour = Grey( colour );
			colourEnd = Grey( colourEnd );
		}

		WantedSprites = 0f;
		switch ( Kind )
		{
			case LayerKind.Light:
			{
				// A light in a cycle takes its colours from it
				var brightness = data.Amount * effect.AmountFactor;
				colour = _cycle is null && !_mix ? colour : Color.White;
				_colour = new Color( colour.r * brightness, colour.g * brightness, colour.b * brightness, 1f );
				_light.LightColor = _colour * _level;
				_light.Radius = data.Size * effect.SizeFactor * motion;
				_light.Shadows = false;
				return;
			}

			case LayerKind.Trail:
				ConfigureTrail( effect, data, colour, colourEnd, motion );
				return;

			default:
				ConfigureSprites( effect, data, colour, colourEnd, motion, shapes );
				return;
		}
	}

	// The drawn shape the look carries with id id; null for none
	private static DrawnShape Find( IReadOnlyList<DrawnShape> shapes, string id )
	{
		if ( id is null || shapes is null )
			return null;

		for ( var i = 0; i < shapes.Count; i++ )
		{
			if ( shapes[ i ].Id == id )
				return shapes[ i ];
		}

		return null;
	}

	/// <summary>
	/// Shows <paramref name="share"/> of its sprites (starting the emitter again when <paramref name="rates"/>: the share
	/// changed), paused or not; a light shows while <paramref name="lit"/>.
	/// </summary>
	internal void Share( float share, bool rates, bool paused, bool lit )
	{
		switch ( Kind )
		{
			case LayerKind.Light:
				_light.Enabled = lit;
				return;

			case LayerKind.Trail:
				_trail.Enabled = share > 0f;
				return;
		}

		_particles.Paused = paused;
		if ( !rates )
			return;

		_share = share;
		_particles.MaxParticles = Math.Max( 1, (int)MathF.Ceiling( _most * share ) );
		UseRate( true );
	}

	/// <summary>
	/// Puts it where it comes from (<paramref name="from"/>, as placed this frame; a line runs to <paramref name="lineEnd"/>
	/// when it has an end joint), beats its rhythm at <paramref name="now"/>, <paramref name="delta"/> seconds after the last
	/// time, and shows it as <paramref name="signals"/> say its layer wants, faded by its key's <paramref name="gate"/> (0
	/// to 1; 0 sends nothing new).
	/// </summary>
	internal void Place( Transform from, Vector3 lineEnd, float now, float delta, EffectSignals signals, float gate )
	{
		var beat = now * _beatSpeed + _phase;
		if ( _cycle is not null )
			_cycleMs = (long)((now - _cycleStart) * MillisecondsPerSecond * _cycleSpeed);

		// Turning carries its offset round with it, so a layer set off from its place circles it
		var turn = _rhythm == LayerRhythm.Spin ? Rotation.FromAxis( Vector3.Up, beat % 1f * 360f ) : Rotation.Identity;
		var offset = turn * _offset;
		if ( _rhythm == LayerRhythm.Jiggle )
			offset += Wobble( beat ) * (_beatAmount * JiggleReach);

		var placed = from.ToWorld( new Transform( offset, turn * _facing ) );
		var wanted = gate > 0f && Wants( signals, now );
		if ( _atHit && _bursting )
			_hit = HitOf( signals.Shot, from.Scale.x );

		// Where the last shot hit stays its place until the next: what it threw there lives out its life there
		if ( _atHit && _hit.Scale.x > 0f )
			placed = _hit;

		// Lying along the last swing: its sprites face out of the swing's plane, so they lie in it
		if ( _alongSwing )
		{
			if ( _bursting )
				_swing = SwingOf( signals.Shot, from.Rotation );

			placed = placed.WithRotation( _swing );
		}

		Root.WorldTransform = placed;
		if ( _drawn is not null && _drawnGeneration != ShapeTextures.Generation )
			UseDrawn();
		if ( ToJoint >= 0 )
			_points.LineEnd = placed.PointToLocal( lineEnd );

		if ( _alongShot && _bursting )
			AlongShot( signals.Shot, placed );

		FollowSpeed( placed.Position, delta );
		_level = Approach( _level, wanted ? 1f : 0f, delta / FadeSeconds );

		var shown = gate * (_fadesWithSpeed ? Math.Clamp( _speed / (FullSpeed * _motion), 0f, 1f ) : 1f)
			* (Kind == LayerKind.Sprite || NearCameraShown ? 1f : NearCamera( placed.Position ));
		Show( wanted, shown );
		if ( _bursting )
			Burst();

		Beat( beat, shown );
	}

	// Whether its layer wants to show now: always, while something is so, or for a moment after it happens, which bursts.
	// A layer on a foot steps with that foot alone
	private bool Wants( EffectSignals signals, float now )
	{
		if ( _when < LayerWhen.OnJump )
			return signals.Has( _when );

		if ( _when == LayerWhen.OnFootstep ? signals.HasFootstep( _foot ) : signals.Has( _when ) )
		{
			_momentUntil = now + _showFor;
			_bursting = true;
		}

		return now < _momentUntil;
	}

	// The plane a swing sweeps through, as a turn whose forward is out of it and whose up is where the player aims: from
	// the aim and the way the tip moved; a slanted sweep across the aim when the tip hardly moved
	private static Rotation SwingOf( in EffectSignals.ShotPath shot, Rotation facing )
	{
		var aim = shot.Direction.IsNearZeroLength ? facing.Forward : shot.Direction.Normal;
		var across = Vector3.Cross( aim, shot.Swing );
		if ( shot.Swing.Length < MinSwingSpeed || across.IsNearZeroLength )
			across = Rotation.FromAxis( aim, SwingTilt ) * Vector3.Cross( aim, facing.Right );

		return Rotation.LookAt( across.Normal, aim );
	}

	// Where a shot hit, facing out of what it hit, at the body's scale; where it ended if it hit nothing
	private static Transform HitOf( in EffectSignals.ShotPath shot, float scale )
	{
		var normal = shot.Hit ? shot.HitNormal : -shot.Direction;
		return new Transform( shot.End, Rotation.LookAt( normal.IsNearZeroLength ? Vector3.Up : normal ), MathF.Max( scale, 0.01f ) );
	}

	// Sprites come from the line from its place to where the shot ended, as many as fill it, at most the burst's
	private void AlongShot( in EffectSignals.ShotPath shot, Transform placed )
	{
		if ( Kind != LayerKind.Sprite || shot.Distance <= 0f )
			return;

		_points.LineEnd = placed.PointToLocal( shot.End );
		_shotCount = MathF.Min( _burst, MathF.Ceiling( shot.Distance / _shotSpacing ) );
	}

	// How many sprites the last shot's line takes (see AlongShot); the burst's own otherwise
	private float _shotCount = -1f;

	// A moment's sprites all at once: the emitter starts again with them (after it starts for the moment, which would
	// forget them), then bursts nothing until the next moment
	private void Burst()
	{
		_bursting = false;
		var burst = _alongShot && _shotCount >= 0f ? _shotCount : _burst;
		_shotCount = -1f;
		if ( Kind != LayerKind.Sprite || burst <= 0f )
			return;

		_emitter.Burst = MathF.Max( 1f, MathF.Round( burst * _share ) );
		_emitter.ResetEmitter();
		_emitter.Burst = 0f;
	}

	// Sends sprites or draws the trail while wanted; what is out already lives out its life
	private void Show( bool wanted, float shown )
	{
		switch ( Kind )
		{
			case LayerKind.Sprite when wanted != _emitting:
				_emitting = wanted;
				UseRate( wanted );
				break;

			case LayerKind.Trail:
				_trail.Emitting = wanted;
				var shade = (int)MathF.Round( shown * TrailShades );
				if ( shade != _trailShade )
				{
					_trailShade = shade;
					_trail.Color = _trailShades[ shade ];
				}

				break;
		}
	}

	// The emitter's rate for the part shown, or none while it isn't sending; starting again counts from now
	private void UseRate( bool restart )
	{
		// An emitter makes a whole number of sprites a second: any share of one makes at least one
		var rate = _emitting ? _rate * _share : 0f;
		_emitter.Rate = rate > 0f ? MathF.Max( 1f, rate ) : 0f;
		if ( restart && _emitting )
			_emitter.ResetEmitter();
	}

	// How much of it shows at position for the camera drawing its scene: eased from none to all (see NearFadeFull)
	private float NearCamera( Vector3 position )
	{
		if ( Root.Scene?.Camera is not { } camera )
			return 1f;

		var near = Math.Clamp( ((position - camera.WorldPosition).Length - NearFadeGone) / (NearFadeFull - NearFadeGone), 0f, 1f );
		return near * near * (3f - 2f * near);
	}

	private void FollowSpeed( Vector3 position, float delta )
	{
		if ( _hasLastPosition && delta > 0f )
			_speed += ((position - _lastPosition).Length / delta - _speed) * MathF.Min( 1f, delta * SpeedFollow );

		_lastPosition = position;
		_hasLastPosition = true;
	}

	// The points sprites come from: a drawn shape's (an emblem's layers', none where an eraser cut it), or a standard one's
	private static float[] FormationOf( DrawnShape drawn, EffectLayer data ) =>
		drawn is null ? StandardShapes.Formation( data.FormationIs, data.Fill ) : drawn.Formation( data.Fill );

	// The drawn shape's texture once it is drawn, a plain circle meanwhile (or while a nearer body has it); asked again
	// only when the textures change
	private void UseDrawn()
	{
		_drawnGeneration = ShapeTextures.Generation;
		var sprite = ShapeTextures.SpriteOf( _drawn, Distance ) ?? EffectAtlas.SpriteOf( StandardShape.Circle );
		if ( _renderer.Sprite != sprite )
			_renderer.Sprite = sprite;
	}

	private void ConfigureSprites( Effect effect, EffectLayer data, Color colour, Color colourEnd, float motion, IReadOnlyList<DrawnShape> shapes )
	{
		var size = effect.SizeFactor;
		var moves = motion * size;

		var particles = _particles;
		particles.Lifetime = new ParticleFloat( data.Lifetime * (1f - LifetimeSpread), data.Lifetime * (1f + LifetimeSpread) );
		particles.TimeScale = effect.SpeedFactor;
		particles.LocalSpace = data.StaysWithBody;

		// A cycle or a mix colours each sprite itself, on the particle threads
		var colours = _cycle is not null || _mix;
		particles.ApplyColor = !colours;
		particles.OnStep = colours ? ColourInCycle : null;
		particles.OnParticleCreated = colours ? CountInCycle : null;
		particles.Collision = false;
		particles.Gradient = new ParticleGradient
		{
			Type = ParticleGradient.ValueType.Range, Evaluation = ParticleGradient.EvaluationType.Life, ConstantA = colour, ConstantB = colourEnd,
		};
		particles.Tint = Color.White;
		particles.ApplyAlpha = true;
		particles.Alpha = new ParticleFloat { Type = ParticleFloat.ValueType.Curve, Evaluation = ParticleFloat.EvaluationType.Life, CurveA = Fade };
		particles.ApplyShape = true;
		particles.Scale = new ParticleFloat
		{
			Type = ParticleFloat.ValueType.Range, Evaluation = ParticleFloat.EvaluationType.Life, ConstantA = data.Size * size,
			ConstantB = (data.SizeEnd ?? data.Size) * size,
		};
		particles.InitialScale = new ParticleFloat( 1f - SizeSpread, 1f + SizeSpread );
		particles.ApplyRotation = true;
		particles.InitialRoll = data.PictureIs == LayerPicture.Slash && data.PictureShape is null ? new ParticleFloat( 0f, 0f )
			: data.PictureShape is not null || data.DrawnPicture is not null || data.PictureIs is LayerPicture.Flame or LayerPicture.Heart or LayerPicture.Droplet
				? new ParticleFloat( -UprightRoll, UprightRoll )
				: new ParticleFloat( 0f, 360f );

		// A sweep turns each sprite over its life: a slash swinging round
		particles.Roll = data.Sweep == 0f
			? new ParticleFloat( 0f, 0f )
			: new ParticleFloat { Type = ParticleFloat.ValueType.Range, Evaluation = ParticleFloat.EvaluationType.Life, ConstantA = 0f, ConstantB = data.Sweep };
		particles.Force = data.Gravity != 0f;
		particles.ForceDirection = Vector3.Down * (data.Gravity * moves);
		particles.ForceSpace = ParticleEffect.SimulationSpace.World;
		particles.Paused = false;

		var shape = data.ShapeIs;
		_emitter = shape switch
		{
			LayerShape.Ring => _ring,
			LayerShape.Box => _box,
			LayerShape.Cone => _cone,
			LayerShape.Line or LayerShape.Shape or LayerShape.Shot => _points,
			_ => _sphere,
		};

		_sphere.Enabled = _emitter == _sphere;
		_ring.Enabled = _emitter == _ring;
		_box.Enabled = _emitter == _box;
		_cone.Enabled = _emitter == _cone;
		_points.Enabled = _emitter == _points;

		// The box and the cone send sprites at the effect's start speed: random ways from a box, out along a cone
		particles.StartVelocity = shape is LayerShape.Box or LayerShape.Cone ? data.Speed * moves : 0f;

		var spread = data.Spread * size;
		switch ( shape )
		{
			case LayerShape.Ring:
				_ring.Radius = spread;
				_ring.Thickness = spread * RingThickness;
				_ring.Flatness = 1f;
				_ring.VelocityFromCenter = data.Speed * moves;
				break;

			case LayerShape.Box:
				_box.Size = spread * 2f;
				_box.OnEdge = false;
				break;

			case LayerShape.Cone:
				_cone.ConeAngle = data.Angle;
				_cone.ConeNear = 0f;
				_cone.ConeFar = MathF.Max( spread, 0.1f );
				_cone.InVolume = true;
				_cone.OnEdge = false;
				break;

			case LayerShape.Line:
				_points.IsLine = true;
				_points.LineStart = ToJoint >= 0 ? Vector3.Zero : Vector3.Backward * spread;
				_points.LineEnd = ToJoint >= 0 ? Vector3.Zero : Vector3.Forward * spread;
				_points.Velocity = data.Speed * moves;
				break;

			case LayerShape.Shot:
				_points.IsLine = true;
				_points.LineStart = Vector3.Zero;
				_points.LineEnd = Vector3.Forward * MathF.Max( spread, 1f );
				_points.Velocity = data.Speed * moves;
				_shotSpacing = MathF.Max( data.Size * size * motion * 0.6f, 0.5f );
				break;

			case LayerShape.Shape:
				_points.IsLine = false;
				_points.Points = FormationOf( Find( shapes, data.DrawnFormation ), data );
				_points.Scale = spread;
				_points.Velocity = data.Speed * moves;
				break;

			default:
				_sphere.Radius = shape == LayerShape.Point ? 0f : spread;
				_sphere.Velocity = data.Speed * moves;
				_sphere.DistanceBias = shape == LayerShape.AroundBody ? AroundBodyBias : Vector3.One;
				break;
		}

		_emitter.Burst = 0f;
		_emitter.Loop = true;
		_emitter.Duration = EmitterLoop;
		_emitter.Delay = 0f;

		_rate = data.Amount * effect.AmountFactor;
		_share = 1f;
		_emitting = _when == LayerWhen.Always;
		_most = (int)MathF.Ceiling( _rate * data.Lifetime * (1f + LifetimeSpread) + _burst ) + 1;
		_cycleCount = Math.Max( 1, _most );
		_colour = Color.White;
		WantedSprites = data.LiveSprites( effect.AmountFactor );

		// A sticker or an emblem has colours of its own and covers what is behind it; it shows a plain circle until it is drawn
		_renderer.Sprite = _drawn is null ? EffectAtlas.SpriteOf( data ) : EffectAtlas.SpriteOf( StandardShape.Circle );
		_renderer.Additive = !data.Solid && _drawn?.HasOwnColours != true;
		_renderer.Alignment = data.AlignIs == LayerAlign.Swing ? ParticleSpriteRenderer.BillboardAlignment.Object : ParticleSpriteRenderer.BillboardAlignment.LookAtCamera;
		_renderer.Lighting = false;
		_renderer.Shadows = false;
		_renderer.Opaque = false;
		_renderer.CameraFadeNear = NearCameraShown ? 0f : NearFadeGone;
		_renderer.CameraFadeFar = NearCameraShown ? 0f : NearFadeFull;
		_renderer.Scale = 1f;
		_renderer.Enabled = true;
	}

	private void ConfigureTrail( Effect effect, EffectLayer data, Color colour, Color colourEnd, float motion )
	{
		var width = data.Size * effect.SizeFactor * motion;
		var widthEnd = (data.SizeEnd ?? data.Size) * effect.SizeFactor * motion;

		var trail = _trail;
		trail.MaxPoints = TrailPoints;
		trail.PointDistance = TrailPointDistance * motion;
		trail.LifeTime = data.Lifetime / MathF.Max( effect.SpeedFactor, 0.01f );
		trail.Width = new Curve( new Curve.Frame( 0f, width ), new Curve.Frame( 1f, widthEnd ) );
		trail.Face = SceneLineObject.FaceMode.Camera;
		trail.Opaque = false;
		trail.CastShadows = false;
		trail.BlendMode = data.Solid ? BlendMode.Normal : BlendMode.Lighten;
		trail.Emitting = _when == LayerWhen.Always;

		// Fading from its colour to its end colour along it, and out at its end; one gradient for each step of opacity
		for ( var shade = 0; shade <= TrailShades; shade++ )
		{
			var gradient = new Gradient();
			gradient.AddColor( 0f, colour );
			gradient.AddColor( 1f, colourEnd );
			gradient.AddAlpha( 0f, (float)shade / TrailShades );
			gradient.AddAlpha( 1f, 0f );
			_trailShades[ shade ] = gradient;
		}

		_trailShade = TrailShades;
		trail.Color = _trailShades[ TrailShades ];
		trail.Enabled = true;
	}

	// Runs every frame for every layer: no allocations
	private void Beat( float beat, float shown )
	{
		Color tint;
		switch ( _rhythm )
		{
			case LayerRhythm.Pulse:
				tint = Level( 1f - _beatAmount * (0.5f - 0.5f * MathF.Cos( beat * MathF.Tau )) );
				break;
			case LayerRhythm.Flicker:
				tint = Level( 1f - _beatAmount * Flicker( beat ) );
				break;
			case LayerRhythm.Rainbow:
				tint = new ColorHsv( beat % 1f * 360f, 1f, 1f, 1f ).ToColor();
				break;
			default:
				tint = Color.White;
				break;
		}

		switch ( Kind )
		{
			case LayerKind.Light:
			{
				var light = (_cycle is not null || _mix ? Times( _colour, ColourAt( 0 ) ) : _colour) * (_level * shown);
				_light.LightColor = Times( light, tint );
				break;
			}

			case LayerKind.Sprite:
			{
				var faded = tint.WithAlpha( shown );
				if ( _cycle is not null || _mix )
				{
					_tint = faded;
				}
				else if ( _rhythm != LayerRhythm.Steady || shown != _lastShown )
				{
					_particles.Tint = faded;
				}

				_lastShown = shown;
				break;
			}
		}
	}

	// Its colour cycle from the layer's colour mode: the colours turned with the effect's, at the effect's speed
	private void UseCycle( Effect effect, EffectLayer data )
	{
		var mode = data.ColorModeIs;
		_cycle = mode switch
		{
			LayerColorMode.Rain => ColorCycle.Kind.Rain,
			LayerColorMode.Cycl => ColorCycle.Kind.Cycl,
			LayerColorMode.Cyln => ColorCycle.Kind.Cyln,
			_ => null,
		};

		_mix = mode == LayerColorMode.Mix;
		_tint = Color.White;
		_spawned = 0;
		_cycleCount = 1;
		if ( _cycle is null && !_mix )
			return;

		var colours = data.CycleColours();
		_palette = new Color[ colours.Count ];
		for ( var i = 0; i < colours.Count; i++ )
			_palette[ i ] = Hued( colours[ i ], effect.Hue );

		_cycleStart = RealTime.Now;
		_cycleSpeed = effect.SpeedFactor;
		_cycleMs = 0;
	}

	// Colours a sprite as the cycle has it now. Runs on the particle threads for every sprite as it steps
	private void ColourInCycle( Particle particle, float age )
	{
		particle.Color = Times( ColourAt( (int)particle.Radius ), _tint );
	}

	// Gives a sprite its place in the cycle's count as it appears, kept in its collision radius, which effects never use
	// (they don't collide). On the main thread
	private void CountInCycle( Particle particle )
	{
		particle.Radius = _spawned;
		_spawned = (_spawned + 1) % Math.Max( 1, _mix ? _palette.Length : _cycleCount );
	}

	// The colour the cycle gives element i (a sprite's place in the count) this frame: reads only what the main thread
	// set before the particles step, and allocates nothing
	private Color ColourAt( int i )
	{
		var palette = _palette;
		if ( palette.Length == 0 )
			return Color.White;

		if ( _mix )
			return palette[ i % palette.Length ];

		if ( _cycle == ColorCycle.Kind.Cyln )
			return ColorCycle.IsScannerHighlight( _cycleMs, i, _cycleCount ) ? palette[ 0 ] : palette[ 1 ];

		var index = ColorCycle.PaletteIndex( _cycle.GetValueOrDefault(), _cycleMs, i, _cycleCount, palette.Length );
		return index >= 0 ? palette[ index ] : Color.White;
	}

	// The way a layer faces as a turn from forwards: a cone opens, a line runs and a shape looks along its forward
	private static Rotation FacingOf( LayerFacing facing ) => facing switch
	{
		LayerFacing.Up => Rotation.FromPitch( -90f ),
		LayerFacing.Down => Rotation.FromPitch( 90f ),
		LayerFacing.Back => Rotation.FromYaw( 180f ),
		LayerFacing.Left => Rotation.FromYaw( 90f ),
		LayerFacing.Right => Rotation.FromYaw( -90f ),
		_ => Rotation.Identity,
	};

	// A smooth wander, each way out of step with the others, within a ball of 1
	private static Vector3 Wobble( float beat )
	{
		var t = beat * MathF.Tau;
		return new Vector3( MathF.Sin( t ) * 0.6f, MathF.Sin( t * 1.3f + 1.7f ) * 0.6f, MathF.Sin( t * 0.7f + 0.4f ) * 0.5f );
	}

	private static float Approach( float value, float target, float step )
	{
		return value < target ? MathF.Min( value + step, target ) : MathF.Max( value - step, target );
	}

	// Colours times each other, keeping the second's opacity (a fade's)
	private static Color Times( Color a, Color b ) => new( a.r * b.r, a.g * b.g, a.b * b.b, b.a );

	private static Color Level( float level ) => new( level, level, level, 1f );

	// Uneven, 0 to 1: three waves out of step, like a flame
	private static float Flicker( float beat )
	{
		var wave = 0.5f * MathF.Sin( beat * MathF.Tau ) + 0.3f * MathF.Sin( beat * MathF.Tau * 2.3f + 1.3f ) + 0.2f * MathF.Sin( beat * MathF.Tau * 5.1f + 0.7f );
		return 0.5f + 0.5f * wave;
	}

	// A colour turned round the colour wheel by hue (0 to 1)
	private static Color Hued( string hex, float hue )
	{
		var colour = Color.Parse( hex ) ?? Color.White;
		if ( hue == 0f )
			return colour;

		var hsv = colour.ToHsv();
		return hsv.WithHue( (hsv.Hue + hue * 360f) % 360f ).ToColor();
	}

	private static Color Grey( Color colour )
	{
		var light = colour.r * 0.3f + colour.g * 0.59f + colour.b * 0.11f;
		return new Color( light, light, light, colour.a );
	}
}
