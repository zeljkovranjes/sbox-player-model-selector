using System;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// What a body is doing, for layers that show only while it is so or for a moment when it happens (see
/// <see cref="LayerWhen"/>), read once a frame from what every client already has of it: the animation the game drives
/// the citizen with (on the ground, crouching, holding a weapon, jumping, shooting or swinging, reloading, its footsteps),
/// what it holds (a weapon drawn or put away), how it moves (moving, jumping, landing, hitting something hard), its
/// health (hurt) and how the body came to be (a respawn, or a ragdoll's death; see <see cref="EffectWearer"/>). A shot's
/// path is worked out by the rack that wears the effects (see <see cref="Shot"/>). Nothing more is sent over the network.
/// Read before the animation runs, as it clears the moments it is told of; a moment waits until the layers have seen it,
/// however rarely a far body is posed. Each moment happens at most so often (<see cref="ShotsPerSecond"/>,
/// <see cref="MomentsPerSecond"/>), so a player flipping their animation can't flood anyone with effects. A new moment is
/// one more <see cref="LayerWhen"/> and the line in <see cref="Read"/> that notices it. Runs every frame for every body
/// wearing effects: no allocations.
/// </summary>
internal sealed class EffectSignals
{
	/// <summary>How often a shot or swing can count, and any other moment, a second.</summary>
	internal const float ShotsPerSecond = 20f;
	internal const float MomentsPerSecond = 10f;

	// The citizen's animation parameters the game sets on every client
	private const string GroundedParameter = "b_grounded";
	private const string CrouchParameter = "duck";
	private const string HoldParameter = "holdtype";
	private const string JumpParameter = "b_jump";
	private const string AttackParameter = "b_attack";
	private const string ReloadParameter = "b_reload";

	// Crouching from halfway down
	private const float CrouchedFrom = 0.5f;

	// Moving: faster than a slow walk, in units a second, over the last moment
	private const float MovingSpeed = 40f;

	// How quickly the speed read follows the body: a frame's jitter doesn't count
	private const float SpeedFollow = 12f;

	// Off the ground and going up this fast, soon after leaving it, is a jump, even without the game saying so
	private const float JumpSpeed = 150f;
	private const float JumpWindow = 0.25f;

	// Movement is measured over at least this long: a body moved in steps (physics, the network) doesn't look jerky
	private const float SampleSeconds = 0.05f;

	// A landing counts after this long in the air: walking down a step isn't one
	private const float LandAfter = 0.2f;

	// Losing this much speed faster than a body slows on its own (units a second, each second) is hitting something hard
	private const float ImpactSpeedLost = 400f;
	private const float SlowingDown = 2000f;

	// Moving further than this in a frame is a teleport or a respawn, not movement
	private const float MostStep = 200f;

	// Losing at least this much health is getting hurt
	private const float HurtFrom = 0.5f;

	private static readonly int Moments = Enum.GetValues<LayerWhen>().Length;

	/// <summary>
	/// The last shot this body fired, as this client worked it out: from the weapon's muzzle along where the player aims,
	/// to what it hit (if anything) or as far as it reaches. Set by the rack when a shot happens.
	/// </summary>
	internal struct ShotPath
	{
		public Vector3 Origin;
		public Vector3 Direction;
		public bool Hit;

		/// <summary>How the weapon's tip was moving as it fired or swung, in units a second: the way a swing sweeps.</summary>
		public Vector3 Swing;
		public Vector3 HitPosition;
		public Vector3 HitNormal;
		public float Distance;

		/// <summary>Where it ended: what it hit, or as far as it reached.</summary>
		public readonly Vector3 End => Hit ? HitPosition : Origin + Direction * Distance;
	}

	private Vector3 _position;
	private Vector3 _velocity;
	private bool _hasPosition;
	private Vector3 _sampleStep;
	private float _sampleTime;
	private float _peakSpeed;
	private float _health = float.NaN;
	private float _inAir;
	private float _jumpWindow;
	private int _moments;
	private int _holdType;
	private GameObject _heldRoot;

	// The feet whose steps happened since the layers last saw (a bit each, left first), and the citizen whose footsteps
	// are listened to, with what listens, made once
	private int _feet;
	private SkinnedModelRenderer _listening;
	private Action<SceneModel.FootstepEvent> _onFootstep;

	// When each moment last counted, by its place in LayerWhen
	private readonly float[] _lastMoment = new float[ Moments ];

	/// <summary>Whether it moves, stands on the ground, crouches or holds a weapon, as last read.</summary>
	internal bool IsMoving { get; private set; }
	internal bool IsGrounded { get; private set; }
	internal bool IsCrouching { get; private set; }
	internal bool IsHolding { get; private set; }

	/// <summary>How fast it moves, in units a second.</summary>
	internal float Speed { get; private set; }

	/// <summary>Who wears the effects: their health, aim and how the body came to be (see <see cref="EffectWearer"/>).</summary>
	internal EffectWearer Wearer
	{
		// Read through: a hot reload keeps this object but not fields added since
		get => _wearer ?? EffectWearer.None;
		set => _wearer = value;
	}

	private EffectWearer _wearer;

	/// <summary>The last shot fired (see <see cref="ShotPath"/>).</summary>
	internal ShotPath Shot;

	/// <summary>
	/// Reads what <paramref name="citizen"/> is doing now, <paramref name="delta"/> seconds after the last read, holding
	/// <paramref name="held"/> (the object of the weapon held, or null). Moments add up until <see cref="Seen"/>.
	/// </summary>
	internal void Read( SkinnedModelRenderer citizen, float delta, GameObject held )
	{
		if ( !citizen.IsValid() )
			return;

		Listen( citizen );

		var now = RealTime.Now;
		var wasGrounded = IsGrounded;
		var wasMoving = IsMoving;
		var wasCrouching = IsCrouching;
		IsGrounded = citizen.GetBool( GroundedParameter );
		IsCrouching = citizen.GetFloat( CrouchParameter ) >= CrouchedFrom;
		var holdType = citizen.GetInt( HoldParameter );
		var wasHolding = IsHolding;
		var heldBefore = _heldRoot;
		IsHolding = holdType > 0 || held.IsValid();
		_heldRoot = held.IsValid() ? held : null;
		var swapped = holdType > 0 && _holdType > 0 && holdType != _holdType || _heldRoot is not null && heldBefore is not null && _heldRoot != heldBefore;
		_holdType = holdType;

		// Velocity over the last sample; a teleport or a respawn starts measuring again
		var position = citizen.WorldPosition;
		var step = position - _position;
		var first = !_hasPosition;
		_position = position;
		_hasPosition = true;
		if ( !first && step.Length <= MostStep )
		{
			_sampleStep += step;
			_sampleTime += delta;
		}

		if ( _sampleTime >= SampleSeconds )
		{
			_velocity = _sampleStep / _sampleTime;
			_sampleStep = Vector3.Zero;
			_sampleTime = 0f;
		}

		var flat = _velocity.WithZ( 0f ).Length;
		Speed += (flat - Speed) * MathF.Min( 1f, delta * SpeedFollow );
		IsMoving = Speed >= MovingSpeed;

		// The fastest it went lately, falling away only as fast as a body slows down by itself
		var speed = _velocity.Length;
		var impact = _peakSpeed - speed >= ImpactSpeedLost;
		_peakSpeed = impact ? speed : MathF.Max( speed, _peakSpeed - SlowingDown * delta );

		var inAirBefore = _inAir;
		_inAir = IsGrounded ? 0f : _inAir + delta;
		_jumpWindow = wasGrounded && !IsGrounded ? JumpWindow : MathF.Max( 0f, _jumpWindow - delta );
		var jumped = citizen.GetBool( JumpParameter ) || _jumpWindow > 0f && _velocity.z >= JumpSpeed;
		if ( jumped )
			_jumpWindow = 0f;

		// The first read only learns how things are: nothing happened yet, unless the body is new because its player just
		// spawned again, or died
		if ( first )
		{
			Happened( LayerWhen.OnRespawn, EffectWearer.IsFresh( Wearer.RespawnedAt, now ), now );
			Happened( LayerWhen.OnDeath, EffectWearer.IsFresh( Wearer.DiedAt, now ), now );
			return;
		}

		Happened( LayerWhen.OnJump, jumped, now );
		Happened( LayerWhen.OnLand, IsGrounded && !wasGrounded && inAirBefore >= LandAfter, now );
		Happened( LayerWhen.OnStartMoving, IsMoving && !wasMoving, now );
		Happened( LayerWhen.OnStopMoving, !IsMoving && wasMoving, now );
		Happened( LayerWhen.OnCrouch, IsCrouching && !wasCrouching, now );
		Happened( LayerWhen.OnUncrouch, !IsCrouching && wasCrouching, now );
		Happened( LayerWhen.OnShoot, citizen.GetBool( AttackParameter ), now );
		Happened( LayerWhen.OnImpact, impact, now );
		Happened( LayerWhen.OnReload, citizen.GetBool( ReloadParameter ), now );
		Happened( LayerWhen.OnEquip, IsHolding && (!wasHolding || swapped), now );
		Happened( LayerWhen.OnHolster, !IsHolding && wasHolding, now );

		if ( Wearer.Health is { } health )
		{
			var left = health();
			Happened( LayerWhen.OnHurt, left <= _health - HurtFrom, now );
			_health = left;
		}
	}

	/// <summary>Whether <paramref name="when"/> is so now (or, for a moment, happened since the layers last saw).</summary>
	internal bool Has( LayerWhen when ) => when switch
	{
		LayerWhen.Always => true,
		LayerWhen.Moving => IsMoving,
		LayerWhen.Grounded => IsGrounded,
		LayerWhen.InAir => !IsGrounded,
		LayerWhen.Crouching => IsCrouching,
		LayerWhen.HoldingWeapon => IsHolding,
		_ => (_moments & Bit( when )) != 0,
	};

	/// <summary>
	/// Whether a foot stepped since the layers last saw: <paramref name="foot"/> 0 the left, 1 the right, anything else
	/// either.
	/// </summary>
	internal bool HasFootstep( int foot ) => foot is 0 or 1 ? (_feet & (1 << foot)) != 0 : Has( LayerWhen.OnFootstep );

	/// <summary>The shot just worked out hit something (see <see cref="Shot"/>): counted like any moment.</summary>
	internal void Hit()
	{
		Happened( LayerWhen.OnHit, true, RealTime.Now );
	}

	/// <summary>The layers have seen the moments so far: they don't count again.</summary>
	internal void Seen()
	{
		_moments = 0;
		_feet = 0;
	}

	/// <summary>Stops listening to the citizen's footsteps: what wears the effects is let go of.</summary>
	internal void Release()
	{
		if ( _listening.IsValid() && _onFootstep is not null )
			_listening.OnFootstepEvent -= _onFootstep;

		_listening = null;
	}

	// The animation tells of each footstep as it happens, not as a parameter read each frame
	private void Listen( SkinnedModelRenderer citizen )
	{
		if ( _listening == citizen )
			return;

		Release();
		_onFootstep ??= OnFootstep;
		citizen.OnFootstepEvent += _onFootstep;
		_listening = citizen;
	}

	private void OnFootstep( SceneModel.FootstepEvent step )
	{
		var now = RealTime.Now;
		var before = _lastMoment[ (int)LayerWhen.OnFootstep ];
		Happened( LayerWhen.OnFootstep, true, now );
		if ( _lastMoment[ (int)LayerWhen.OnFootstep ] != before && step.FootId is 0 or 1 )
			_feet |= 1 << step.FootId;
	}

	// A moment counts unless it already did too recently for its rate
	private void Happened( LayerWhen when, bool happened, float now )
	{
		if ( !happened )
			return;

		var most = when == LayerWhen.OnShoot ? ShotsPerSecond : MomentsPerSecond;
		ref var last = ref _lastMoment[ (int)when ];
		if ( last > 0f && now - last < 1f / most )
			return;

		last = now;
		_moments |= Bit( when );
	}

	private static int Bit( LayerWhen when ) => 1 << (int)when;
}
