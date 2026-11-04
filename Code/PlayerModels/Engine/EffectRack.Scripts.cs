using System;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Sandbox;

namespace PlayerModels.Engine;

// The scripts of the effects a body wears (see Effect.Script): each built once with the effect, then run every frame
// before its layers are placed, sharing the body's instructions for the frame. A body out of view, or too far away to
// show effects, runs none.
internal sealed partial class EffectRack
{
	// How quickly the speed read of an effect's place follows it: a frame's jitter doesn't count
	private const float AnchorSpeedFollow = 15f;

	// How long after a shot hits someone their death counts as a kill
	private const float KillWindow = 1.5f;

	// Who the body's last shot hit (their health) and when
	private Player _victim;
	private float _hitAt;

	// Each effect shown's script while it runs (null for one without), how fast its place moves and where it was, and the
	// instructions this body's scripts share each frame
	private EffectScriptRun[] _runs = Array.Empty<EffectScriptRun>();
	private Vector3[] _anchorWas = Array.Empty<Vector3>();
	private float[] _anchorSpeeds = Array.Empty<float>();
	private readonly EffectScriptRun.BodyBudget _instructions = new();

	// effect's script built for this body, as the effect shown (its layers marked with their place in it); null for an
	// effect without one, or one that doesn't build here
	private EffectScriptRun RunFor( Effect effect, int shown )
	{
		if ( effect.Script is null )
			return null;

		var library = new LookLibrary( _shapes );
		var compiled = ScriptPrograms.Cache.Compile( effect.Script, ScriptKind.Effect, library );
		if ( !compiled.Succeeded )
			return null;

		var machine = new ScriptMachine( compiled.Program );
		machine.SetParameters( effect.Parameters );
		if ( !machine.Build( library, ScriptPrograms.Stack ).Succeeded )
			return null;

		_anchorSpeeds[ shown ] = 0f;
		_anchorWas[ shown ] = default;
		var run = new EffectScriptRun( machine, shown, effect.Key, _instructions );
		_wantsShot |= run.WantsShots;
		return run;
	}

	// Every frame, after the body's shot is worked out and before the layers are placed: a kill is noticed, then each
	// effect's script runs what happened and its OnUpdate. No allocations
	private void RunScripts( SkinnedModelRenderer model, float delta )
	{
		if ( _paused || _share <= 0f )
			return;

		if ( _wantsShot )
			WatchKill();

		for ( var e = 0; e < _effects; e++ )
		{
			if ( _runs[ e ] is not { } run )
				continue;

			var anchor = _from[ e ].Position;
			if ( delta > 0f && _anchorWas[ e ] != default )
				_anchorSpeeds[ e ] += ((anchor - _anchorWas[ e ]).Length / delta - _anchorSpeeds[ e ]) * MathF.Min( 1f, delta * AnchorSpeedFollow );

			_anchorWas[ e ] = anchor;
			run.Step( _signals, model.WorldRotation, anchor, _anchorSpeeds[ e ], delta, _gates[ e ] > 0f );
		}
	}

	// Someone the body's shot hit dying a moment after is a kill (a moment like the others: see EffectSignals)
	private void WatchKill()
	{
		var now = RealTime.Now;
		var hit = _signals.Shot.HitObject;
		if ( _signals.Has( LayerWhen.OnShoot ) && hit.IsValid() && hit.Root.Components.Get<Player>( FindMode.EverythingInSelfAndDescendants ) is { Health: > 0f } victim )
			(_victim, _hitAt) = (victim, now);

		if ( _victim is null )
			return;

		if ( !_victim.IsValid() || now - _hitAt > KillWindow )
			_victim = null;
		else if ( _victim.Health <= 0f )
		{
			_victim = null;
			_signals.Killed( now );
		}
	}

	// What an effect's ghosts copy: the weapon held or the item worn it comes from, or else the body
	private ModelRenderer GhostOf( int effect, SkinnedModelRenderer model )
	{
		switch ( _anchors[ effect ].KindIs )
		{
			case AnchorKind.HeldWeapon when _held is not null && _held.TryGetRenderer( out var weapon ):
				return weapon;
			case AnchorKind.Item when _worn is not null && _worn.TryGetPlacement( _itemIndices[ effect ], out var placement ) && placement.Renderer.IsValid():
				return placement.Renderer;
			default:
				return model;
		}
	}

	/// <summary>
	/// Makes <paramref name="scriptEvent"/> happen to the body now, for a preview's test bar: a layer showing on it shows,
	/// and every script handling it runs it (with <paramref name="amount"/>: a fall's speed, a hurt's damage, a foot).
	/// Only here: nothing is sent, nothing is held to a rate.
	/// </summary>
	internal void Simulate( ScriptEvent scriptEvent, float amount = 0f )
	{
		if ( Enum.TryParse<LayerWhen>( "On" + scriptEvent, out var when ) )
		{
			_signals.Simulate( when, amount );
			return;
		}

		for ( var e = 0; e < _effects; e++ )
			_runs[ e ]?.Simulate( scriptEvent, amount );
	}

	// Lets go of the effects' scripts with their layers
	private void ForgetRuns()
	{
		Array.Clear( _runs );
		_victim = null;
	}
}
