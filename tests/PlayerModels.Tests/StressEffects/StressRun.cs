using System;
using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Core.Script;

namespace PlayerModels.Tests.StressEffects;

/// <summary>
/// Runs a built stress effect the way the game would for three seconds, allocating nothing once made: OnSpawn, then
/// each frame OnUpdate and every layer's particle function for its sprites (those it keeps, or as many as it would
/// have out), with the player's events on the way: moving, jumping, landing, swinging, getting hurt, stepping, a few
/// kills, dying and coming back. A sticker instead runs for each element of its shape (an emblem's tiles, or a shape's
/// points) every frame. The body circles at a run, so its place and speed keep changing.
/// </summary>
internal sealed class StressRun
{
	public const int Frames = 180;
	private const double Dt = 1 / 60.0;

	// How many sprites a layer that only emits is run for, and the most any layer is
	private const int EmittedSprites = 24;
	private const int MostSprites = 100;

	private readonly ScriptMachine _machine;
	private readonly ScriptIO _io = new();
	private readonly ScriptStack _stack = new();
	private readonly Host _host = new();
	private readonly int[] _sprites;
	private readonly double[] _lifetimes;
	private readonly bool[] _kept;
	private readonly float[] _elements;
	private readonly (int Frame, ScriptEvent Event, double Amount)[] _events;
	private readonly int _key;

	/// <summary>Why the last run stopped, when it did.</summary>
	public string Fault { get; private set; }

	/// <summary>How many runs (events, frames, sprites, elements) the last <see cref="Run"/> made.</summary>
	public int Runs { get; private set; }

	/// <summary>The sprites the effect asked for with Emit and EmitAt, all runs together.</summary>
	public int Emitted => _host.Sprites;

	private sealed class Host : IScriptHost
	{
		public int Sprites;
		public void Emit( int layer, int count ) => Sprites += count;
		public void EmitAt( int layer, double x, double y, double z, int count ) => Sprites += count;
	}

	/// <summary>A run of an effect's machine, built into <paramref name="effect"/>.</summary>
	public StressRun( ScriptMachine machine, Effect effect )
	{
		_machine = machine;
		var layers = effect.Layers.Count;
		(_sprites, _lifetimes, _kept) = (new int[ layers ], new double[ layers ], new bool[ layers ]);
		for ( var layer = 0; layer < layers; layer++ )
		{
			var data = effect.Layers[ layer ];
			_kept[ layer ] = data.Keep > 0;
			_lifetimes[ layer ] = data.Lifetime;
			_sprites[ layer ] = data.Keep > 0 ? (int)data.Keep
				: data.KindIs == LayerKind.Sprite ? Math.Clamp( (int)Math.Ceiling( data.Amount * data.Lifetime + data.Burst ), EmittedSprites, MostSprites )
				: 1;
		}

		_events = Schedule();
		_key = Math.Max( 0, IndexOf( Effect.Keys, "g" ) );
	}

	/// <summary>A run of a sticker's machine over the tiles or points of <paramref name="build"/>'s shape.</summary>
	public StressRun( ScriptMachine machine, ScriptBuild build )
	{
		_machine = machine;
		var places = new List<float>();
		if ( build.Emblem.Count > 0 )
		{
			foreach ( var part in build.Emblem )
				places.AddRange( new[] { part.X, part.Y } );
		}
		else
		{
			foreach ( var stroke in build.Shape.Decode() )
				places.AddRange( stroke.Points );
		}

		_elements = places.ToArray();
	}

	private static int IndexOf( IReadOnlyList<string> names, string name )
	{
		for ( var i = 0; i < names.Count; i++ )
		{
			if ( names[ i ] == name )
				return i;
		}

		return -1;
	}

	// What happens to the player, and when (frames at 60 a second)
	private static (int, ScriptEvent, double)[] Schedule()
	{
		var events = new List<(int, ScriptEvent, double)>
		{
			(2, ScriptEvent.StartMoving, 0), (3, ScriptEvent.Equip, 0), (10, ScriptEvent.Jump, 0), (20, ScriptEvent.Shoot, 0),
			(21, ScriptEvent.Hit, 0), (40, ScriptEvent.Land, 380), (45, ScriptEvent.Impact, 500), (60, ScriptEvent.Hurt, 35),
			(64, ScriptEvent.Hurt, 10), (70, ScriptEvent.Shoot, 0), (74, ScriptEvent.Shoot, 0), (90, ScriptEvent.Reload, 0),
			(100, ScriptEvent.Kill, 0), (110, ScriptEvent.Kill, 0), (115, ScriptEvent.Crouch, 0), (118, ScriptEvent.Jump, 0),
			(120, ScriptEvent.Kill, 0), (125, ScriptEvent.Key, 0), (130, ScriptEvent.Shoot, 0), (140, ScriptEvent.Uncrouch, 0),
			(148, ScriptEvent.Land, 200), (150, ScriptEvent.Death, 0), (160, ScriptEvent.Respawn, 0), (170, ScriptEvent.Holster, 0),
			(175, ScriptEvent.StopMoving, 0),
		};

		for ( var frame = 6; frame < Frames; frame += 18 )
			events.Add( (frame, ScriptEvent.Footstep, (int)(frame / 18 % 2 == 0 ? Effect.Joint.LeftFoot : Effect.Joint.RightFoot)) );

		events.Sort( ( a, b ) => a.Item1.CompareTo( b.Item1 ) );
		return events.ToArray();
	}

	/// <summary>Runs it all once; false at the first run that faulted (see <see cref="Fault"/>).</summary>
	public bool Run()
	{
		Fault = null;
		Runs = 0;
		return _elements is null ? RunEffect() : RunSticker();
	}

	private bool RunEffect()
	{
		var next = 0;
		Body( 0 );
		if ( !Event( ScriptEvent.Spawn ) )
			return false;

		for ( var frame = 0; frame < Frames; frame++ )
		{
			Body( frame );
			for ( ; next < _events.Length && _events[ next ].Frame == frame; next++ )
			{
				var (_, scriptEvent, amount) = _events[ next ];
				_io.Set( ScriptInput.Amount, amount );
				_io.Set( ScriptInput.Foot, amount );
				_io.Set( ScriptInput.Key, _key );
				if ( !Event( scriptEvent ) )
					return false;
			}

			_io.Set( ScriptInput.Dt, Dt );
			if ( !Event( ScriptEvent.Update ) )
				return false;

			for ( var layer = 0; layer < _sprites.Length; layer++ )
			{
				if ( !_machine.HasParticleFunction( layer ) )
					continue;

				for ( var sprite = 0; sprite < _sprites[ layer ]; sprite++ )
				{
					Particle( layer, sprite, frame );
					Runs++;
					if ( !_machine.RunParticle( layer, _io, _host, _stack ) )
						return Stopped();
				}
			}
		}

		return true;
	}

	private bool RunSticker()
	{
		var count = _elements.Length / 2;
		for ( var frame = 0; frame < Frames; frame++ )
		{
			Body( frame );
			for ( var element = 0; element < count; element++ )
			{
				_io.Set( ScriptInput.Index, element );
				_io.Set( ScriptInput.Count, count );
				_io.Set( ScriptInput.Life, 0 );
				_io.Set( ScriptInput.OriginX, _elements[ element * 2 ], _elements[ element * 2 + 1 ], 0 );
				Reset( 0, 0, 0, 0 );
				Runs++;
				if ( !_machine.RunElement( _io, _stack ) )
					return Stopped();
			}
		}

		return true;
	}

	private bool Event( ScriptEvent scriptEvent )
	{
		Runs++;
		return _machine.RunEvent( scriptEvent, _io, _host, _stack ) || Stopped();
	}

	private bool Stopped()
	{
		Fault = _machine.Fault.Describe( _machine.Program )?.ToString();
		return false;
	}

	// The body this frame: running round a circle 200 across, in the air from frame 10 to 40, crouching from 115 to
	// 140, its weapon swinging three times a second
	private void Body( int frame )
	{
		var t = frame * Dt;
		var (turn, radius, pace) = (t * 0.75, 200.0, 0.75 * 200.0);
		var jump = frame is >= 10 and < 40 ? Math.Sin( (frame - 10) / 30.0 * Math.PI ) * 50 : 0;
		_io.Set( ScriptInput.Time, Math.Round( t * 1000 ) );
		_io.Set( ScriptInput.Seconds, t );
		_io.Set( ScriptInput.VelocityX, -Math.Sin( turn ) * pace, Math.Cos( turn ) * pace, 0 );
		_io.Set( ScriptInput.Speed, frame < 175 ? pace : 0 );
		_io.Set( ScriptInput.Grounded, jump > 0 ? 0 : 1 );
		_io.Set( ScriptInput.InAir, jump > 0 ? 1 : 0 );
		_io.Set( ScriptInput.Moving, frame < 175 ? 1 : 0 );
		_io.Set( ScriptInput.Crouching, frame is >= 115 and < 140 ? 1 : 0 );
		_io.Set( ScriptInput.HoldingWeapon, 1 );
		_io.Set( ScriptInput.AnchorSpeed, 80 + 300 * Math.Max( 0, Math.Sin( t * 6 * Math.PI ) ) );
		_io.Set( ScriptInput.AnchorPositionX, Math.Cos( turn ) * radius, Math.Sin( turn ) * radius, 40 + jump );
		_io.Set( ScriptInput.ShotOriginX, Math.Cos( turn ) * radius + 10, Math.Sin( turn ) * radius, 60 );
		_io.Set( ScriptInput.ShotDirectionX, 1, 0, 0 );
		_io.Set( ScriptInput.ShotHit, 1 );
		_io.Set( ScriptInput.ShotHitPositionX, Math.Cos( turn ) * radius + 310, Math.Sin( turn ) * radius, 60 );
		_io.Set( ScriptInput.ShotHitNormalX, -1, 0, 0 );
		_io.Set( ScriptInput.ShotDistance, 300 );
	}

	// Sprite k of a layer this frame: kept ones live all along, emitted ones are spread through their lifetimes
	private void Particle( int layer, int sprite, int frame )
	{
		var t = frame * Dt;
		var lifetime = Math.Max( _lifetimes[ layer ], 0.1 );
		var age = _kept[ layer ] ? t : (t + sprite * 0.37) % lifetime;
		_io.Set( ScriptInput.Index, sprite );
		_io.Set( ScriptInput.Count, _sprites[ layer ] );
		_io.Set( ScriptInput.Age, age );
		_io.Set( ScriptInput.Life, age / lifetime % 1 );
		_io.Set( ScriptInput.Seed, (sprite * 0.6180339887 + layer * 0.1) % 1 );
		var (x, y, z) = (Math.Cos( sprite ) * 5, Math.Sin( sprite ) * 5, sprite % 5 - 2);
		_io.Set( ScriptInput.OriginX, x, y, z );
		Reset( _io.Inputs[ (int)ScriptInput.AnchorPositionX ] + x, _io.Inputs[ (int)ScriptInput.AnchorPositionY ] + y, _io.Inputs[ (int)ScriptInput.AnchorPositionZ ] + z, sprite % 4 );
	}

	// The element's own values before a run: white, solid, its own size, unturned, where it is
	private void Reset( double x, double y, double z, int frame )
	{
		var outputs = _io.Outputs;
		Array.Clear( outputs );
		(outputs[ (int)ScriptOutput.ColorR ], outputs[ (int)ScriptOutput.ColorG ], outputs[ (int)ScriptOutput.ColorB ], outputs[ (int)ScriptOutput.ColorA ]) = (1, 1, 1, 1);
		(outputs[ (int)ScriptOutput.Alpha ], outputs[ (int)ScriptOutput.Size ]) = (1, 1);
		(outputs[ (int)ScriptOutput.PositionX ], outputs[ (int)ScriptOutput.PositionY ], outputs[ (int)ScriptOutput.PositionZ ]) = (x, y, z);
		outputs[ (int)ScriptOutput.Frame ] = frame;
		_io.OutputsSet = 0;
	}
}
