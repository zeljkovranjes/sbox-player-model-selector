using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Core.Script;

namespace PlayerModels.Engine;

/// <summary>
/// The player's own library as their scripts see it on their own machine (see <see cref="IPlayerLibrary"/>): My shapes,
/// the shapes the look being edited carries (named, for a script made elsewhere) and My effects. Read once when made, for
/// one compile or one editor session.
/// </summary>
internal sealed class MyLibrary : IPlayerLibrary
{
	private readonly List<DrawnShape> _shapes = new();
	private readonly List<Effect> _effects;
	private readonly List<LibraryItem> _items = new();

	internal MyLibrary( IReadOnlyList<DrawnShape> lookShapes = null )
	{
		foreach ( var shape in LocalPrefs.MyShapes() )
			Add( shape );

		foreach ( var shape in lookShapes ?? new List<DrawnShape>() )
		{
			if ( !string.IsNullOrEmpty( shape?.Name ) && !_shapes.Exists( x => x.Name == shape.Name && LookLibrary.KindOf( x ) == LookLibrary.KindOf( shape ) ) )
				Add( shape );
		}

		_effects = LocalPrefs.MyEffects();
		foreach ( var effect in _effects )
			_items.Add( new LibraryItem( effect.Name, LibraryItemKind.Effect, effect.Id ) );
	}

	public IReadOnlyList<LibraryItem> Items => _items;

	public Effect Effect( string name ) => _effects.Find( x => x.Name == name );

	public DrawnShape Shape( string name, LibraryItemKind kind ) => _shapes.Find( x => x.Name == name && LookLibrary.KindOf( x ) == kind );

	/// <summary>The drawn shape called <paramref name="name"/>, of any kind (for a completion's picture); null for none.</summary>
	internal DrawnShape ShapeNamed( string name ) => _shapes.Find( x => x.Name == name );

	private void Add( DrawnShape shape )
	{
		_shapes.Add( shape );
		_items.Add( new LibraryItem( shape.Name, LookLibrary.KindOf( shape ), shape.Content ) );
	}
}
