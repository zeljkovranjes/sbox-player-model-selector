using System;
using System.Collections.Generic;
using System.Diagnostics;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// What players' drawn shapes look like as sprites (see <see cref="DrawnShape"/>), drawn on this machine from their
/// lines (see <see cref="ShapeRaster"/>) at one small size, emblems bigger (see <see cref="EmblemRaster"/>), once for each
/// shape whoever wears it: a hundred players with the same sticker make one texture. Kept by what the shape is, never by
/// its id, so no one can make their shape stand in for another's. Shapes asked for wait their turn and are drawn a
/// millisecond or so's worth a frame (an emblem a layer at a time), so a crowd joining doesn't stall a frame; the textures
/// kept take at most <see cref="MostBytes"/>, and past that the bodies furthest away show a plain shape while nearer ones
/// get theirs. An emblem whose layers' colours move is put together again a few times a second while it shows near,
/// within a fraction of a millisecond a frame. A server with no screen draws none.
/// </summary>
internal static class ShapeTextures
{
	/// <summary>
	/// The bytes the kept textures (and emblems' moving parts) take at most: 16 MB, 256 stickers (each
	/// <see cref="ShapeRaster.Size"/> square, four bytes a pixel, 64 KB) or 64 still emblems (256 KB).
	/// </summary>
	internal const long MostBytes = 16L * 1024 * 1024;

	// How long a frame spends drawing shapes at most (at least one step is taken, a millisecond or two at worst), and how
	// long a texture let go of is kept before it is freed, so nothing still showing it for a frame shows a freed one
	private const double FrameMilliseconds = 1.5;
	private const float RetireSeconds = 2f;

	// How many shapes wait to be drawn at most: past that the furthest are let go of (their bodies show a plain shape)
	private const int MostWaiting = 256;

	// Moving emblems are put together again this often a second at most, while a body showing one is this near, for at
	// most this long a frame (at least one)
	private const float MovingRate = 15f;
	private const float MovingDistance = 2000f;
	private const double MovingMilliseconds = 0.5;
	private const float MillisecondsPerSecond = 1000f;

	private sealed class Entry
	{
		public Texture Texture;
		public Sprite Sprite;
		public long Bytes;
		public EmblemRaster.Moving Moving;
		public RealTimeSince SinceComposed;

		// How far away the nearest body showing it is, as last counted (see Count); none counted is as far as can be
		public float Nearest;
	}

	// A shape waiting to be drawn, and how near the nearest body wanting it is, kept up to date by the counts
	private sealed class Wait
	{
		public DrawnShape Shape;
		public float Nearest;
	}

	// Where shapes and emblems are drawn, kept from one to the next: a crowd joining makes no garbage to collect but the
	// pictures. One emblem is drawn at a time, a layer a step, by what it is
	private static readonly ShapeRaster.Canvas Scratch = new();
	private static readonly EmblemRaster.Workspace Room = new();
	private static EmblemRaster.Job _job;
	private static string _jobContent;

	private static readonly Dictionary<string, Entry> Entries = new();
	private static readonly Dictionary<string, Wait> Waiting = new();
	private static readonly List<Entry> Moving = new();
	private static readonly List<(Texture Texture, RealTimeSince Since)> Retired = new();
	private static float _frame = -1f;
	private static double _spentThisFrame;
	private static long _bytes;
	private static int _nextMoving;

	/// <summary>
	/// Counts up whenever a texture is drawn or let go of: whatever shows one asks again when this changes, and between
	/// changes asks nothing.
	/// </summary>
	internal static int Generation { get; private set; }

	/// <summary>How many textures are kept now, the bytes they take, and how many of them move.</summary>
	internal static int Kept => Entries.Count;
	internal static long Bytes => _bytes;
	internal static int Moves => Moving.Count;

	/// <summary>
	/// The sprite of <paramref name="shape"/> for a body <paramref name="distance"/> away (0 for a picture on the page),
	/// or null for now: it waits its turn to be drawn, or until a texture shown further away can be let go of for it, and
	/// whatever shows it asks again on the next <see cref="Generation"/>.
	/// </summary>
	internal static Sprite SpriteOf( DrawnShape shape, float distance ) => Request( shape, distance )?.Sprite;

	/// <summary>The texture of <paramref name="shape"/> for a picture on the page; null for now (see <see cref="SpriteOf"/>).</summary>
	internal static Texture TextureOf( DrawnShape shape ) => Request( shape, 0f )?.Texture;

	/// <summary>
	/// Starts counting again which shapes show (or wait to) and how near: each body wearing effects is counted after this,
	/// nearest first (see <see cref="Count"/>). A few times a second: no allocations.
	/// </summary>
	internal static void BeginCount()
	{
		foreach ( var entry in Entries.Values )
			entry.Nearest = float.MaxValue;

		foreach ( var wait in Waiting.Values )
			wait.Nearest = float.MaxValue;
	}

	/// <summary>
	/// Counts the shape <paramref name="content"/> (see <see cref="DrawnShape.Content"/>) as showing, or wanting to,
	/// <paramref name="distance"/> away.
	/// </summary>
	internal static void Count( string content, float distance )
	{
		if ( content is null )
			return;

		if ( Entries.TryGetValue( content, out var entry ) )
			entry.Nearest = MathF.Min( entry.Nearest, distance );
		else if ( Waiting.TryGetValue( content, out var wait ) )
			wait.Nearest = MathF.Min( wait.Nearest, distance );
	}

	/// <summary>
	/// Draws the shapes waiting, nearest first, for a millisecond or so this frame (at least one step), while there is
	/// room or a texture shown further away to let go of; puts moving emblems shown near together again; and frees
	/// textures let go of a while ago. Every frame: with nothing waiting it allocates nothing.
	/// </summary>
	internal static void Pump()
	{
		// Counted by the frame's time, which stays the same all frame (real time doesn't)
		if ( Time.Now != _frame )
		{
			_frame = Time.Now;
			_spentThisFrame = 0;
			Animate();
		}

		while ( _spentThisFrame < FrameMilliseconds && (Waiting.Count > 0 || _job is not null) )
		{
			var started = Stopwatch.GetTimestamp();
			var drawn = DrawNext();
			_spentThisFrame += Stopwatch.GetElapsedTime( started ).TotalMilliseconds;
			if ( !drawn )
				break;
		}

		for ( var i = Retired.Count - 1; i >= 0; i-- )
		{
			if ( Retired[ i ].Since < RetireSeconds )
				continue;

			Retired[ i ].Texture?.Dispose();
			Retired.RemoveAt( i );
		}
	}

	private static Entry Request( DrawnShape shape, float distance )
	{
		if ( shape is null || Application.IsHeadless )
			return null;

		var content = shape.Content;
		if ( Entries.TryGetValue( content, out var entry ) )
		{
			entry.Nearest = MathF.Min( entry.Nearest, distance );
			return entry;
		}

		if ( Waiting.TryGetValue( content, out var wait ) )
			wait.Nearest = MathF.Min( wait.Nearest, distance );
		else
		{
			Waiting[ content ] = new Wait { Shape = shape, Nearest = distance };
			if ( Waiting.Count > MostWaiting )
				Waiting.Remove( Furthest( Waiting, float.MinValue, x => x.Nearest ) );
		}

		Pump();
		return Entries.GetValueOrDefault( content );
	}

	private static int SizeOf( DrawnShape shape ) => shape.IsEmblem ? EmblemRaster.Size : ShapeRaster.Size;

	// One step towards the shapes waiting: the emblem being drawn a layer further (kept once done), or the nearest shape
	// drawn (or its emblem started) when there is room, or room is made by letting go of the texture shown furthest away
	// if that is further than it; false when it has to wait, as everything kept shows nearer
	private static bool DrawNext()
	{
		if ( _job is not null )
			return StepEmblem();

		string content = null;
		Wait nearest = null;
		foreach ( var (key, wait) in Waiting )
		{
			if ( nearest is not null && wait.Nearest >= nearest.Nearest )
				continue;

			content = key;
			nearest = wait;
		}

		var size = SizeOf( nearest.Shape );
		if ( !MakeRoom( (long)size * size * 4, nearest.Nearest ) )
			return false;

		if ( nearest.Shape.IsEmblem )
		{
			_job = new EmblemRaster.Job( nearest.Shape, size, Room, ScriptPrograms.Cache, ScriptPrograms.Stack );
			_jobContent = content;
			return true;
		}

		Waiting.Remove( content );
		var texture = Texture.Create( size, size ).WithName( $"drawn_shape_{nearest.Shape.Id}" ).WithData( ShapeRaster.Rgba( nearest.Shape, size, Scratch ) ).Finish();
		Keep( content, new Entry { Texture = texture, Sprite = Sprite.FromTexture( texture ), Bytes = (long)size * size * 4, Nearest = nearest.Nearest } );
		return true;
	}

	// The emblem being drawn, a layer further; once done, kept with its moving part while there is room for that too
	private static bool StepEmblem()
	{
		if ( !Waiting.TryGetValue( _jobContent, out var wait ) )
		{
			_job = null;
			return true;
		}

		if ( !_job.Step() )
			return true;

		var size = EmblemRaster.Size;
		var bytes = (long)size * size * 4;
		var moving = _job.Moving;
		if ( moving is not null && !MakeRoom( bytes + moving.Bytes, wait.Nearest ) )
			moving = null;

		var builder = Texture.Create( size, size ).WithName( $"emblem_{wait.Shape.Id}" );
		if ( moving is not null )
			builder = builder.WithDynamicUsage();

		var texture = builder.WithData( Room.Pixels ).Finish();
		Waiting.Remove( _jobContent );
		Keep( _jobContent, new Entry { Texture = texture, Sprite = Sprite.FromTexture( texture ), Bytes = bytes + (moving?.Bytes ?? 0), Moving = moving, Nearest = wait.Nearest } );
		_job = null;
		_jobContent = null;
		return true;
	}

	private static void Keep( string content, Entry entry )
	{
		Entries[ content ] = entry;
		_bytes += entry.Bytes;
		if ( entry.Moving is not null )
			Moving.Add( entry );

		Generation++;
	}

	// Lets go of the textures shown furthest away, further than distance, until bytes more fit; false when they can't
	private static bool MakeRoom( long bytes, float distance )
	{
		while ( _bytes + bytes > MostBytes )
		{
			if ( !LetGoFurthest( distance ) )
				return false;
		}

		return true;
	}

	// The moving emblems shown near put together as they are now, in turn, each at most MovingRate times a second, for at
	// most MovingMilliseconds a frame: no allocations
	private static void Animate()
	{
		if ( Moving.Count == 0 )
			return;

		var started = Stopwatch.GetTimestamp();
		var ms = (long)(RealTime.Now * MillisecondsPerSecond);
		for ( var tried = 0; tried < Moving.Count; tried++ )
		{
			_nextMoving = (_nextMoving + 1) % Moving.Count;
			var entry = Moving[ _nextMoving ];
			if ( entry.Nearest > MovingDistance || entry.SinceComposed < 1f / MovingRate || !entry.Texture.IsValid() )
				continue;

			entry.SinceComposed = 0f;
			var moving = entry.Moving;
			moving.Compose( ms, Room );
			entry.Texture.Update( new ReadOnlySpan<byte>( Room.Composed, 0, moving.Width * moving.Height * 4 ), moving.X, moving.Y, moving.Width, moving.Height );
			if ( Stopwatch.GetElapsedTime( started ).TotalMilliseconds >= MovingMilliseconds )
				return;
		}
	}

	// The key of the furthest of items, further than at; null for none
	private static string Furthest<T>( Dictionary<string, T> items, float at, Func<T, float> distance )
	{
		string furthest = null;
		foreach ( var (key, item) in items )
		{
			if ( distance( item ) <= at )
				continue;

			at = distance( item );
			furthest = key;
		}

		return furthest;
	}

	private static bool LetGoFurthest( float distance )
	{
		var furthest = Furthest( Entries, distance, NearestOf );
		if ( furthest is null )
			return false;

		var entry = Entries[ furthest ];
		Retired.Add( (entry.Texture, 0f) );
		Entries.Remove( furthest );
		Moving.Remove( entry );
		_bytes -= entry.Bytes;
		Generation++;
		return true;
	}

	// Made once, so looking for the furthest allocates nothing
	[SkipHotload]
	private static readonly Func<Entry, float> NearestOf = x => x.Nearest;
}
