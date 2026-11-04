using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>
/// The drawn shapes a look (or an effect on its own) carries, read within bounds, for its layers to name: a layer's name
/// for one becomes the shape's own id, worked out from what it is, so a name that was tampered with can't stand for
/// another shape. Only the shapes layers name are kept, each once however many layers name it, at most
/// <see cref="DrawnShape.MaxPerLook"/>; a layer naming one past that, or one not carried, is a layer this version can't
/// show.
/// </summary>
internal sealed class ShapePool
{
	// The most shapes read: a few times what a look keeps, so a look listing thousands costs no more than a few
	private const int MostRead = DrawnShape.MaxPerLook * 4;

	private readonly Dictionary<string, DrawnShape> _named = new();
	private readonly List<DrawnShape> _used = new();
	private int _read;

	/// <summary>The shapes named so far, in the order they were first named.</summary>
	internal List<DrawnShape> Used => _used;

	/// <summary>A pool of <paramref name="shapes"/>.</summary>
	internal static ShapePool Of( IEnumerable<DrawnShape> shapes )
	{
		var pool = new ShapePool();
		pool.Add( shapes );
		return pool;
	}

	/// <summary>Adds <paramref name="shapes"/>, each under the name it came with and its own; a name taken already keeps its shape.</summary>
	internal void Add( IEnumerable<DrawnShape> shapes )
	{
		foreach ( var shape in shapes ?? new List<DrawnShape>() )
		{
			if ( _read >= MostRead )
				return;

			_read++;
			if ( shape?.Sanitized() is not { } sanitized )
				continue;

			var given = DrawnShape.ReferencePrefix + (shape.Id ?? "");
			if ( DrawnShape.IsReference( given ) )
				_named.TryAdd( given, sanitized );

			_named.TryAdd( sanitized.Reference, sanitized );
		}
	}

	/// <summary>
	/// What a layer naming <paramref name="reference"/> names now: the shape's own reference, counting it as used; null
	/// when no shape here goes by that name, or <see cref="DrawnShape.MaxPerLook"/> others are used already.
	/// </summary>
	internal string Resolve( string reference )
	{
		if ( reference is null || !_named.TryGetValue( reference, out var shape ) )
			return null;

		if ( !_used.Exists( x => x.Id == shape.Id ) )
		{
			if ( _used.Count >= DrawnShape.MaxPerLook )
				return null;

			_used.Add( shape );
		}

		return shape.Reference;
	}
}
