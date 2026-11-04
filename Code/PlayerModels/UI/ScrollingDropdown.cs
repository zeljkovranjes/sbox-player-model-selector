using System;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// A dropdown in the selector's own look: a box like its text boxes with the item picked and a chevron, opening a
/// <see cref="DropdownList"/> however long the list is (the standard shapes and the player's own, every joint): as tall
/// as the screen allows, scrolled with the wheel or its scrollbar, opened on the item picked, with a search box when it
/// is long, and worked from the keyboard: Up and Down move through it, Enter picks, Escape closes, and typing searches
/// or jumps to the first item starting with what was typed.
/// </summary>
public sealed class ScrollingDropdown : UIDropdown
{
	/// <summary>Whether items named "Group: name" show under a heading per group, as "name".</summary>
	[Parameter] public bool Grouped { get; set; }

	/// <summary>Makes the small picture shown beside an item, by its value; null (or a null picture) for none.</summary>
	[Parameter] public Func<object, Panel> Thumbnail { get; set; }

	// The list open now, and whether the press that opens one is the one that just closed it
	private DropdownList _list;
	private bool _closedByThisPress;

	public ScrollingDropdown()
	{
		// The game's dropdown underneath (its value and chevron), in our look (ScrollingDropdown.cs.scss)
		ElementName = "uidropdown";
		AddClass( "picker" );
		AcceptsFocus = true;
		OpenPicker = OpenList;
	}

	private bool IsOpen => _list.IsValid() && !_list.IsDeleting;

	public override void Tick()
	{
		base.Tick();

		var open = IsOpen;
		SetClass( "open", open );
		foreach ( var part in Children )
		{
			if ( part is IconPanel chevron )
				chevron.Text = open ? "expand_less" : "expand_more";
		}
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		_closedByThisPress = IsOpen;
		base.OnMouseDown( e );
	}

	private void OpenList()
	{
		// a press on the box while its list is open closes it (the press elsewhere usually has already)
		if ( _closedByThisPress )
		{
			_closedByThisPress = false;
			if ( IsOpen )
				_list.Delete();

			return;
		}

		if ( IsOpen )
			return;

		_list = new DropdownList( this, Items, Value, Tokenize, Grouped, Thumbnail, picked =>
		{
			Value = picked.Value;
			ValueChanged?.Invoke( picked.Value );
		} );

		// a long list's search box takes the keys once it shows; a short list's go to the box
		if ( !_list.Searches )
			Focus();
	}

	public override void OnButtonTyped( ButtonEvent e )
	{
		if ( IsOpen && _list.Key( e.Button ) )
		{
			e.StopPropagation = true;
			return;
		}

		base.OnButtonTyped( e );
	}

	public override void OnKeyTyped( char k )
	{
		if ( !IsOpen || char.IsControl( k ) )
		{
			base.OnKeyTyped( k );
			return;
		}

		_list.Typed( k );
	}
}
