using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;

namespace PlayerModels.UI;

/// <summary>
/// The open list of a <see cref="ScrollingDropdown"/>: a card floating under the box (over it when there is more room
/// above), as tall as the screen allows, that scrolls with the wheel or its scrollbar and opens on the item picked. Its
/// rows can carry a small picture and sit under group headings ("Shape: Circle" under "Shape"); a long list has a search
/// box at its top. Up and Down move through it, Enter picks, Escape closes, and typing searches (or, in a short list,
/// jumps to the first item starting with what was typed). A click anywhere else closes it.
/// </summary>
public sealed class DropdownList : Popup
{
	/// <summary>Lists this long get a search box.</summary>
	internal const int SearchFrom = 12;

	// Room kept between the card and the screen's edges, and between the card and the box
	private const float ScreenMargin = 12f;
	private const float SourceGap = 3f;

	// How tall a row and a heading are, for telling how tall the card wants to be before it is laid out
	private const float RowHeight = 26f;
	private const float HeadingHeight = 24f;
	private const float SearchHeight = 34f;
	private const float ScrollbarWidth = 8f;

	// Typing within this long of the last key adds to what was typed; later, it starts again
	private const float TypedForgetSeconds = 1f;

	// How long after opening the search box keeps asking for the keys: focus asked before it is shown is dropped
	private const float FocusSeconds = 0.5f;

	private readonly List<UIDropdown.Item> _items;
	private readonly object _picked;
	private readonly bool _tokenize;
	private readonly bool _grouped;
	private readonly Func<object, Panel> _thumbnail;
	private readonly Action<UIDropdown.Item> _pick;
	private readonly Panel _rows;
	private readonly SearchBox _search;
	private readonly List<Panel> _shown = new();
	private string _filter = "";
	private bool _revealed;
	private Panel _highlighted;
	private string _typed = "";
	private RealTimeSince _sinceTyped;
	private RealTimeSince _sinceOpened;

	/// <summary>
	/// Opens the list under <paramref name="source"/>. <paramref name="grouped"/> shows "Group: name" items under a heading
	/// per group; <paramref name="thumbnail"/>, when given, makes an item's picture (or null for none).
	/// </summary>
	public DropdownList( Panel source, List<UIDropdown.Item> items, object picked, bool tokenize, bool grouped,
		Func<object, Panel> thumbnail, Action<UIDropdown.Item> pick )
		: base( source, PositionMode.BelowStretch, SourceGap )
	{
		_items = items ?? new List<UIDropdown.Item>();
		_picked = picked;
		_tokenize = tokenize;
		_grouped = grouped;
		_thumbnail = thumbnail;
		_pick = pick;
		_sinceOpened = 0;
		CloseWhenParentIsHidden = true;

		if ( _items.Count >= SearchFrom )
		{
			_search = AddChild<SearchBox>( "search" );
			_search.List = this;
			_search.Placeholder = "Search";
		}

		_rows = Add.Panel( "rows" );
		_rows.Style.ScrollbarWidth = Length.Pixels( ScrollbarWidth );
		Fill();
	}

	/// <summary>Whether its search box takes the keys (a long list), rather than the box that opened it.</summary>
	internal bool Searches => _search is not null;

	// keeps a popup the dropdown sits in, like a resource picker, open while the list is used
	protected override BasePopup ParentPopup => PopupSource?.AncestorsAndSelf.OfType<BasePopup>().FirstOrDefault();

	public override void Tick()
	{
		base.Tick();
		if ( !this.IsValid() || IsDeleting || !PopupSource.IsValid() )
			return;

		if ( _search is not null && (_search.Text ?? "") != _filter )
		{
			_filter = _search.Text ?? "";
			Fill();
		}

		Place();

		// Laid out once: on the item picked
		if ( !_revealed && _rows.Box.Rect.Height > 0f )
		{
			_revealed = true;
			if ( _highlighted is not null )
				Reveal( _highlighted, true );
		}

		// The keys in the search box once it shows
		if ( _search is not null && _sinceOpened < FocusSeconds && !_search.HasFocus && _search.IsVisible )
			_search.Focus();
	}

	// Under the box, or over it when there is more room above and not enough below; never past the screen
	private void Place()
	{
		var scale = PopupSource.ScaleFromScreen;
		var source = PopupSource.Box.Rect * scale;
		var screen = (FindRootPanel()?.Box.Rect.Height ?? 0f) * scale;
		if ( screen <= 0f )
			return;

		var below = screen - source.Bottom - SourceGap - ScreenMargin;
		var above = source.Top - SourceGap - ScreenMargin;
		var wanted = WantedHeight();

		if ( wanted > below && above > below )
		{
			Style.Top = null;
			Style.Bottom = screen - source.Top + SourceGap;
			Style.MaxHeight = Math.Max( above, RowHeight * 3f );
		}
		else
		{
			Style.Bottom = null;
			Style.MaxHeight = Math.Max( below, RowHeight * 3f );
		}

		Style.Dirty();
	}

	private float WantedHeight()
	{
		var height = (_search is null ? 0f : SearchHeight) + 10f;
		foreach ( var row in _shown )
			height += row.HasClass( "group-header" ) ? HeadingHeight : RowHeight;

		return height;
	}

	// The rows again, for what the search box holds: an item matches when its name has every word typed
	private void Fill()
	{
		_rows.DeleteChildren( true );
		_shown.Clear();
		_highlighted = null;

		var words = _filter.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		string group = null;
		var afterGroup = false;
		foreach ( var item in _items )
		{
			var (heading, name) = _grouped ? Split( item.Label ) : (null, item.Label);
			if ( !Matches( item.Label, words ) )
				continue;

			if ( heading != group )
			{
				if ( heading is not null )
					_shown.Add( _rows.Add.Label( heading, "group-header" ) );
				else if ( afterGroup )
					_shown.Add( _rows.Add.Panel( "separator" ) );

				group = heading;
				afterGroup = heading is not null;
			}

			_shown.Add( Row( item, name ) );
		}

		if ( _shown.Count == 0 )
			_rows.Add.Label( "Nothing matches", "empty" );

		_highlighted ??= FirstRow();
		_highlighted?.SetClass( "active", true );
		SelectedChild = _highlighted;
		_revealed = false;
	}

	private Panel Row( UIDropdown.Item item, string name )
	{
		var row = _rows.Add.Panel( "option" );
		var isPicked = Equals( item.Value, _picked );
		row.SetClass( "selected", isPicked );

		if ( _thumbnail?.Invoke( item.Value ) is { } picture )
		{
			picture.AddClass( "thumb" );
			row.AddChild( picture );
		}

		var label = row.Add.Label( "", "text" );
		label.Tokenize = _tokenize;
		label.Text = name;
		if ( item.Font is not null )
			label.Style.FontFamily = item.Font;

		row.Add.Label( isPicked ? "check" : "", "check" );
		row.AddEventListener( "onclick", () => Pick( item ) );
		row.AddEventListener( "onmouseover", () => Highlight( row, false ) );

		if ( isPicked && _filter == "" )
			_highlighted = row;

		row.UserData = item;
		return row;
	}

	private void Pick( UIDropdown.Item item )
	{
		_pick?.Invoke( item );
		Delete();
	}

	// "Shape: Circle" as the heading "Shape" and the row "Circle"; a name with no group stands alone
	private static (string Heading, string Name) Split( string label )
	{
		var colon = label?.IndexOf( ": ", StringComparison.Ordinal ) ?? -1;
		return colon > 0 ? (label[ ..colon ], label[ (colon + 2).. ]) : (null, label);
	}

	private static bool Matches( string label, string[] words )
	{
		foreach ( var word in words )
		{
			if ( label?.Contains( word, StringComparison.OrdinalIgnoreCase ) != true )
				return false;
		}

		return true;
	}

	private Panel FirstRow()
	{
		foreach ( var row in _shown )
		{
			if ( row.HasClass( "option" ) )
				return row;
		}

		return null;
	}

	/// <summary>Works the list from a key: Up and Down move, Enter picks, Escape closes. False for a key it doesn't use.</summary>
	internal bool Key( string button )
	{
		switch ( button )
		{
			case "up":
				Move( -1 );
				return true;
			case "down":
				Move( 1 );
				return true;
			case "enter":
			case "pad_enter":
				if ( _highlighted?.UserData is UIDropdown.Item item )
					Pick( item );

				return true;
			case "escape":
				Delete();
				return true;
			default:
				return false;
		}
	}

	/// <summary>Jumps to the first item starting with what was typed (a list without a search box).</summary>
	internal void Typed( char key )
	{
		_typed = _sinceTyped > TypedForgetSeconds ? key.ToString() : _typed + key;
		_sinceTyped = 0;
		foreach ( var row in _shown )
		{
			if ( row.UserData is UIDropdown.Item item && (_grouped ? Split( item.Label ).Name : item.Label)?.StartsWith( _typed, StringComparison.OrdinalIgnoreCase ) == true )
			{
				Highlight( row, true );
				return;
			}
		}
	}

	// The next row up or down, stopping at either end
	private void Move( int by )
	{
		var rows = _shown.FindAll( x => x.HasClass( "option" ) );
		if ( rows.Count == 0 )
			return;

		var at = _highlighted is null ? (by > 0 ? -1 : rows.Count) : rows.IndexOf( _highlighted );
		Highlight( rows[ Math.Clamp( at + by, 0, rows.Count - 1 ) ], true );
	}

	private void Highlight( Panel row, bool reveal )
	{
		if ( row == _highlighted )
			return;

		_highlighted?.SetClass( "active", false );
		_highlighted = row;
		SelectedChild = row;
		row.SetClass( "active", true );
		if ( reveal )
			Reveal( row, false );
	}

	// The row scrolled into view: to the middle when the list opens on it, else just inside the nearest edge
	private void Reveal( Panel row, bool centre )
	{
		var top = row.Box.Rect.Top - _rows.Box.Rect.Top + _rows.ScrollOffset.y;
		var shown = _rows.Box.Rect.Height;
		var height = row.Box.Rect.Height;
		var offset = _rows.ScrollOffset;
		if ( centre )
			offset.y = top - (shown - height) * 0.5f;
		else if ( top < offset.y )
			offset.y = top;
		else if ( top + height > offset.y + shown )
			offset.y = top + height - shown;
		else
			return;

		offset.y = Math.Clamp( offset.y, 0f, Math.Max( 0f, _rows.ScrollSize.y ) );
		_rows.ScrollOffset = offset;
	}

	/// <summary>The search box at the top of a long list: it takes the typing, and gives the list its moving keys.</summary>
	private sealed class SearchBox : TextEntry
	{
		public DropdownList List { get; set; }

		public override void OnButtonTyped( ButtonEvent e )
		{
			if ( List.IsValid() && List.Key( e.Button ) )
			{
				e.StopPropagation = true;
				return;
			}

			base.OnButtonTyped( e );
		}
	}
}
