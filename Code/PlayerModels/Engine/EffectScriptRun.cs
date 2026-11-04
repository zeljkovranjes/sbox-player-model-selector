using System;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// One worn effect's script while it shows on a body (see <see cref="Effect.Script"/>): built once when the effect is,
/// then each frame the events that happened to the body run, then its <c>OnUpdate</c>, and its layers take the fields
/// those changed; each layer's particle function runs for its sprites as the layer is placed (see
/// <see cref="EffectLayerObject"/>). Every run is held to its instruction budget, the body's share of a frame and the
/// frame's for every body together, and a run that faults stops there and changes nothing more. Each event happens at
/// most as often as <see cref="EffectSignals"/> lets it, on every client. Runs every frame: no allocations.
/// </summary>
internal sealed class EffectScriptRun : IScriptHost
{
	/// <summary>
	/// The most instructions one body's scripts run in a frame (events, updates and particles together), and every
	/// body's on this machine: past them, what is left waits for the next frame and keeps what it showed.
	/// </summary>
	internal const int BodyInstructions = 60_000;
	internal const int FrameInstructions = 600_000;

	// The most sprites one Emit or EmitAt asks for (and a layer's burst is the cap of what a moment brings anyway)
	private const int MostEmitted = (int)EffectLayer.MaxBurst;

	// What each moment a layer can show on (see LayerWhen) is to a script: the event of the same name, or none
	private static readonly ScriptEvent[] MomentEvents = Moments();

	// Every body's instructions left this frame, and the frame they were counted for
	private static int _frameLeft;
	private static float _frameCountedAt = -1f;

	/// <summary>The script's machine, built into the effect shown.</summary>
	internal ScriptMachine Machine { get; }

	/// <summary>What runs read and write: the body's state, filled each frame, then each sprite's.</summary>
	internal ScriptIO IO { get; } = new();

	/// <summary>Which of the body's effects it runs, by place among those shown.</summary>
	internal int Effect { get; }

	// The body's instructions left this frame (shared by all its runs), and the frame they were counted for
	private readonly BodyBudget _body;

	// The layers it drives, by their place in the script's build; null for one not shown (held back by the budget)
	private readonly EffectLayerObject[] _layers;

	private readonly int _key;
	private float _started;
	private bool _wasShown;

	/// <summary>
	/// A shot as every client works it out for itself (see <see cref="EffectRack"/>): from the weapon's muzzle along where
	/// the body aims, to what it hit or as far as it reaches.
	/// </summary>
	internal struct ShotPath
	{
		public Vector3 Origin;
		public Vector3 Direction;
		public bool Hit;
		public Vector3 HitPosition;
		public Vector3 HitNormal;
		public float Distance;
	}

	/// <summary>Whether it handles shots, hits or kills: the body's shots are traced only for those that do.</summary>
	internal bool WantsShots { get; }

	/// <summary>The instructions a body's runs share each frame.</summary>
	internal sealed class BodyBudget
	{
		public int Left;
		public float CountedAt = -1f;
	}

	internal EffectScriptRun( ScriptMachine machine, int effect, string key, BodyBudget body )
	{
		Machine = machine;
		Effect = effect;
		_body = body;
		_layers = new EffectLayerObject[ machine.LayerCount ];
		_key = key is null ? -1 : Math.Max( -1, IndexOf( Core.Effect.Keys, key ) );
		WantsShots = machine.Program.Handles( ScriptEvent.Shoot ) || machine.Program.Handles( ScriptEvent.Hit ) || machine.Program.Handles( ScriptEvent.Kill );
		_started = RealTime.Now;
	}

	/// <summary>Drives <paramref name="layer"/> as layer <paramref name="scriptLayer"/> of the script's build.</summary>
	internal void Drive( int scriptLayer, EffectLayerObject layer )
	{
		if ( scriptLayer >= 0 && scriptLayer < _layers.Length )
			_layers[ scriptLayer ] = layer;
	}

	/// <summary>
	/// One frame of it, <paramref name="delta"/> seconds after the last: fills what the body is doing from
	/// <paramref name="signals"/> (<paramref name="body"/> is how the body faces, <paramref name="anchor"/> where the effect
	/// is), runs <c>OnSpawn</c> and <c>OnKey</c> when it starts showing (<paramref name="shown"/>: its key's gate is up),
	/// the events that happened (a shot being <paramref name="shot"/>), then <c>OnUpdate</c>, and gives its layers what
	/// those changed.
	/// </summary>
	internal void Step( EffectSignals signals, Rotation body, Vector3 anchor, float anchorSpeed, float delta, bool shown, in ShotPath shot )
	{
		var now = RealTime.Now;
		Fill( signals, body, anchor, anchorSpeed, delta, now );

		if ( shown && !_wasShown )
		{
			_started = now;
			Fill( signals, body, anchor, anchorSpeed, delta, now );
			Event( ScriptEvent.Spawn );
			if ( _key >= 0 )
			{
				IO.Set( ScriptInput.Key, _key );
				Event( ScriptEvent.Key );
			}
		}

		_wasShown = shown;
		if ( !shown )
			return;

		for ( var when = (int)LayerWhen.OnJump; when < MomentEvents.Length; when++ )
		{
			var scriptEvent = MomentEvents[ when ];
			if ( scriptEvent == ScriptEvent.None || !signals.Has( (LayerWhen)when ) )
				continue;

			IO.Set( ScriptInput.Amount, signals.AmountOf( (LayerWhen)when ) );
			if ( scriptEvent == ScriptEvent.Shoot )
				Shoot( shot );
			else
				Event( scriptEvent );
		}

		IO.Set( ScriptInput.Dt, delta );
		Event( ScriptEvent.Update );
		ApplyLive();
	}

	/// <summary>
	/// Runs layer <paramref name="layer"/>'s particle function for the sprite whose inputs and outputs <see cref="IO"/>
	/// holds, within what the body and the frame have left; false when it didn't run to the end (out of budget, or it
	/// faulted), and the sprite keeps its own values.
	/// </summary>
	internal bool RunParticle( int layer )
	{
		var budget = Allowed();
		if ( budget <= 0 )
			return false;

		var finished = Machine.RunParticle( layer, IO, this, ScriptPrograms.Stack, budget );
		Spend( budget, finished ? Machine.InstructionsLeft : 0 );
		return finished;
	}

	/// <summary>Whether the body and the frame have instructions left for another run.</summary>
	internal bool CanRun => Allowed() > 0;

	public void Emit( int layer, int count )
	{
		if ( layer >= 0 && layer < _layers.Length && count > 0 )
			_layers[ layer ]?.Emit( Math.Min( count, MostEmitted ) );
	}

	public void EmitAt( int layer, double x, double y, double z, int count )
	{
		if ( layer >= 0 && layer < _layers.Length && count > 0 && double.IsFinite( x ) && double.IsFinite( y ) && double.IsFinite( z ) )
			_layers[ layer ]?.EmitAt( new Vector3( (float)x, (float)y, (float)z ), Math.Min( count, MostEmitted ) );
	}

	// What the body is doing, for every run this frame: velocity as the body faces (forwards, left, up)
	private void Fill( EffectSignals signals, Rotation body, Vector3 anchor, float anchorSpeed, float delta, float now )
	{
		var io = IO;
		var seconds = now - _started;
		io.Set( ScriptInput.Time, MathF.Floor( seconds * 1000f ) );
		io.Set( ScriptInput.Seconds, seconds );
		io.Set( ScriptInput.Speed, signals.Speed );
		var velocity = body.Inverse * signals.Velocity;
		io.Set( ScriptInput.VelocityX, velocity.x, velocity.y, velocity.z );
		io.Set( ScriptInput.Grounded, signals.IsGrounded ? 1 : 0 );
		io.Set( ScriptInput.Crouching, signals.IsCrouching ? 1 : 0 );
		io.Set( ScriptInput.Moving, signals.IsMoving ? 1 : 0 );
		io.Set( ScriptInput.InAir, signals.IsGrounded ? 0 : 1 );
		io.Set( ScriptInput.HoldingWeapon, signals.IsHolding ? 1 : 0 );
		io.Set( ScriptInput.AnchorSpeed, anchorSpeed );
		io.Set( ScriptInput.AnchorPositionX, anchor.x, anchor.y, anchor.z );
		io.Set( ScriptInput.Dt, delta );
	}

	private void Event( ScriptEvent scriptEvent )
	{
		if ( !Machine.Program.Handles( scriptEvent ) )
			return;

		var budget = Allowed();
		if ( budget <= 0 )
			return;

		var finished = Machine.RunEvent( scriptEvent, IO, this, ScriptPrograms.Stack, budget );
		Spend( budget, finished ? Machine.InstructionsLeft : 0 );
	}

	// A shot: its path for OnShoot, and OnHit when it hit something
	private void Shoot( in ShotPath shot )
	{
		var io = IO;
		io.Set( ScriptInput.ShotOriginX, shot.Origin.x, shot.Origin.y, shot.Origin.z );
		io.Set( ScriptInput.ShotDirectionX, shot.Direction.x, shot.Direction.y, shot.Direction.z );
		io.Set( ScriptInput.ShotHit, shot.Hit ? 1 : 0 );
		io.Set( ScriptInput.ShotHitPositionX, shot.HitPosition.x, shot.HitPosition.y, shot.HitPosition.z );
		io.Set( ScriptInput.ShotHitNormalX, shot.HitNormal.x, shot.HitNormal.y, shot.HitNormal.z );
		io.Set( ScriptInput.ShotDistance, shot.Distance );
		Event( ScriptEvent.Shoot );
		if ( shot.Hit )
			Event( ScriptEvent.Hit );
	}

	// Gives each layer the fields the runs changed, then forgets them
	private void ApplyLive()
	{
		for ( var i = 0; i < _layers.Length; i++ )
			_layers[ i ]?.ApplyLive( Machine, i );

		Machine.ClearLiveChanges();
	}

	// What the next run may spend: its own cap, within the body's and the frame's
	private int Allowed()
	{
		var now = Time.Now;
		if ( _frameCountedAt != now )
		{
			_frameCountedAt = now;
			_frameLeft = FrameInstructions;
		}

		if ( _body.CountedAt != now )
		{
			_body.CountedAt = now;
			_body.Left = BodyInstructions;
		}

		return Math.Min( ScriptLimits.RunInstructions, Math.Min( _body.Left, _frameLeft ) );
	}

	private void Spend( int given, int left )
	{
		var used = Math.Max( 0, given - left );
		_body.Left -= used;
		_frameLeft -= used;
	}

	private static ScriptEvent[] Moments()
	{
		var moments = Enum.GetValues<LayerWhen>();
		var events = new ScriptEvent[ moments.Length ];
		for ( var i = 0; i < moments.Length; i++ )
		{
			var name = moments[ i ].ToString();
			events[ i ] = name.StartsWith( "On" ) && Enum.TryParse<ScriptEvent>( name[ 2.. ], out var scriptEvent ) ? scriptEvent : ScriptEvent.None;
		}

		return events;
	}

	private static int IndexOf( System.Collections.Generic.IReadOnlyList<string> names, string name )
	{
		for ( var i = 0; i < names.Count; i++ )
		{
			if ( names[ i ] == name )
				return i;
		}

		return -1;
	}
}
