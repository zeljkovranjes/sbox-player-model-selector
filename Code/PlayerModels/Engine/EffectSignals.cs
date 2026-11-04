using System;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// What a body is doing, for layers that show only while it is so or for a moment when it happens (see
/// <see cref="LayerWhen"/>), read once a frame from what every client already has of it: the animation the game drives
/// the citizen with (on the ground, crouching, holding a weapon, jumping, shooting or swinging), how it moves (moving,
/// jumping, landing, hitting something hard) and its health (hurt). Nothing more is sent over the network. Read before
/// the animation runs, as it clears the moments it is told of; a moment waits until the layers have seen it, however
/// rarely a far body is posed. Each moment happens at most so often (<see cref="ShotsPerSecond"/>,
/// <see cref="MomentsPerSecond"/>), so a player flipping their animation can't flood anyone with effects. A new moment is
/// one more <see cref="LayerWhen"/> and the line in <see cref="Read"/> that notices it. Runs every frame for every body
/// wearing effects: no allocations.
/// </summary>
internal sealed partial class EffectSignals
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

	// When each moment last counted, by its place in LayerWhen
	private readonly float[] _lastMoment = new float[ Moments ];

	/// <summary>Whether it moves, stands on the ground, crouches or holds a weapon, as last read.</summary>
	internal bool IsMoving { get; private set; }
	internal bool IsGrounded { get; private set; }
	internal bool IsCrouching { get; private set; }
	internal bool IsHolding { get; private set; }

	/// <summary>How fast it moves, in units a second.</summary>
	internal float Speed { get; private set; }

	/// <summary>Which way and how fast it moves, in the world, in units a second.</summary>
	internal Vector3 Velocity => _velocity;

	/// <summary>Where it reads its health from: a player's own, or nothing for a body without (it is never hurt).</summary>
	internal Func<float> Health { get; set; }

	/// <summary>
	/// Reads what <paramref name="citizen"/> is doing now, <paramref name="delta"/> seconds after the last read. Moments
	/// add up until <see cref="Seen"/>.
	/// </summary>
	internal void Read( SkinnedModelRenderer citizen, float delta )
	{
		if ( !citizen.IsValid() )
			return;

		var now = RealTime.Now;
		var wasGrounded = IsGrounded;
		var wasMoving = IsMoving;
		var wasCrouching = IsCrouching;
		IsGrounded = citizen.GetBool( GroundedParameter );
		IsCrouching = citizen.GetFloat( CrouchParameter ) >= CrouchedFrom;
		IsHolding = citizen.GetInt( HoldParameter ) > 0;

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
		_impactLost = _peakSpeed - speed;
		var impact = _impactLost >= ImpactSpeedLost;
		_peakSpeed = impact ? speed : MathF.Max( speed, _peakSpeed - SlowingDown * delta );

		_fallSpeed = IsGrounded ? _fallSpeed : MathF.Max( _fallSpeed, -_velocity.z );
		var inAirBefore = _inAir;
		_inAir = IsGrounded ? 0f : _inAir + delta;
		_jumpWindow = wasGrounded && !IsGrounded ? JumpWindow : MathF.Max( 0f, _jumpWindow - delta );
		var jumped = citizen.GetBool( JumpParameter ) || _jumpWindow > 0f && _velocity.z >= JumpSpeed;
		if ( jumped )
			_jumpWindow = 0f;

		// The first read only learns how things are: nothing happened yet
		if ( first )
			return;

		Happened( LayerWhen.OnJump, jumped, now );
		if ( Happened( LayerWhen.OnLand, IsGrounded && !wasGrounded && inAirBefore >= LandAfter, now ) )
			_landSpeed = _fallSpeed;

		if ( IsGrounded )
			_fallSpeed = 0f;

		Happened( LayerWhen.OnStartMoving, IsMoving && !wasMoving, now );
		Happened( LayerWhen.OnStopMoving, !IsMoving && wasMoving, now );
		Happened( LayerWhen.OnCrouch, IsCrouching && !wasCrouching, now );
		Happened( LayerWhen.OnUncrouch, !IsCrouching && wasCrouching, now );
		Happened( LayerWhen.OnShoot, citizen.GetBool( AttackParameter ), now );
		if ( Happened( LayerWhen.OnImpact, impact, now ) )
			_impactSpeed = _impactLost;


		if ( Health is { } health )
		{
			var left = health();
			if ( Happened( LayerWhen.OnHurt, left <= _health - HurtFrom, now ) )
				_damage = _health - left;

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

	/// <summary>The layers have seen the moments so far: they don't count again.</summary>
	internal void Seen()
	{
		_moments = 0;
	}

	// A moment counts unless it already did too recently for its rate; true when it counted
	private bool Happened( LayerWhen when, bool happened, float now )
	{
		if ( !happened )
			return false;

		var most = when == LayerWhen.OnShoot ? ShotsPerSecond : MomentsPerSecond;
		ref var last = ref _lastMoment[ (int)when ];
		if ( last > 0f && now - last < 1f / most )
			return false;

		last = now;
		_moments |= Bit( when );
		return true;
	}

	private static int Bit( LayerWhen when ) => 1 << (int)when;
}
