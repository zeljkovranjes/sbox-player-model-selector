using System;
using System.Collections.Generic;
using System.Globalization;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Sandbox;

namespace PlayerModels.Engine;

// What a layer does sprite by sprite, each frame as it is placed: the colour mode's script colours each sprite, the
// rhythm's script shades, turns or wobbles the layer, a layer that keeps its sprites out holds each at its place, the
// layer's own particle function (its effect's script) moves and colours each, sprites lie as the layer aligns them, and
// no sprite is ever drawn bigger than MaxDrawnSize, whatever a script asks. Also what its effect's script changes while
// it runs, and the sprites the script emits; a layer with several pictures puts each sprite on its picture's renderer
// (see EffectPictures). All without allocating.
internal sealed partial class EffectLayerObject
{
	/// <summary>
	/// The biggest a sprite is ever drawn, in world units: a little over the biggest a layer can ask for (its size at the
	/// size slider's top), so no script, multiplier or body size makes one that covers the screen.
	/// </summary>
	internal const float MaxDrawnSize = 128f;

	// A sprite a layer keeps out lives this long, and its life loops by the layer's lifetime instead
	private const float KeptLifetime = 100_000f;

	// How many sprites a script's Emit can add to a layer over what it makes itself
	private const int EmitRoom = (int)EffectLayer.MaxBurst;

	// How far from the effect's place a particle function can put a sprite, each way (see Particle.Position)
	private const float PositionReach = 400f;

	// Indices count up to here, then start again (a float holds every whole number below it exactly)
	private const int MostIndex = 1 << 24;

	private static readonly ScriptIO ElementIO = new();
	private static ScriptMachine[] _rhythms;

	// The sticker its sprites show has a script of its own (see DrawnShape.Script): run for each sprite to fade, size, turn
	// or move it (its layers' colours are the texture's). Every sprite on this client shares these instructions a frame;
	// past them, sprites keep their own
	private const int StickerFrameInstructions = 200_000;
	private static int _stickerLeft;
	private static float _stickerCountedAt = -1f;
	private ScriptMachine _sticker;

	// Its effect's script and which layer of its build this is (-1 for none), whether it has a particle function, and
	// whether the script switched it off
	private EffectScriptRun _run;
	private int _scriptLayer = -1;
	private bool _hasParticleFunction;
	private bool _scriptOff;

	// Where it was placed this frame, where its line ends, when, and how much it shows (0 to 1): the sprites are worked on
	// once they have stepped (see AfterStep), after it is placed
	private Transform _place;
	private Vector3 _lineEnd;
	private float _now;
	private float _visible;
	private EffectBeam _beam;
	private EffectGhosts _ghosts;
	private ModelRenderer _ghostOf;

	// The colour mode's script (null for fixed colours), when its cycle started, how fast it runs, how far it is this
	// frame, how many sprites it counts through and how many before the count starts again, the rhythm's light or dark,
	// and how many sprites appeared
	private ScriptMachine _colours;
	private float _cycleStart;
	private float _cycleSpeed = 1f;
	private long _cycleMs;
	private int _cycleCount = 1;
	private int _cycleWrap = 1;
	private Color _tint = Color.White;
	private int _spawned;

	// Sprites kept out (0 for none) at their places, as they look over their looping life; how sprites lie; whether any
	// sprite needs more than the particles do by themselves
	private int _keep;
	private float[] _keepPlaces = Array.Empty<float>();
	private float _keepLife = 1f;
	private Color _keepColour;
	private Color _keepColourEnd;
	private float _keepSize;
	private float _keepSizeEnd;
	private bool _keptLine;
	private LayerAlign _align;
	private bool _perSprite;

	/// <summary>What the layer's ghosts copy: the body's model, an item worn or the weapon held. Set as it is placed.</summary>
	internal ModelRenderer GhostOf
	{
		set => _ghostOf = value;
	}

	// The scripts the layer runs: its colour mode's, its effect's (run), and its rhythm's (shared by every layer)
	private void UseScripts( Effect effect, EffectLayer data, EffectScriptRun run )
	{
		_run = run;
		_scriptLayer = run is null ? -1 : data.ScriptLayer;
		_hasParticleFunction = _scriptLayer >= 0 && run.Machine.HasParticleFunction( _scriptLayer );
		_scriptOff = false;
		_tint = Color.White;
		_spawned = 0;
		_cycleCount = 1;
		_cycleWrap = 1;
		_cycleStart = RealTime.Now;
		_cycleSpeed = effect.SpeedFactor;
		_cycleMs = 0;
		_keep = Kind == LayerKind.Sprite ? (int)data.Keep : 0;
		_align = data.AlignIs;
		_colours = null;
		_sticker = _drawn?.Script is { } sticker && ScriptPrograms.Cache.Compile( sticker, ScriptKind.Sticker ) is { Succeeded: true } scripted
			? new ScriptMachine( scripted.Program )
			: null;

		var mode = data.ColorModeIs;
		if ( mode == LayerColorMode.Fixed || Kind is not (LayerKind.Sprite or LayerKind.Light) )
			return;

		var compiled = ScriptPrograms.Cache.Compile( PresetScripts.ColorModeScript( mode ), ScriptKind.Sticker );
		if ( !compiled.Succeeded )
			return;

		var colours = data.CycleColours();
		var hued = new List<string>( colours.Count );
		foreach ( var colour in colours )
			hued.Add( HexOf( Hued( colour, effect.Hue ) ) );

		_colours = new ScriptMachine( compiled.Program );
		_colours.SetPalette( hued );
		_cycleWrap = mode == LayerColorMode.Mix ? Math.Max( 1, hued.Count ) : 1;
	}

	// After the sprites are set up: a layer keeping its sprites out holds them still at their places, and those with
	// work to do sprite by sprite are marked
	private void ConfigureKept( EffectLayer data, float size, IReadOnlyList<DrawnShape> shapes )
	{
		_cycleCount = Math.Max( 1, _most );
		_cycleWrap = _cycleWrap > 1 ? _cycleWrap : _cycleCount;
		_renderer.Alignment = _align switch
		{
			LayerAlign.Ground or LayerAlign.Facing or LayerAlign.Outward => ParticleSpriteRenderer.BillboardAlignment.Particle,
			LayerAlign.Swing => ParticleSpriteRenderer.BillboardAlignment.Object,
			_ => ParticleSpriteRenderer.BillboardAlignment.LookAtCamera,
		};

		// Room for what its script emits, past what it makes itself (the colours still count through only those)
		if ( _run is not null )
			_most += EmitRoom;

		_perSprite = true;
		if ( _keep <= 0 )
			return;

		var shape = data.ShapeIs;
		_keptLine = shape == LayerShape.Line;
		_keepPlaces = KeptPlaces.Of( shape, data.Spread * size, _keep, data.Angle, shape == LayerShape.Shape ? Formation( shapes, data, _keep ) : null );
		_keepLife = data.Lifetime;
		_keepSize = data.Size * size;
		_keepSizeEnd = (data.SizeEnd ?? data.Size) * size;
		_keepColour = _particles.Gradient.ConstantA;
		_keepColourEnd = _particles.Gradient.ConstantB;
		_rate = 0f;
		_most = _keep + EmitRoom;
		_cycleCount = _keep;
		_cycleWrap = Math.Min( _cycleWrap, _keep );

		var particles = _particles;
		particles.Lifetime = KeptLifetime;
		particles.ApplyColor = false;
		particles.ApplyAlpha = false;
		particles.ApplyShape = false;
		particles.Force = false;
		particles.StartVelocity = 0f;
		particles.Clear();
	}

	// The points a layer's formation has (count of them; a standard shape's or the drawn one it names)
	private float[] Formation( IReadOnlyList<DrawnShape> shapes, EffectLayer data, int count = 0 )
	{
		var drawn = Find( shapes, data.DrawnFormation );
		if ( drawn is null )
			return count > 0 ? StandardShapes.Formation( data.FormationIs, data.Fill, count ) : StandardShapes.Formation( data.FormationIs, data.Fill );

		var (closed, open) = drawn.Outline();
		return count > 0 ? StandardShapes.FormationOf( closed, open, data.Fill, count ) : StandardShapes.FormationOf( closed, open, data.Fill );
	}

	// The beam's formation: its kept points spread along where it comes from
	private float[] Formation( IReadOnlyList<DrawnShape> shapes, EffectLayer data ) =>
		KeptPlaces.Of( data.ShapeIs, data.Spread, (int)data.Keep, data.Angle, data.ShapeIs == LayerShape.Shape ? Formation( shapes, data, (int)data.Keep ) : null );

	// A sprite appearing: its place in the order they appear, and where on the layer's shape it came from (kept in
	// fields effects never use: they don't collide). On the main thread
	private void Created( Particle particle )
	{
		particle.Radius = _spawned;
		_spawned = (_spawned + 1) % MostIndex;
		particle.HitNormal = Root.WorldTransform.PointToLocal( particle.Position );
		particle.HitPos = Vector3.Zero;
	}

	// The rhythm now, beat beats in: how light or dark it shades the layer, how far round it turns it (degrees), and how
	// far it wobbles it (citizen units). Its script runs once at full strength; the layer's amount scales that
	private (Color Tint, float Spin, Vector3 Wobble) Rhythm( float beat )
	{
		if ( _rhythm == LayerRhythm.Steady || RhythmMachine( _rhythm ) is not { } machine )
			return (Color.White, 0f, Vector3.Zero);

		var io = ElementIO;
		ResetOutputs( io );
		io.Set( ScriptInput.Seconds, beat );
		io.Set( ScriptInput.Time, MathF.Floor( beat * 1000f ) );
		if ( !machine.RunElement( io, ScriptPrograms.Stack ) )
			return (Color.White, 0f, Vector3.Zero);

		var outputs = io.Outputs;
		switch ( _rhythm )
		{
			case LayerRhythm.Spin:
				return (Color.White, (float)outputs[ (int)ScriptOutput.Rotation ], Vector3.Zero);
			case LayerRhythm.Jiggle:
				return (Color.White, 0f, new Vector3( (float)outputs[ (int)ScriptOutput.OffsetX ], (float)outputs[ (int)ScriptOutput.OffsetY ], (float)outputs[ (int)ScriptOutput.OffsetZ ] ) * _beatAmount);
			case LayerRhythm.Rainbow:
				return (new Color( (float)outputs[ 0 ], (float)outputs[ 1 ], (float)outputs[ 2 ], 1f ), 0f, Vector3.Zero);
			default:
			{
				var level = 1f - _beatAmount * (1f - (float)outputs[ 0 ]);
				return (new Color( level, level, level, 1f ), 0f, Vector3.Zero);
			}
		}
	}

	// Each rhythm's script, compiled once for every layer, at its full amount (a layer scales it by its own)
	private static ScriptMachine RhythmMachine( LayerRhythm rhythm )
	{
		_rhythms ??= new ScriptMachine[ Enum.GetValues<LayerRhythm>().Length ];
		if ( _rhythms[ (int)rhythm ] is { } machine )
			return machine;

		if ( PresetScripts.RhythmScript( rhythm ) is not { } script || ScriptPrograms.Cache.Compile( script, ScriptKind.Sticker ) is not { Succeeded: true } compiled )
			return null;

		machine = new ScriptMachine( compiled.Program );
		machine.SetParameters( new Dictionary<string, string> { ["amount"] = "1" } );
		_rhythms[ (int)rhythm ] = machine;
		return machine;
	}

	// The colour its colour mode gives sprite i (its place in the count) this frame, life of the way through its life
	private Color ColourOf( int i, float life )
	{
		var io = ElementIO;
		ResetOutputs( io );
		io.Set( ScriptInput.Time, _cycleMs );
		io.Set( ScriptInput.Seconds, _cycleMs / MillisecondsPerSecond );
		io.Set( ScriptInput.Index, i % _cycleWrap );
		io.Set( ScriptInput.Count, _cycleCount );
		io.Set( ScriptInput.Life, life );
		if ( !_colours.RunElement( io, ScriptPrograms.Stack ) )
			return Color.White;

		var outputs = io.Outputs;
		return new Color( (float)outputs[ 0 ], (float)outputs[ 1 ], (float)outputs[ 2 ], (float)outputs[ 3 ] );
	}

	// Once its sprites have stepped this frame, and before they are drawn: each one as the layer says. On the main thread
	private void AfterStep( float delta )
	{
		if ( _perSprite && Root.IsValid() )
			PlaceSprites( _place, _lineEnd, _now, _visible );
	}

	// Every sprite this frame, the layer placed at place (a line running to lineEnd), shown this much (0 to 1)
	private void PlaceSprites( Transform place, Vector3 lineEnd, float now, float visible )
	{
		var particles = _particles.Particles;
		if ( _keep > 0 && particles.Count < _keep && visible > 0f )
			KeepOut( place, lineEnd );

		var runs = _hasParticleFunction && _run is not null;
		var count = particles.Count;
		var alignment = AlignmentOf( place );
		var pictures = _pictures?.Count ?? 0;
		var (across, turned) = pictures > 0 && _lenticular ? Lenticular( place ) : (Vector3.Zero, 0f);
		for ( var i = 0; i < count; i++ )
		{
			var p = particles[ i ];
			var index = (int)p.Radius;
			var position = p.Position - p.HitPos;
			var kept = _keep > 0 && index < _keep;
			float life;
			if ( kept )
			{
				life = _keepLife > 0f ? (now - _cycleStart) / _keepLife % 1f : 0f;
				position = KeptPlace( index, place, lineEnd );
				p.Color = Color.Lerp( _keepColour, _keepColourEnd, life ) * _tint;
				p.Alpha = visible;
				var size = MathX.Lerp( _keepSize, _keepSizeEnd, life );
				p.Size = new Vector3( size, size, size );
			}
			else
			{
				var lifetime = p.DeathTime - p.BornTime;
				life = lifetime > 0f ? Math.Clamp( p.Age / lifetime, 0f, 1f ) : 0f;
			}

			if ( _colours is not null )
				p.Color = ColourOf( index, life ) * _tint;

			var offset = Vector3.Zero;
			var rotation = p.Angles.roll;
			var frame = -1;
			var origin = kept ? new Vector3( _keepPlaces[ index * 3 ], _keepPlaces[ index * 3 + 1 ], _keepPlaces[ index * 3 + 2 ] ) : p.HitNormal;
			if ( runs && _run.CanRun )
				RunParticle( p, index, count, life, origin, place, ref position, ref offset, ref rotation, ref frame );

			if ( _sticker is not null )
				RunSticker( p, index, count, life, origin, ref offset, ref rotation );

			if ( alignment is { } aligned )
				p.Angles = ((_align == LayerAlign.Outward ? Outward( position - place.Position, place ) : aligned) * Rotation.FromRoll( rotation )).Angles();
			else
				p.Angles = p.Angles.WithRoll( rotation );

			p.Size = new Vector3( MathF.Min( p.Size.x, MaxDrawnSize ), MathF.Min( p.Size.y, MaxDrawnSize ), MathF.Min( p.Size.z, MaxDrawnSize ) );
			var moved = place.Rotation * offset * place.Scale.x;
			p.Position = position + moved;
			p.HitPos = moved;
			if ( pictures == 0 )
				continue;

			// Which picture it shows: as its particle function picks, else a lenticular look, else each in turn
			if ( frame < 0 )
				frame = _lenticular ? LenticularLook( p.Position - place.Position, across, turned, now ) : index % pictures;

			_pictures.Put( p, frame );
		}

		if ( pictures > 0 )
			_pictures.Finish( Distance );
	}

	// For a lenticular sticker's looks this frame: the camera's right, and how far it has gone round the layer's place (in
	// half turns)
	private (Vector3 Across, float Turned) Lenticular( Transform place )
	{
		if ( Root.Scene?.Camera is not { } camera || !camera.IsValid() )
			return (Vector3.Zero, 0f);

		var rotation = camera.WorldRotation;
		return (rotation.Right, (rotation.Yaw() - place.Rotation.Yaw()) / 180f);
	}

	// The look (0 or 1) of a lenticular sticker's sprite fromPlace off its layer's place: the looks swap as the camera
	// goes round, as time passes and in bands across the screen, as a lenticular print does when it is tilted
	private static int LenticularLook( Vector3 fromPlace, Vector3 across, float turned, float now )
	{
		var phase = now / (2f * LenticularSeconds) + turned + Vector3.Dot( fromPlace, across ) / LenticularBand;
		return phase - MathF.Floor( phase ) < 0.5f ? 0 : 1;
	}

	// One sprite through its layer's particle function: its colour, solidity, size, turn, offset and place as it left them
	private void RunParticle( Particle p, int index, int count, float life, Vector3 origin, Transform place, ref Vector3 position, ref Vector3 offset,
		ref float rotation, ref int frame )
	{
		var io = _run.IO;
		io.Set( ScriptInput.Index, index );
		io.Set( ScriptInput.Count, count );
		io.Set( ScriptInput.Age, p.Age );
		io.Set( ScriptInput.Life, life );
		io.Set( ScriptInput.Seed, p.Rand() );
		io.Set( ScriptInput.OriginX, origin.x, origin.y, origin.z );
		var outputs = io.Outputs;
		(outputs[ 0 ], outputs[ 1 ], outputs[ 2 ], outputs[ 3 ]) = (p.Color.r, p.Color.g, p.Color.b, p.Color.a);
		outputs[ (int)ScriptOutput.Alpha ] = p.Alpha;
		outputs[ (int)ScriptOutput.Size ] = 1;
		outputs[ (int)ScriptOutput.Rotation ] = rotation;
		(outputs[ (int)ScriptOutput.OffsetX ], outputs[ (int)ScriptOutput.OffsetY ], outputs[ (int)ScriptOutput.OffsetZ ]) = (0, 0, 0);
		(outputs[ (int)ScriptOutput.PositionX ], outputs[ (int)ScriptOutput.PositionY ], outputs[ (int)ScriptOutput.PositionZ ]) = (position.x, position.y, position.z);
		outputs[ (int)ScriptOutput.Frame ] = 0;
		io.OutputsSet = 0;
		if ( !_run.RunParticle( _scriptLayer ) )
			return;

		var set = io.OutputsSet;
		if ( (set & ScriptIO.ColorSet) != 0 )
			p.Color = new Color( (float)outputs[ 0 ], (float)outputs[ 1 ], (float)outputs[ 2 ], (float)outputs[ 3 ] ) * _tint;

		if ( (set & ScriptIO.AlphaSet) != 0 )
			p.Alpha = (float)outputs[ (int)ScriptOutput.Alpha ] * _tint.a;

		if ( (set & ScriptIO.SizeSet) != 0 )
			p.Size *= (float)outputs[ (int)ScriptOutput.Size ];

		if ( (set & ScriptIO.RotationSet) != 0 )
			rotation = (float)outputs[ (int)ScriptOutput.Rotation ];

		if ( (set & ScriptIO.FrameSet) != 0 )
			frame = (int)Math.Clamp( Math.Floor( outputs[ (int)ScriptOutput.Frame ] ), 0, EffectLayer.MaxPictures - 1 );

		if ( (set & ScriptIO.OffsetSet) != 0 )
			offset = new Vector3( (float)outputs[ (int)ScriptOutput.OffsetX ], (float)outputs[ (int)ScriptOutput.OffsetY ], (float)outputs[ (int)ScriptOutput.OffsetZ ] );

		if ( (set & ScriptIO.PositionSet) != 0 )
		{
			var anchor = new Vector3( (float)io.Inputs[ (int)ScriptInput.AnchorPositionX ], (float)io.Inputs[ (int)ScriptInput.AnchorPositionY ],
				(float)io.Inputs[ (int)ScriptInput.AnchorPositionZ ] );
			var wanted = new Vector3( (float)outputs[ (int)ScriptOutput.PositionX ], (float)outputs[ (int)ScriptOutput.PositionY ], (float)outputs[ (int)ScriptOutput.PositionZ ] );
			position = new Vector3( Math.Clamp( wanted.x, anchor.x - PositionReach, anchor.x + PositionReach ), Math.Clamp( wanted.y, anchor.y - PositionReach, anchor.y + PositionReach ),
				Math.Clamp( wanted.z, anchor.z - PositionReach, anchor.z + PositionReach ) );
		}
	}

	// One sprite through its sticker's own script, within what this frame has left of the instructions sprites' stickers
	// share: how solid, big, turned and moved it is (its colour is the sticker's)
	private void RunSticker( Particle p, int index, int count, float life, Vector3 origin, ref Vector3 offset, ref float rotation )
	{
		var now = Time.Now;
		if ( _stickerCountedAt != now )
			(_stickerCountedAt, _stickerLeft) = (now, StickerFrameInstructions);

		var budget = Math.Min( ScriptLimits.RunInstructions, _stickerLeft );
		if ( budget <= 0 )
			return;

		var io = ElementIO;
		ResetOutputs( io );
		io.Set( ScriptInput.Time, _cycleMs );
		io.Set( ScriptInput.Seconds, _cycleMs / MillisecondsPerSecond );
		io.Set( ScriptInput.Index, index );
		io.Set( ScriptInput.Count, count );
		io.Set( ScriptInput.Life, life );
		io.Set( ScriptInput.OriginX, origin.x, origin.y, origin.z );
		var finished = _sticker.RunElement( io, ScriptPrograms.Stack, budget );
		_stickerLeft -= finished ? budget - _sticker.InstructionsLeft : budget;
		if ( !finished )
			return;

		var outputs = io.Outputs;
		var set = io.OutputsSet;
		if ( (set & ScriptIO.AlphaSet) != 0 )
			p.Alpha *= (float)outputs[ (int)ScriptOutput.Alpha ];

		if ( (set & ScriptIO.SizeSet) != 0 )
			p.Size *= (float)outputs[ (int)ScriptOutput.Size ];

		if ( (set & ScriptIO.RotationSet) != 0 )
			rotation += (float)outputs[ (int)ScriptOutput.Rotation ];

		if ( (set & ScriptIO.OffsetSet) != 0 )
			offset += new Vector3( (float)outputs[ (int)ScriptOutput.OffsetX ], (float)outputs[ (int)ScriptOutput.OffsetY ], (float)outputs[ (int)ScriptOutput.OffsetZ ] );
	}

	// Which way the layer's sprites lie this frame (null: as the renderer has them, facing the camera): flat on the
	// ground turned the way the body faces, or the way the layer faces
	private Rotation? AlignmentOf( Transform place ) => _align switch
	{
		LayerAlign.Ground => Lying( Vector3.Up, Rotation.FromYaw( place.Rotation.Yaw() ).Forward ),
		LayerAlign.Facing => Lying( place.Rotation.Forward, place.Rotation.Up ),
		LayerAlign.Outward => Lying( place.Rotation.Forward, place.Rotation.Up ),
		_ => null,
	};

	// Out from the layer's place: facing away from its middle, like tiles on a ball
	private static Rotation Outward( Vector3 fromMiddle, Transform place )
	{
		if ( fromMiddle.IsNearlyZero() )
			return Lying( place.Rotation.Forward, place.Rotation.Up );

		var normal = fromMiddle.Normal;
		var along = MathF.Abs( Vector3.Dot( normal, Vector3.Up ) ) > 0.99f ? place.Rotation.Forward : Vector3.Up;
		return Lying( normal, along );
	}

	/// <summary>
	/// The turn that lays a sprite (the renderer draws one by its particle's angles across its forward, the top of its
	/// picture up) facing <paramref name="normal"/>, the top of its picture towards <paramref name="along"/>; a roll then
	/// spins it in that plane.
	/// </summary>
	internal static Rotation Lying( Vector3 normal, Vector3 along ) => Rotation.LookAt( normal, along );

	// Where kept sprite index sits now: at its place in the layer's space, or along the line to its end joint
	private Vector3 KeptPlace( int index, Transform place, Vector3 lineEnd )
	{
		var at = index * 3;
		if ( at + 2 >= _keepPlaces.Length )
			return place.Position;

		if ( _keptLine )
		{
			var start = ToJoint >= 0 ? place.Position : place.PointToWorld( Vector3.Backward * _points.LineStart.Length );
			var end = ToJoint >= 0 ? lineEnd : place.PointToWorld( Vector3.Forward * _points.LineEnd.Length );
			return Vector3.Lerp( start, end, _keepPlaces[ at ] );
		}

		return place.PointToWorld( new Vector3( _keepPlaces[ at ], _keepPlaces[ at + 1 ], _keepPlaces[ at + 2 ] ) );
	}

	// Puts every kept sprite out again, each at its place: on the first frame, and if any went (the effect was cleared)
	private void KeepOut( Transform place, Vector3 lineEnd )
	{
		_particles.Clear();
		_spawned = 0;
		for ( var i = 0; i < _keep; i++ )
			_particles.Emit( KeptPlace( i, place, lineEnd ), 0f );
	}

	/// <summary>
	/// Takes what its effect's script changed of layer <paramref name="layer"/> while it ran (see
	/// <see cref="ScriptMachine.IsLiveChanged"/>): its switch, colours, sizes, amount, life, speeds, spread, offset and
	/// rhythm. A trail's colours and widths stay as built (changing them would make garbage every frame).
	/// </summary>
	internal void ApplyLive( ScriptMachine machine, int layer )
	{
		if ( machine.IsLiveChanged( layer, PropertyId.LayerOff ) )
			_scriptOff = machine.LiveValue( layer, PropertyId.LayerOff ) != 0;

		if ( machine.IsLiveChanged( layer, PropertyId.LayerOffset ) )
			_offset = new Vector3( (float)machine.LiveValue( layer, PropertyId.LayerOffset ), (float)machine.LiveValue( layer, PropertyId.LayerOffset, 1 ),
				(float)machine.LiveValue( layer, PropertyId.LayerOffset, 2 ) );

		if ( machine.IsLiveChanged( layer, PropertyId.LayerRhythmSpeed ) )
			_beatSpeed = (float)machine.LiveValue( layer, PropertyId.LayerRhythmSpeed ) * _speedFactor;

		if ( machine.IsLiveChanged( layer, PropertyId.LayerRhythmAmount ) )
			_beatAmount = (float)machine.LiveValue( layer, PropertyId.LayerRhythmAmount );

		switch ( Kind )
		{
			case LayerKind.Light:
				if ( machine.IsLiveChanged( layer, PropertyId.LayerSize ) )
					_light.Radius = (float)machine.LiveValue( layer, PropertyId.LayerSize ) * _sizeFactor * _motion;

				if ( machine.IsLiveChanged( layer, PropertyId.LayerColor ) || machine.IsLiveChanged( layer, PropertyId.LayerAmount ) )
				{
					var brightness = (float)machine.LiveValue( layer, PropertyId.LayerAmount ) * _amountFactor;
					var colour = _colours is null ? LiveColour( machine, layer, PropertyId.LayerColor ) : Color.White;
					_colour = new Color( colour.r * brightness, colour.g * brightness, colour.b * brightness, 1f );
				}

				return;

			case LayerKind.Sprite:
				ApplyLiveSprites( machine, layer );
				return;

			case LayerKind.Beam:
				_beam.ApplyLive( machine, layer, _sizeFactor * _motion );
				return;

			case LayerKind.Ghost:
				_ghosts.ApplyLive( machine, layer, _amountFactor, _speedFactor );
				return;
		}
	}

	private void ApplyLiveSprites( ScriptMachine machine, int layer )
	{
		var particles = _particles;
		if ( machine.IsLiveChanged( layer, PropertyId.LayerColor ) || machine.IsLiveChanged( layer, PropertyId.LayerColorEnd ) )
		{
			var gradient = particles.Gradient;
			gradient.ConstantA = Hued( LiveColour( machine, layer, PropertyId.LayerColor ) );
			gradient.ConstantB = Hued( LiveColour( machine, layer, PropertyId.LayerColorEnd ) );
			particles.Gradient = gradient;
			(_keepColour, _keepColourEnd) = (gradient.ConstantA, gradient.ConstantB);
		}

		if ( machine.IsLiveChanged( layer, PropertyId.LayerSize ) || machine.IsLiveChanged( layer, PropertyId.LayerSizeEnd ) )
		{
			var scale = particles.Scale;
			scale.ConstantA = (float)machine.LiveValue( layer, PropertyId.LayerSize ) * _sizeFactor;
			scale.ConstantB = (float)machine.LiveValue( layer, PropertyId.LayerSizeEnd ) * _sizeFactor;
			particles.Scale = scale;
			(_keepSize, _keepSizeEnd) = (scale.ConstantA, scale.ConstantB);
		}

		if ( machine.IsLiveChanged( layer, PropertyId.LayerLifetime ) && _keep <= 0 )
		{
			var lifetime = (float)machine.LiveValue( layer, PropertyId.LayerLifetime );
			particles.Lifetime = new ParticleFloat( lifetime * (1f - LifetimeSpread), lifetime * (1f + LifetimeSpread) );
		}
		else if ( machine.IsLiveChanged( layer, PropertyId.LayerLifetime ) )
		{
			_keepLife = (float)machine.LiveValue( layer, PropertyId.LayerLifetime );
		}

		if ( machine.IsLiveChanged( layer, PropertyId.LayerGravity ) )
			particles.ForceDirection = Vector3.Down * ((float)machine.LiveValue( layer, PropertyId.LayerGravity ) * _motion * _sizeFactor);

		if ( machine.IsLiveChanged( layer, PropertyId.LayerAmount ) && _keep <= 0 )
		{
			_rate = (float)machine.LiveValue( layer, PropertyId.LayerAmount ) * _amountFactor;
			UseRate( false );
		}

		if ( machine.IsLiveChanged( layer, PropertyId.LayerSpeed ) )
		{
			var speed = (float)machine.LiveValue( layer, PropertyId.LayerSpeed ) * _motion * _sizeFactor;
			_sphere.Velocity = speed;
			_ring.VelocityFromCenter = speed;
			_points.Velocity = speed;
			_outSpeed = speed;
		}

		if ( machine.IsLiveChanged( layer, PropertyId.LayerSpread ) )
		{
			var spread = (float)machine.LiveValue( layer, PropertyId.LayerSpread ) * _sizeFactor;
			_sphere.Radius = _sphere.Radius > 0f ? spread : 0f;
			_outSpread = _outSpread > 0f ? spread : 0f;
			_ring.Radius = spread;
			_points.Scale = spread;
		}

		if ( machine.IsLiveChanged( layer, PropertyId.LayerAngle ) )
			_cone.ConeAngle = (float)machine.LiveValue( layer, PropertyId.LayerAngle );
	}

	// A layer colour as its script has it now, turned with the effect's colour slider
	private static Color LiveColour( ScriptMachine machine, int layer, PropertyId property ) => new(
		(float)machine.LiveValue( layer, property ), (float)machine.LiveValue( layer, property, 1 ), (float)machine.LiveValue( layer, property, 2 ), 1f );

	private Color Hued( Color colour )
	{
		if ( _hue == 0f )
			return colour;

		var hsv = colour.ToHsv();
		return hsv.WithHue( (hsv.Hue + _hue * 360f) % 360f ).ToColor();
	}

	/// <summary>Sends <paramref name="count"/> of its sprites out now, from where it comes from (its script's <c>Emit</c>).</summary>
	internal void Emit( int count )
	{
		switch ( Kind )
		{
			case LayerKind.Sprite when _keep <= 0:
				_emitter.Burst = MathF.Max( 1f, MathF.Round( count * _share ) );
				_emitter.ResetEmitter();
				_emitter.Burst = 0f;
				return;

			case LayerKind.Ghost:
				_ghosts.Emit( count, _place, _ghostOf );
				return;
		}
	}

	/// <summary>
	/// Sends <paramref name="count"/> of its sprites out at <paramref name="position"/> in the world (its script's
	/// <c>EmitAt</c>): from within its spread, every way, at its speed.
	/// </summary>
	internal void EmitAt( Vector3 position, int count )
	{
		if ( Kind != LayerKind.Sprite || _keep > 0 || !Root.IsValid() )
			return;

		var shown = (int)MathF.Round( count * _share );
		for ( var i = 0; i < shown && !_particles.IsFull; i++ )
		{
			var direction = Vector3.Random.Normal;
			var sprite = _particles.Emit( position + direction * Game.Random.Float( 0f, _outSpread ), i / (float)Math.Max( 1, shown ) );
			sprite.Velocity = direction * _outSpeed;
		}
	}

	private static void ResetOutputs( ScriptIO io )
	{
		var outputs = io.Outputs;
		Array.Clear( outputs );
		(outputs[ 0 ], outputs[ 1 ], outputs[ 2 ], outputs[ 3 ]) = (1, 1, 1, 1);
		outputs[ (int)ScriptOutput.Alpha ] = 1;
		outputs[ (int)ScriptOutput.Size ] = 1;
		io.OutputsSet = 0;
	}

	// A colour as #rrggbb, for a script's palette
	private static string HexOf( Color colour ) => string.Create( CultureInfo.InvariantCulture,
		$"#{(int)MathF.Round( Math.Clamp( colour.r, 0f, 1f ) * 255f ):x2}{(int)MathF.Round( Math.Clamp( colour.g, 0f, 1f ) * 255f ):x2}{(int)MathF.Round( Math.Clamp( colour.b, 0f, 1f ) * 255f ):x2}" );

	// How far from a place in the world sprites sent there start, and how fast they leave it (see EmitAt)
	private float _outSpread;
	private float _outSpeed;
}
