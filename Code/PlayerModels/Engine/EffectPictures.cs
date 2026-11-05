using System;
using System.Collections.Generic;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The sprites of a layer whose sprites each show one of several pictures (see <see cref="EffectLayer.Pictures"/>, or a
/// lenticular sticker's two looks). The engine draws one picture per sprite renderer, and a picture made at runtime can't
/// be cut into frames, so each picture has a renderer of its own, on a child of the layer's object with a particle
/// effect that never moves its sprites itself: each frame, once the layer's own sprites are worked out, each is copied
/// to the renderer of the picture it shows (<see cref="Put"/>), and the layer's own renderer draws nothing. The copies
/// are kept from frame to frame (one left over shows nothing until it is needed again), so this allocates nothing once
/// it has as many as the layer has out.
/// </summary>
internal sealed class EffectPictures
{
	// A copy is never stepped on (its effect is paused): it lives as long as this, which is never reached
	private const float CopyLifetime = 1_000_000f;

	private sealed class Picture
	{
		public GameObject Object;
		public ParticleEffect Particles;
		public ParticleSpriteRenderer Renderer;
		public DrawnShape Drawn;
		public int Used;
	}

	private readonly GameObject _root;
	private readonly List<Picture> _pictures = new();
	private int _drawnGeneration = -1;

	/// <summary>How many pictures its sprites take now: below two, the layer's own renderer draws them all.</summary>
	internal int Count { get; private set; }

	internal EffectPictures( GameObject root ) => _root = root;

	/// <summary>
	/// Sets it up for <paramref name="names"/> (pictures, standard shapes or drawn shapes' references, as a layer names
	/// them, found in <paramref name="shapes"/>; or <paramref name="looks"/>, drawn shapes, in their place), drawn as
	/// <paramref name="main"/> draws, up to <paramref name="most"/> sprites each. Fewer than two pictures switches it off.
	/// Runs when the look changes.
	/// </summary>
	internal void Configure( IReadOnlyList<string> names, IReadOnlyList<DrawnShape> looks, IReadOnlyList<DrawnShape> shapes, ParticleSpriteRenderer main, int most )
	{
		Count = Math.Min( looks?.Count ?? names?.Count ?? 0, EffectLayer.MaxPictures );
		if ( Count < 2 )
			Count = 0;

		for ( var i = 0; i < Count; i++ )
		{
			if ( i == _pictures.Count )
				_pictures.Add( Make( i ) );

			var picture = _pictures[ i ];
			var name = looks is null ? names[ i ] : null;
			picture.Drawn = looks is not null ? looks[ i ] : DrawnShape.IsReference( name ) ? Find( shapes, name[ 1.. ] ) : null;
			picture.Used = 0;
			Copy( main, picture.Renderer );
			picture.Renderer.Sprite = picture.Drawn is null && name is not null ? EffectAtlas.SpriteOf( name ) : EffectAtlas.SpriteOf( StandardShape.Circle );

			var particles = picture.Particles;
			particles.MaxParticles = Math.Max( 1, most );
			particles.Clear();
			picture.Object.Enabled = true;
		}

		for ( var i = Count; i < _pictures.Count; i++ )
			_pictures[ i ].Object.Enabled = false;

		_drawnGeneration = -1;
	}

	/// <summary>
	/// Shows sprite <paramref name="sprite"/> (one of the layer's own, as it is to be drawn this frame) as picture
	/// <paramref name="picture"/>. Every frame for every sprite: no allocations once it has as many copies as sprites.
	/// </summary>
	internal void Put( Particle sprite, int picture )
	{
		var into = _pictures[ Math.Clamp( picture, 0, Count - 1 ) ];
		var particles = into.Particles;
		Particle copy;
		if ( into.Used < particles.Particles.Count )
			copy = particles.Particles[ into.Used ];
		else if ( !particles.IsFull )
			copy = particles.Emit( sprite.Position, 0f );
		else
			return;

		into.Used++;
		copy.Position = sprite.Position;
		copy.Velocity = sprite.Velocity;
		copy.Angles = sprite.Angles;
		copy.Size = sprite.Size;
		copy.Color = sprite.Color;
		copy.Alpha = sprite.Alpha;
	}

	/// <summary>
	/// Once every sprite is put this frame: the copies no sprite took show nothing, and the next frame's start over; a
	/// drawn picture's texture is asked for again when the textures change, as <paramref name="distance"/> away.
	/// </summary>
	internal void Finish( float distance )
	{
		var drawnChanged = _drawnGeneration != ShapeTextures.Generation;
		_drawnGeneration = ShapeTextures.Generation;
		for ( var i = 0; i < Count; i++ )
		{
			var picture = _pictures[ i ];
			var copies = picture.Particles.Particles;
			for ( var c = picture.Used; c < copies.Count; c++ )
			{
				copies[ c ].Alpha = 0f;
				copies[ c ].Size = Vector3.Zero;
			}

			picture.Used = 0;
			if ( drawnChanged && picture.Drawn is not null )
			{
				var sprite = ShapeTextures.SpriteOf( picture.Drawn, distance ) ?? EffectAtlas.SpriteOf( StandardShape.Circle );
				if ( picture.Renderer.Sprite != sprite )
					picture.Renderer.Sprite = sprite;
			}
		}
	}

	/// <summary>Counts its drawn pictures as showing <paramref name="distance"/> away (see <see cref="ShapeTextures.Count"/>).</summary>
	internal void CountDrawn( float distance )
	{
		for ( var i = 0; i < Count; i++ )
		{
			if ( _pictures[ i ].Drawn is { } drawn )
				ShapeTextures.Count( drawn.Content, distance );
		}
	}

	// A picture's object under the layer's: an effect that holds the copies still, and the renderer drawing them
	private Picture Make( int index )
	{
		var go = new GameObject( false, $"Effect picture {index}" );
		go.NetworkMode = NetworkMode.Never;
		go.Flags |= GameObjectFlags.NotSaved;
		go.SetParent( _root, false );
		go.LocalTransform = Transform.Zero;

		var particles = go.AddComponent<ParticleEffect>();
		particles.Paused = true;
		particles.Lifetime = CopyLifetime;
		particles.TimeScale = 0f;
		particles.ApplyColor = false;
		particles.ApplyAlpha = false;
		particles.ApplyShape = false;
		particles.ApplyRotation = false;
		particles.Force = false;
		particles.Collision = false;
		particles.StartVelocity = 0f;
		particles.LocalSpace = 0f;

		var renderer = go.AddComponent<ParticleSpriteRenderer>();
		return new Picture { Object = go, Particles = particles, Renderer = renderer };
	}

	// Draws as the layer's own renderer does
	private static void Copy( ParticleSpriteRenderer from, ParticleSpriteRenderer to )
	{
		to.Additive = from.Additive;
		to.Lighting = from.Lighting;
		to.Shadows = from.Shadows;
		to.Opaque = from.Opaque;
		to.CameraFadeNear = from.CameraFadeNear;
		to.CameraFadeFar = from.CameraFadeFar;
		to.Scale = from.Scale;
		to.Alignment = from.Alignment;
		to.Enabled = true;
	}

	private static DrawnShape Find( IReadOnlyList<DrawnShape> shapes, string id )
	{
		for ( var i = 0; i < (shapes?.Count ?? 0); i++ )
		{
			if ( shapes[ i ].Id == id )
				return shapes[ i ];
		}

		return null;
	}
}
