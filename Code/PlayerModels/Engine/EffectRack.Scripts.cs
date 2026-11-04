using System;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Sandbox;

namespace PlayerModels.Engine;

// The scripts of the effects a body wears (see Effect.Script): each built once with the effect, then run every frame
// before its layers are placed, sharing the body's instructions for the frame. A body out of view, or too far away to
// show effects, runs none (and traces no shots).
internal sealed partial class EffectRack
{
	// How quickly the speed read of an effect's place follows it: a frame's jitter doesn't count
	private const float AnchorSpeedFollow = 15f;

	// How far a shot is traced, and how long after it hits someone their death counts as a kill
	private const float ShotReach = 4096f;
	private const float KillWindow = 1.5f;

	// The citizen's animation parameter for where the player aims, as the body faces
	private const string AimParameter = "aim_eyes";

	// Whether any effect needs the body's shots (a script handling them, a layer showing on a kill), the last shot, who it
	// hit (their health) and when
	private bool _wantsShots;
	private EffectScriptRun.ShotPath _shot;
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
		_wantsShots |= run.WantsShots;
		return run;
	}

	// Every frame, before the layers are placed: the body's shot is traced (a kill noticed), then each effect's script
	// runs what happened and its OnUpdate. No allocations
	private void RunScripts( SkinnedModelRenderer citizen, SkinnedModelRenderer model, float delta )
	{
		if ( _paused || _share <= 0f )
			return;

		if ( _wantsShots )
			WatchShots( citizen );

		for ( var e = 0; e < _effects; e++ )
		{
			if ( _runs[ e ] is not { } run )
				continue;

			var anchor = _from[ e ].Position;
			if ( delta > 0f && _anchorWas[ e ] != default )
				_anchorSpeeds[ e ] += ((anchor - _anchorWas[ e ]).Length / delta - _anchorSpeeds[ e ]) * MathF.Min( 1f, delta * AnchorSpeedFollow );

			_anchorWas[ e ] = anchor;
			run.Step( _signals, model.WorldRotation, anchor, _anchorSpeeds[ e ], delta, _gates[ e ] > 0f, _shot );
		}
	}

	// A shot as every client works it out: from the weapon's muzzle (or the chest) along where the body aims, traced once,
	// never through the shooter. Someone it hit dying a moment after is a kill (a moment like the others: see
	// EffectSignals)
	private void WatchShots( SkinnedModelRenderer citizen )
	{
		var now = RealTime.Now;
		if ( _signals.Has( LayerWhen.OnShoot ) && citizen.IsValid() )
		{
			var aim = citizen.WorldRotation.Forward;
			var eyes = citizen.GetVector( AimParameter );
			if ( !eyes.IsNearlyZero() )
				aim = (citizen.WorldRotation * eyes).Normal;

			var origin = _held is not null && _held.TryGetTip( out var tip ) ? tip.Position : citizen.WorldPosition + Vector3.Up * (Accessory.CitizenHeight * 0.75f);
			var trace = citizen.Scene.Trace.Ray( origin, origin + aim * ShotReach ).IgnoreGameObjectHierarchy( citizen.GameObject.Root ).Run();
			_shot = new EffectScriptRun.ShotPath
			{
				Origin = origin, Direction = aim, Hit = trace.Hit, HitPosition = trace.EndPosition, HitNormal = trace.Normal, Distance = trace.Distance,
			};

			if ( trace.Hit && trace.GameObject.IsValid() && trace.GameObject.Root.Components.Get<Player>( FindMode.EverythingInSelfAndDescendants ) is { Health: > 0f } victim )
				(_victim, _hitAt) = (victim, now);
		}

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

	// Lets go of the effects' scripts with their layers
	private void ForgetRuns()
	{
		Array.Clear( _runs );
		_wantsShots = false;
		_victim = null;
	}

	// Whether a layer of these effects shows on a kill: the body's shots are traced for it
	private static bool ShowsOnKill( EffectLayer layer ) => layer.WhenIs == LayerWhen.OnKill;
}
