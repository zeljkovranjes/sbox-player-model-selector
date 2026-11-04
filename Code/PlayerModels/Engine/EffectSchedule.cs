using System;
using System.Collections.Generic;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// How much of every body's effects this client shows. Each player's effects are within their budget already (see
/// <see cref="EffectBudget"/>), but a full server of them together could still be too much, so all of them share one
/// budget too, the nearest bodies first: far ones thin out first, and the lights go to the nearest. Bodies further away
/// show fewer sprites and past the mid distance none (see <see cref="PoseSchedule.EffectsShareFor"/>), and out of view
/// they pause. It also lets only so many layers be built each frame, keeps the objects of layers taken off to build
/// the next ones from, and has every body wearing effects read what it is doing each frame, before its animation runs
/// (see <see cref="EffectSignals"/>).
/// </summary>
internal sealed class EffectSchedule : GameObjectSystem<EffectSchedule>
{
	// What this client shows of everyone's effects together
	private const float MostSprites = 6000f;
	private const int MostLights = 16;

	// Shares are worked out again this often: bodies don't come or go, near or far, faster than that shows
	private const float AllotInterval = 0.25f;

	// How many layers are built a frame, over all bodies; a body with more is built alone in its frame
	private const int LayersPerFrame = 12;

	// How many layer objects are kept for reuse; more are destroyed
	private const int MostKept = 256;

	private const string PoolName = "Effect layers kept";

	private readonly Stack<EffectLayerObject> _keptSprites = new();
	private readonly Stack<EffectLayerObject> _keptLights = new();

	// Made where used: a hot reload keeps this system but not fields added since
	private List<EffectRack> _wornRacks;
	private Stack<EffectLayerObject> _keptTrails;
	private Stack<EffectLayerObject> _keptBeams;
	private Stack<EffectLayerObject> _keptGhosts;

	private List<EffectRack> Worn => _wornRacks ??= new();
	private GameObject _pool;
	private int _builtThisFrame;
	private float _countedAt = -1f;
	private RealTimeSince _sinceAllot;

	// The bodies wearing effects, nearest first, and what each wants and gets: grown when there are more, never made
	// again each time
	private EffectRack[] _racks = new EffectRack[ 32 ];
	private float[] _distances = new float[ 32 ];
	private int[] _order = new int[ 32 ];
	private float[] _tiers = new float[ 32 ];
	private bool[] _seen = new bool[ 32 ];
	private float[] _wantedSprites = new float[ 32 ];
	private float[] _wantedLights = new float[ 32 ];
	private float[] _spriteShares = new float[ 32 ];
	private float[] _lightShares = new float[ 32 ];

	public EffectSchedule( Scene scene ) : base( scene )
	{
		// After PoseSchedule reads the camera, before the bodies are posed
		Listen( Stage.FinishUpdate, 1, Tick, "EffectSchedule.Tick" );

		// After the game sets the animation for the frame, before it runs and clears the moments it was told of
		Listen( Stage.UpdateBones, -50, Read, "EffectSchedule.Read" );
	}

	/// <summary>Counts <paramref name="rack"/> among the bodies wearing effects, once it shows any.</summary>
	internal void Wear( EffectRack rack )
	{
		if ( !Worn.Contains( rack ) )
			Worn.Add( rack );
	}

	/// <summary>Stops counting <paramref name="rack"/>: it shows nothing now.</summary>
	internal void TakeOff( EffectRack rack )
	{
		Worn.Remove( rack );
	}

	/// <summary>
	/// Whether <paramref name="layers"/> more layers may be built this frame. The first body of a frame always may, so a
	/// body with more than a frame's worth is still built.
	/// </summary>
	internal bool TryBuild( int layers )
	{
		// Counted by the frame's time, not only from Tick: a preview's scene doesn't run its systems, and a count never
		// started again would keep its effects from ever being built again
		if ( Time.Now != _countedAt )
		{
			_countedAt = Time.Now;
			_builtThisFrame = 0;
		}

		if ( _builtThisFrame > 0 && _builtThisFrame + layers > LayersPerFrame )
			return false;

		_builtThisFrame += Math.Max( 1, layers );
		return true;
	}

	/// <summary>A kept layer object of the kind asked for, or null when none is kept.</summary>
	internal EffectLayerObject Take( LayerKind kind )
	{
		var kept = Kept( kind );
		while ( kept.TryPop( out var layer ) )
		{
			if ( layer.Root.IsValid() )
				return layer;
		}

		return null;
	}

	/// <summary>Keeps a layer object taken off a body, switched off, to build another from.</summary>
	internal void Return( EffectLayerObject layer )
	{
		if ( !layer.Root.IsValid() )
			return;

		var kept = Kept( layer.Kind );
		if ( kept.Count >= MostKept || Pool() is not { } pool )
		{
			layer.Root.Destroy();
			return;
		}

		layer.Root.Enabled = false;
		layer.Root.SetParent( pool, false );
		kept.Push( layer );
	}

	private Stack<EffectLayerObject> Kept( LayerKind kind ) => kind switch
	{
		LayerKind.Light => _keptLights,
		LayerKind.Trail => _keptTrails ??= new(),
		LayerKind.Beam => _keptBeams ??= new(),
		LayerKind.Ghost => _keptGhosts ??= new(),
		_ => _keptSprites,
	};

	// Every frame: each body reads what it is doing, gone ones are let go. No allocations
	private void Read()
	{
		var worn = Worn;
		for ( var i = worn.Count - 1; i >= 0; i-- )
		{
			if ( worn[ i ].IsWorn && worn[ i ].Source.IsValid() )
				worn[ i ].Read();
			else
				worn.RemoveAt( i );
		}
	}

	private GameObject Pool()
	{
		if ( _pool.IsValid() )
			return _pool;

		using ( Scene.Push() )
		{
			_pool = new GameObject( false, PoolName );
			_pool.NetworkMode = NetworkMode.Never;
			_pool.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.Hidden;
		}

		return _pool;
	}

	private void Tick()
	{
		_builtThisFrame = 0;
		ShapeTextures.Pump();
		if ( _sinceAllot < AllotInterval )
			return;

		_sinceAllot = 0;
		Allot();
	}

	// Runs a few times a second for every body: no allocations once the arrays are big enough
	private void Allot()
	{
		var poses = Scene.GetSystem<PoseSchedule>();
		var count = 0;
		foreach ( var rack in Worn )
		{
			if ( !rack.IsWorn || !rack.Source.IsValid() )
				continue;

			if ( count == _racks.Length )
				Grow();

			var seen = true;
			var distance = 0f;
			var tier = poses?.EffectsShareFor( rack.Source.Bounds, out seen, out distance ) ?? 1f;

			_racks[ count ] = rack;
			_distances[ count ] = distance;
			_order[ count ] = count;
			_tiers[ count ] = tier;
			_seen[ count ] = seen;
			count++;
		}

		Array.Sort( _distances, _order, 0, count );

		// Drawn shapes go to the nearest bodies first (see ShapeTextures)
		ShapeTextures.BeginCount();
		for ( var i = 0; i < count; i++ )
			_racks[ _order[ i ] ].CountShapes( _distances[ i ] );

		for ( var i = 0; i < count; i++ )
		{
			var body = _order[ i ];
			_wantedSprites[ i ] = _racks[ body ].WantedSprites * _tiers[ body ];
			_wantedLights[ i ] = _tiers[ body ] > 0f ? _racks[ body ].WantedLights : 0f;
		}

		EffectBudget.Share( _wantedSprites.AsSpan( 0, count ), MostSprites, _spriteShares.AsSpan( 0, count ) );
		EffectBudget.Share( _wantedLights.AsSpan( 0, count ), MostLights, _lightShares.AsSpan( 0, count ) );

		for ( var i = 0; i < count; i++ )
		{
			var body = _order[ i ];
			var lights = (int)MathF.Round( _lightShares[ i ] * _wantedLights[ i ] );
			_racks[ body ].UseShare( _tiers[ body ] * _spriteShares[ i ], lights, !_seen[ body ] );
			_racks[ body ] = null;
		}
	}

	private void Grow()
	{
		var size = _racks.Length * 2;
		Array.Resize( ref _racks, size );
		Array.Resize( ref _distances, size );
		Array.Resize( ref _order, size );
		Array.Resize( ref _tiers, size );
		Array.Resize( ref _seen, size );
		Array.Resize( ref _wantedSprites, size );
		Array.Resize( ref _wantedLights, size );
		Array.Resize( ref _spriteShares, size );
		Array.Resize( ref _lightShares, size );
	}
}
