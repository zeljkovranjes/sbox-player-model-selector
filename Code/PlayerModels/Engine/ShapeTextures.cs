using System;
using System.Collections.Generic;
using System.Diagnostics;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// What players' drawn shapes look like as sprites (see <see cref="DrawnShape"/>), drawn on this machine from their
/// lines (see <see cref="ShapeRaster"/>) at one small size, once for each shape whoever wears it: a hundred players with
/// the same sticker make one texture. Kept by what the shape is, never by its id, so no one can make their shape stand
/// in for another's. Shapes asked for wait their turn and are drawn a millisecond or so's worth a frame, so a crowd joining
/// doesn't stall a frame; at most <see cref="MostTextures"/> are kept, and past that the bodies furthest away show a plain shape while
/// nearer ones get theirs. A server with no screen draws none.
/// </summary>
internal static class ShapeTextures
{
	/// <summary>The most textures kept: each is <see cref="ShapeRaster.Size"/> square, four bytes a pixel (64 KB), 8 MB in all.</summary>
	internal const int MostTextures = 128;

	/// <summary>The bytes the kept textures take at most.</summary>
	internal const int MostBytes = MostTextures * ShapeRaster.Size * ShapeRaster.Size * 4;

	// How long a frame spends drawing shapes at most (at least one is drawn, a few milliseconds at worst), and how long a
	// texture let go of is kept before it is freed, so nothing still showing it for a frame shows a freed one
	private const double FrameMilliseconds = 1.5;
	private const float RetireSeconds = 2f;

	// How many shapes wait to be drawn at most: past that the furthest are let go of (their bodies show a plain shape)
	private const int MostWaiting = 256;

	private sealed class Entry
	{
		public Texture Texture;
		public Sprite Sprite;

		// How far away the nearest body showing it is, as last counted (see Count); none counted is as far as can be
		public float Nearest;
	}

	// A shape waiting to be drawn, and how near the nearest body wanting it is, kept up to date by the counts
	private sealed class Wait
	{
		public DrawnShape Shape;
		public float Nearest;
	}

	// Where shapes are drawn, kept from one to the next: a crowd joining makes no garbage to collect
	private static readonly ShapeRaster.Canvas Scratch = new();

	private static readonly Dictionary<string, Entry> Entries = new();
	private static readonly Dictionary<string, Wait> Waiting = new();
	private static readonly List<(Texture Texture, RealTimeSince Since)> Retired = new();
	private static float _frame = -1f;
	private static double _spentThisFrame;

	/// <summary>
	/// Counts up whenever a texture is drawn or let go of: whatever shows one asks again when this changes, and between
	/// changes asks nothing.
	/// </summary>
	internal static int Generation { get; private set; }

	/// <summary>How many textures are kept now, and the bytes they take.</summary>
	internal static int Kept => Entries.Count;
	internal static long Bytes => (long)Entries.Count * ShapeRaster.Size * ShapeRaster.Size * 4;

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
	/// Draws the shapes waiting, nearest first, for a millisecond or so this frame (at least one), while there is room or
	/// a texture shown further away to let go of, and frees textures let go of a while ago. Every frame: with nothing
	/// waiting it allocates nothing.
	/// </summary>
	internal static void Pump()
	{
		// Counted by the frame's time, which stays the same all frame (real time doesn't)
		if ( Time.Now != _frame )
		{
			_frame = Time.Now;
			_spentThisFrame = 0;
		}

		while ( _spentThisFrame < FrameMilliseconds && Waiting.Count > 0 )
		{
			var started = Stopwatch.GetTimestamp();
			var drawn = DrawNearest();
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

	// The nearest shape waiting, drawn when there is room, or room is made by letting go of the texture shown furthest
	// away if that is further than it; false when it has to wait, as everything kept shows nearer
	private static bool DrawNearest()
	{
		string content = null;
		Wait nearest = null;
		foreach ( var (key, wait) in Waiting )
		{
			if ( nearest is not null && wait.Nearest >= nearest.Nearest )
				continue;

			content = key;
			nearest = wait;
		}

		if ( Entries.Count >= MostTextures && !LetGoFurthest( nearest.Nearest ) )
			return false;

		Waiting.Remove( content );
		var size = ShapeRaster.Size;
		var texture = Texture.Create( size, size ).WithName( $"drawn_shape_{nearest.Shape.Id}" ).WithData( ShapeRaster.Rgba( nearest.Shape, size, Scratch ) ).Finish();
		Entries[ content ] = new Entry { Texture = texture, Sprite = Sprite.FromTexture( texture ), Nearest = nearest.Nearest };
		Generation++;
		return true;
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

		Retired.Add( (Entries[ furthest ].Texture, 0f) );
		Entries.Remove( furthest );
		Generation++;
		return true;
	}

	// Made once, so looking for the furthest allocates nothing
	private static readonly Func<Entry, float> NearestOf = x => x.Nearest;
}
