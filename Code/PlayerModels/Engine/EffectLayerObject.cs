using System;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The object one layer of a worn effect shows on (see <see cref="EffectLayer"/>): a particle effect with its emitter and
/// sprite renderer, or a light that casts no shadows. Made once and kept for reuse (see <see cref="EffectSchedule"/>),
/// set up again for each layer it shows. Each frame it is placed on its joint and beats its rhythm, and a colour cycle
/// colours each of its sprites (see <see cref="ColorCycle"/>), all without allocating.
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

	// Sprites of a picture with a way up stay near upright; the rest turn at random
	private const float UprightRoll = 8f;

	// An emitter counts what it made from when it last started, starting again each loop: long, so that never shows
	private const float EmitterLoop = 600f;

	// Each layer beats this far behind the one before it, so layers alike don't beat in step
	private const float PhaseStep = 0.618f;

	private const float MillisecondsPerSecond = 1000f;

	// Sprites fade in over the first part of their life and out over the last, so none pops in or out
	private static readonly Curve Fade = new( new Curve.Frame( 0f, 0f ), new Curve.Frame( 0.15f, 1f ), new Curve.Frame( 0.6f, 1f ), new Curve.Frame( 1f, 0f ) );

	/// <summary>The object itself.</summary>
	internal GameObject Root { get; }

	/// <summary>Whether it is a light; otherwise it shows sprites.</summary>
	internal bool IsLight { get; }

	/// <summary>The <see cref="Effect.Joint"/> it comes from, as set up last.</summary>
	internal int Joint { get; private set; }

	/// <summary>How many sprites it has out at once, all shown.</summary>
	internal float WantedSprites { get; private set; }

	private readonly ParticleEffect _particles;
	private readonly ParticleSphereEmitter _sphere;
	private readonly ParticleRingEmitter _ring;
	private readonly ParticleSpriteRenderer _renderer;
	private readonly PointLight _light;
	private ParticleEmitter _emitter;

	// Where it sits from its joint, its rhythm, its colour (a light's at full brightness, a sprite's tint), how many
	// sprites it makes a second and how many it may have out
	private Vector3 _offset;
	private LayerRhythm _rhythm;
	private float _beatSpeed;
	private float _beatAmount;
	private float _phase;
	private Color _colour;
	private float _rate;
	private int _most;

	// A colour cycle: which (none for fixed colours), its colours, how many sprites it counts through, when it started and
	// how fast it runs, how far it is this frame, the rhythm's light or dark over it, and the next sprite's place in the
	// count. The particle threads colour the sprites from these, so only the main thread writes them, before they step
	private ColorCycle.Kind? _cycle;
	private Color[] _palette = Array.Empty<Color>();
	private int _cycleCount = 1;
	private float _cycleStart;
	private float _cycleSpeed = 1f;
	private long _cycleMs;
	private Color _tint = Color.White;
	private int _spawned;

	private EffectLayerObject( GameObject root, bool isLight )
	{
		Root = root;
		IsLight = isLight;
		if ( isLight )
		{
			_light = root.AddComponent<PointLight>();
			_light.Shadows = false;
			return;
		}

		_particles = root.AddComponent<ParticleEffect>();
		_sphere = root.AddComponent<ParticleSphereEmitter>();
		_ring = root.AddComponent<ParticleRingEmitter>();
		_renderer = root.AddComponent<ParticleSpriteRenderer>();
	}

	/// <summary>A new object, in the scene pushed now, switched off for <see cref="Configure"/> to set up.</summary>
	internal static EffectLayerObject Make( bool isLight )
	{
		var root = new GameObject( false, isLight ? "Effect light" : "Effect sprites" );
		root.NetworkMode = NetworkMode.Never;
		root.Flags |= GameObjectFlags.NotSaved;
		return new EffectLayerObject( root, isLight );
	}

	/// <summary>
	/// Sets it up as <paramref name="data"/> of <paramref name="effect"/> says, as the <paramref name="index"/>th layer of
	/// the body; <paramref name="motion"/> is how many world units a citizen's unit is on this body.
	/// </summary>
	internal void Configure( Effect effect, EffectLayer data, int index, float motion )
	{
		Joint = (int)effect.JointOf( data );
		_offset = new Vector3( data.Forward, data.Left, data.Up );
		_rhythm = data.RhythmIs;
		_beatSpeed = data.RhythmSpeed * effect.SpeedFactor;
		_beatAmount = data.RhythmAmount;
		_phase = index * PhaseStep;
		UseCycle( effect, data );

		var colour = Hued( data.Color, effect.Hue );
		var colourEnd = data.ColorEnd is null ? colour : Hued( data.ColorEnd, effect.Hue );

		// A rainbow beats through the colours itself, so under it a layer is only light or dark
		if ( _rhythm == LayerRhythm.Rainbow )
		{
			colour = Grey( colour );
			colourEnd = Grey( colourEnd );
		}

		if ( IsLight )
		{
			// A light in a cycle takes its colours from it
			var brightness = data.Amount * effect.AmountFactor;
			colour = _cycle is null ? colour : Color.White;
			_colour = new Color( colour.r * brightness, colour.g * brightness, colour.b * brightness, 1f );
			_light.LightColor = _colour;
			_light.Radius = data.Size * effect.SizeFactor * motion;
			_light.Shadows = false;
			WantedSprites = 0f;
			return;
		}

		ConfigureSprites( effect, data, colour, colourEnd, motion );
	}

	/// <summary>
	/// Shows <paramref name="share"/> of its sprites (starting the emitter again when <paramref name="rates"/>: the share
	/// changed), paused or not; a light shows while <paramref name="lit"/>.
	/// </summary>
	internal void Share( float share, bool rates, bool paused, bool lit )
	{
		if ( IsLight )
		{
			_light.Enabled = lit;
			return;
		}

		_particles.Paused = paused;
		if ( !rates )
			return;

		// An emitter makes a whole number of sprites a second: any share of one makes at least one
		var rate = _rate * share;
		_emitter.Rate = rate > 0f ? MathF.Max( 1f, rate ) : 0f;
		_particles.MaxParticles = Math.Max( 1, (int)MathF.Ceiling( _most * share ) );
		_emitter.ResetEmitter();
	}

	/// <summary>Puts it on its joint, <paramref name="joint"/> as placed this frame, and beats its rhythm at <paramref name="now"/>.</summary>
	internal void Place( Transform joint, float now )
	{
		var beat = now * _beatSpeed + _phase;
		if ( _cycle is not null )
			_cycleMs = (long)((now - _cycleStart) * MillisecondsPerSecond * _cycleSpeed);

		var turn = _rhythm == LayerRhythm.Spin ? Rotation.FromAxis( Vector3.Up, beat % 1f * 360f ) : Rotation.Identity;
		Root.WorldTransform = joint.ToWorld( new Transform( _offset, turn ) );
		Beat( beat );
	}

	private void ConfigureSprites( Effect effect, EffectLayer data, Color colour, Color colourEnd, float motion )
	{
		var size = effect.SizeFactor;
		var moves = motion * size;

		var particles = _particles;
		particles.Lifetime = new ParticleFloat( data.Lifetime * (1f - LifetimeSpread), data.Lifetime * (1f + LifetimeSpread) );
		particles.TimeScale = effect.SpeedFactor;
		particles.LocalSpace = data.Follow ? 1f : 0f;

		// A cycle colours each sprite itself, on the particle threads
		particles.ApplyColor = _cycle is null;
		particles.OnStep = _cycle is null ? null : ColourInCycle;
		particles.OnParticleCreated = _cycle is null ? null : CountInCycle;
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
		particles.InitialRoll = data.PictureIs is LayerPicture.Flame or LayerPicture.Heart or LayerPicture.Droplet
			? new ParticleFloat( -UprightRoll, UprightRoll )
			: new ParticleFloat( 0f, 360f );
		particles.Force = data.Gravity != 0f;
		particles.ForceDirection = Vector3.Down * (data.Gravity * moves);
		particles.ForceSpace = ParticleEffect.SimulationSpace.World;
		particles.Paused = false;

		var shape = data.ShapeIs;
		var isRing = shape == LayerShape.Ring;
		_ring.Enabled = isRing;
		_sphere.Enabled = !isRing;
		_emitter = isRing ? _ring : _sphere;

		if ( isRing )
		{
			_ring.Radius = data.Spread * size;
			_ring.Thickness = data.Spread * size * RingThickness;
			_ring.Flatness = 1f;
			_ring.VelocityFromCenter = data.Speed * moves;
		}
		else
		{
			_sphere.Radius = shape == LayerShape.Point ? 0f : data.Spread * size;
			_sphere.Velocity = data.Speed * moves;
			_sphere.DistanceBias = shape == LayerShape.AroundBody ? AroundBodyBias : Vector3.One;
		}

		_emitter.Burst = 0f;
		_emitter.Loop = true;
		_emitter.Duration = EmitterLoop;
		_emitter.Delay = 0f;

		_rate = data.Amount * effect.AmountFactor;
		_most = (int)MathF.Ceiling( _rate * data.Lifetime * (1f + LifetimeSpread) ) + 1;
		_cycleCount = Math.Max( 1, _most );
		_colour = Color.White;
		WantedSprites = data.LiveSprites( effect.AmountFactor );

		_renderer.Sprite = EffectAtlas.SpriteOf( data.PictureIs );
		_renderer.Additive = !data.Solid;
		_renderer.Lighting = false;
		_renderer.Shadows = false;
		_renderer.Opaque = false;
		_renderer.Scale = 1f;
		_renderer.Enabled = true;
	}

	// Runs every frame for every layer: no allocations
	private void Beat( float beat )
	{
		var changes = true;
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
				changes = false;
				break;
		}

		if ( IsLight )
		{
			if ( _cycle is not null )
				_light.LightColor = Times( Times( _colour, ColourAt( 0 ) ), tint );
			else if ( changes )
				_light.LightColor = Times( _colour, tint );
		}
		else if ( _cycle is not null )
		{
			_tint = tint;
		}
		else if ( changes )
		{
			_particles.Tint = tint;
		}
	}

	// Its colour cycle from the layer's colour mode: the colours turned with the effect's, at the effect's speed
	private void UseCycle( Effect effect, EffectLayer data )
	{
		_cycle = data.ColorModeIs switch
		{
			LayerColorMode.Rain => ColorCycle.Kind.Rain,
			LayerColorMode.Cycl => ColorCycle.Kind.Cycl,
			LayerColorMode.Cyln => ColorCycle.Kind.Cyln,
			_ => null,
		};

		_tint = Color.White;
		_spawned = 0;
		_cycleCount = 1;
		if ( _cycle is null )
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
		_spawned = (_spawned + 1) % Math.Max( 1, _cycleCount );
	}

	// The colour the cycle gives element i (a sprite's place in the count) this frame: reads only what the main thread
	// set before the particles step, and allocates nothing
	private Color ColourAt( int i )
	{
		var palette = _palette;
		if ( _cycle == ColorCycle.Kind.Cyln )
			return ColorCycle.IsScannerHighlight( _cycleMs, i, _cycleCount ) ? palette[ 0 ] : palette[ 1 ];

		var index = ColorCycle.PaletteIndex( _cycle.GetValueOrDefault(), _cycleMs, i, _cycleCount, palette.Length );
		return index >= 0 ? palette[ index ] : Color.White;
	}

	private static Color Times( Color a, Color b ) => new( a.r * b.r, a.g * b.g, a.b * b.b, 1f );

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
