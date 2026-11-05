using System.Collections.Generic;
using System.Linq;

namespace PlayerModels.Core.Script;

/// <summary>
/// What the scripting reference's live examples take by name: the player's own (see <see cref="IPlayerLibrary"/>), then a
/// stand-in for each name the examples of <c>Shapes.Mine</c>, <c>Stickers.Mine</c>, <c>Emblems.Mine</c> and
/// <c>Effects.Mine</c> use, so those run for a player with nothing of that name yet. Only for an example shown in the
/// reference: a script that is saved or worn takes the player's own and nothing else.
/// </summary>
public sealed class ExampleLibrary : IPlayerLibrary
{
	private const string StockEffect = "Fire hands";

	private readonly IPlayerLibrary _own;
	private readonly List<DrawnShape> _standIns = new() { Cool(), GoldSkull(), Clan() };
	private readonly List<LibraryItem> _items;

	public ExampleLibrary( IPlayerLibrary own )
	{
		_own = own;
		_items = new List<LibraryItem>( own?.Items ?? new List<LibraryItem>() );
		var standIns = new List<LibraryItem> { new( StockEffect, LibraryItemKind.Effect, StockEffect ) };
		foreach ( var shape in _standIns )
			LookLibrary.AddItems( standIns, shape );

		_items.AddRange( standIns.Where( x => !_items.Exists( mine => mine.Name == x.Name && mine.Kind == x.Kind ) ) );
	}

	public IReadOnlyList<LibraryItem> Items => _items;

	public Effect Effect( string name ) => _own?.Effect( name ) ?? (name == StockEffect ? EffectPresets.All.FirstOrDefault( x => x.Name == name ) : null);

	public DrawnShape Shape( string name, LibraryItemKind kind ) => _own?.Shape( name, kind ) ?? _standIns.Find( x => x.Name == name && LookLibrary.Is( x, kind ) );

	private static DrawnShape Cool() => DrawnShape.From( HersheyFont.Strokes( "COOL" ), "COOL" );

	private static DrawnShape GoldSkull() => new DrawnShape
	{
		Name = "Gold skull",
		Emblem = new List<EmblemLayer>
		{
			new() { Shape = nameof( StandardShape.RoundedRectangle ), Y = -0.55f, ScaleX = 0.42f, ScaleY = 0.32f, Color = "#ffd700" },
			new() { Shape = nameof( StandardShape.Circle ), Y = 0.15f, ScaleX = 0.75f, ScaleY = 0.75f, Color = "#ffd700" },
			new() { Shape = nameof( StandardShape.Circle ), X = -0.3f, Y = 0.2f, ScaleX = 0.2f, ScaleY = 0.2f, Color = "#201000" },
			new() { Shape = nameof( StandardShape.Circle ), X = 0.3f, Y = 0.2f, ScaleX = 0.2f, ScaleY = 0.2f, Color = "#201000" },
			new() { Shape = nameof( StandardShape.Triangle ), Y = -0.2f, ScaleX = 0.1f, ScaleY = 0.12f, Color = "#201000" },
		},
	}.Sanitized();

	private static DrawnShape Clan() => new DrawnShape
	{
		Name = "Clan",
		Emblem = new List<EmblemLayer>
		{
			new() { Shape = nameof( StandardShape.Hexagon ), Color = "#203060", OutlineColor = "#ffd000", OutlineWidth = 0.05f },
			new() { Shape = nameof( StandardShape.Star5 ), ScaleX = 0.6f, ScaleY = 0.6f, Color = "#ffd000" },
		},
	}.Sanitized();
}
