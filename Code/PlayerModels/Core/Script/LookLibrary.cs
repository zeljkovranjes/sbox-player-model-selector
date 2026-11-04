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
				_items.Add( new LibraryItem( shape.Name, KindOf( shape ), shape.Content ) );
		}
	}

	public IReadOnlyList<LibraryItem> Items => _items;

	public Effect Effect( string name ) => null;

	public DrawnShape Shape( string name, LibraryItemKind kind )
	{
		foreach ( var shape in _shapes )
		{
			if ( shape?.Name == name && KindOf( shape ) == kind )
				return shape;
		}

		return null;
	}

	/// <summary>What a drawn shape is to a script: a sticker when it has its own colours, else a shape.</summary>
	public static LibraryItemKind KindOf( DrawnShape shape ) => shape.Sticker is null ? LibraryItemKind.Shape : LibraryItemKind.Sticker;
}
