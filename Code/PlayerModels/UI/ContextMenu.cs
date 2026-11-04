using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// A right-click menu: a list of entries (see <see cref="Entry"/>), each with its picture, words, the key that does the
/// same and whether it can be chosen now (and why not), opened at the cursor and kept on the screen. A click on an entry
/// does it; Esc or a click anywhere else closes it. Every entry is something the window also does another way, so
/// nothing needs a right click. One open at a time: <see cref="Open"/> shows it, <see cref="Close"/> hides it. Its look is
/// ContextMenu.scss, which a window that opens one imports.
/// </summary>
public sealed class ContextMenu : Panel
{
	/// <summary>
	/// One thing the menu offers: its words, a Material icon, the key that does the same (shown, not listened for), whether
	/// it can be chosen now and why not, and what it does.
	/// </summary>
	public readonly record struct Entry( string Label, string Icon, Action Action, string Shortcut = null, bool Enabled = true, string Reason = null );

	// How far from the screen's edge the menu keeps
	private const float EdgePixels = 8f;

	private static ContextMenu _open;

	private Vector2 _at;
	private bool _placed;

	/// <summary>Whether a menu is open now.</summary>
	public static bool IsOpen => _open.IsValid();

	/// <summary>
	/// Opens a menu of <paramref name="entries"/> titled <paramref name="title"/> (none for null) on <paramref name="window"/>
	/// (a panel filling the screen, whose styles import ContextMenu.scss) at the cursor, in place of any menu open now.
	/// </summary>
	public static ContextMenu Open( Panel window, string title, IReadOnlyList<Entry> entries )
	{
		Close();
		if ( window is null || entries.Count == 0 )
			return null;

		var menu = window.AddChild<ContextMenu>();
		menu._at = window.MousePosition;
		if ( title is not null )
			menu.AddChild<Label>( "title" ).Text = title;

		foreach ( var entry in entries )
			menu.AddEntry( entry );

		_open = menu;
		return menu;
	}

	/// <summary>Closes the menu open now, if any.</summary>
	public static void Close()
	{
		if ( _open.IsValid() )
			_open.Delete( true );

		_open = null;
	}

	private void AddEntry( Entry entry )
	{
		var row = AddChild<Panel>( "entry" );
		row.SetClass( "disabled", !entry.Enabled );
		row.AddChild<Label>( "icon" ).Text = entry.Icon ?? "";
		row.AddChild<Label>( "label" ).Text = entry.Label;
		row.AddChild<Label>( "shortcut" ).Text = entry.Shortcut ?? "";
		if ( !entry.Enabled && entry.Reason is not null )
			row.Tooltip = entry.Reason;

		row.AddEventListener( "onclick", () =>
		{
			if ( !entry.Enabled )
				return;

			Close();
			entry.Action?.Invoke();
		} );
	}

	public override void Tick()
	{
		base.Tick();

		// Kept on the window once its size is known: moved left or up from the cursor when it would run off
		var size = Box.Rect.Size;
		if ( !_placed && size.x > 0f && Parent is { } window )
		{
			var room = window.Box.Rect.Size;
			var scale = ScaleFromScreen;
			var x = MathF.Min( _at.x, room.x - size.x - EdgePixels );
			var y = MathF.Min( _at.y, room.y - size.y - EdgePixels );
			Style.Left = Length.Pixels( MathF.Max( EdgePixels, x ) * scale );
			Style.Top = Length.Pixels( MathF.Max( EdgePixels, y ) * scale );
			_placed = true;
		}

		if ( Input.Keyboard.Pressed( "escape" ) )
			Close();
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		// Clicks on the menu are its own
		e.StopPropagation();
		base.OnMouseDown( e );
	}

	/// <summary>Closes the menu when the window behind it is clicked: call from the window's own mouse handling.</summary>
	public static void ClickedElsewhere( Panel clicked )
	{
		if ( _open.IsValid() && (clicked is null || !clicked.IsAncestor( _open ) && clicked != _open) )
			Close();
	}
}
