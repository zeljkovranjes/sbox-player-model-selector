using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// The player's own shapes as a look carries them (see <see cref="PlayerLook.DrawnShapes"/>), for a script built on any
/// machine: <c>Shapes.Mine( "COOL" )</c> finds the copy the look carries, by its name, so everyone sees what its author
/// saw without having it. A look carries no effects, so <c>Effects.Mine</c> finds none here.
/// </summary>
public sealed class LookLibrary : IPlayerLibrary
{
	private readonly IReadOnlyList<DrawnShape> _shapes;
	private readonly List<LibraryItem> _items = new();

	public LookLibrary( IReadOnlyList<DrawnShape> shapes )
	{
		_shapes = shapes ?? Array.Empty<DrawnShape>();
		foreach ( var shape in _shapes )
		{
			if ( !string.IsNullOrEmpty( shape?.Name ) )
				AddItems( _items, shape );
		}
	}

	/// <summary>
	/// The items <paramref name="shape"/> is in a library: a shape or a sticker by its <see cref="KindOf"/>, and a sticker
	/// made of layers an emblem too (for <c>Emblems.Mine</c>).
	/// </summary>
	public static void AddItems( List<LibraryItem> items, DrawnShape shape )
	{
		items.Add( new LibraryItem( shape.Name, KindOf( shape ), shape.Content ) );
		if ( shape.IsEmblem )
			items.Add( new LibraryItem( shape.Name, LibraryItemKind.Emblem, shape.Content ) );
	}

	public IReadOnlyList<LibraryItem> Items => _items;

	public Effect Effect( string name ) => null;

	public DrawnShape Shape( string name, LibraryItemKind kind )
	{
		foreach ( var shape in _shapes )
		{
			if ( shape?.Name == name && Is( shape, kind ) )
				return shape;
		}

		return null;
	}

	/// <summary>What a drawn shape is to a script: a sticker when it has its own colours (made of layers or not), else a shape.</summary>
	public static LibraryItemKind KindOf( DrawnShape shape ) => shape.IsSticker ? LibraryItemKind.Sticker : LibraryItemKind.Shape;

	/// <summary>Whether <paramref name="shape"/> is what <paramref name="kind"/> asks for: a shape, a sticker, or an emblem (a sticker made of layers).</summary>
	public static bool Is( DrawnShape shape, LibraryItemKind kind ) => kind == LibraryItemKind.Emblem ? shape.IsEmblem : KindOf( shape ) == kind;
}
