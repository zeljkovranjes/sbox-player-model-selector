using System;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// The game's dropdown, its open list usable however long it is (the standard shapes and the player's own, every joint):
/// as tall as the screen allows, scrolled with the wheel or its scrollbar, opened on the item picked, and worked from the
/// keyboard: Up and Down move through it, Enter picks, Escape closes, and typing jumps to the first item starting with
/// what was typed.
/// </summary>
public sealed class ScrollingDropdown : UIDropdown
{
	// Typing within this long of the last key adds to what was typed; later, it starts again
	private const float TypedForgetSeconds = 1f;

	// What the game's list calls each item's row; its scrollbar is as wide as the page's
	private const string Row = "option";
	private const float ScrollbarWidth = 8f;

	// The list open now (the game's popup, found once it opened), and whether it was scrolled to the item picked yet
	private Popup _list;
	private bool _revealed;
	private string _typed = "";
	private RealTimeSince _sinceTyped;

	public ScrollingDropdown()
	{
		// Styled as the game's dropdown is, by its stylesheets and ours alike
		ElementName = "uidropdown";
		AcceptsFocus = true;
	}

	protected override void OnClick( MousePanelEvent e )
	{
		base.OnClick( e );
		_list = null;
		_revealed = false;
		foreach ( var panel in FindPopupPanel()?.Children ?? Array.Empty<Panel>() )
		{
			if ( panel is Popup popup && popup.PopupSource == this && !popup.IsDeleting )
				_list = popup;
		}

		if ( _list is not null )
			Focus();
	}

	public override void Tick()
	{
		base.Tick();
		if ( _list is null )
			return;

		if ( !_list.IsValid() || _list.IsDeleting )
		{
			_list = null;
			return;
		}

		_list.Style.Overflow = OverflowMode.Scroll;
		_list.Style.ScrollbarWidth = Length.Pixels( ScrollbarWidth );

		// Laid out once: on the item picked
		if ( !_revealed && _list.Box.Rect.Height > 0f )
		{
			_revealed = true;
			foreach ( var row in _list.Children )
			{
				if ( row.HasClass( Row ) && row.HasClass( "active" ) )
					Select( row );
			}
		}
	}

	public override void OnButtonTyped( ButtonEvent e )
	{
		if ( _list?.IsValid() != true )
		{
			base.OnButtonTyped( e );
			return;
		}

		switch ( e.Button )
		{
			case "up":
				Move( -1 );
				break;
			case "down":
				Move( 1 );
				break;
			case "enter":
			case "pad_enter":
				_list.SelectedChild?.CreateEvent( "onclick" );
				break;
			case "escape":
				_list.Delete();
				break;
			default:
				base.OnButtonTyped( e );
				return;
		}

		e.StopPropagation = true;
	}

	public override void OnKeyTyped( char k )
	{
		if ( _list?.IsValid() != true || char.IsControl( k ) )
		{
			base.OnKeyTyped( k );
			return;
		}

		_typed = _sinceTyped > TypedForgetSeconds ? k.ToString() : _typed + k;
		_sinceTyped = 0;
		foreach ( var row in _list.Children )
		{
			if ( row.HasClass( Row ) && LabelOf( row )?.StartsWith( _typed, StringComparison.OrdinalIgnoreCase ) == true )
			{
				Select( row );
				return;
			}
		}
	}

	// The next row up or down (the list's scrollbar is a child of it too, after the rows), stopping at either end
	private void Move( int by )
	{
		Panel previous = null;
		var passed = _list.SelectedChild is null;
		foreach ( var row in _list.Children )
		{
			if ( !row.HasClass( Row ) )
				continue;

			if ( passed && by > 0 )
			{
				Select( row );
				return;
			}

			if ( row == _list.SelectedChild )
			{
				if ( by < 0 )
				{
					if ( previous is not null )
						Select( previous );

					return;
				}

				passed = true;
			}

			previous = row;
		}
	}

	// The row marked as the one Enter picks, scrolled into view
	private void Select( Panel row )
	{
		_list.SelectedChild?.SetClass( "active", false );
		_list.SelectedChild = row;
		row.SetClass( "active", true );

		var top = row.Box.Rect.Top - _list.Box.Rect.Top + _list.ScrollOffset.y;
		var shown = _list.Box.Rect.Height;
		var offset = _list.ScrollOffset;
		if ( top < offset.y )
			offset.y = top;
		else if ( top + row.Box.Rect.Height > offset.y + shown )
			offset.y = top + row.Box.Rect.Height - shown;
		else
			return;

		_list.ScrollOffset = offset;
	}

	private static string LabelOf( Panel row )
	{
		foreach ( var part in row.Children )
		{
			if ( part is Label label )
				return label.Text;
		}

		return null;
	}
}
