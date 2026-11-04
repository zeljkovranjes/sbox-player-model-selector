using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// A right-click menu: a list of entries (see <see cref="Entry"/>), each with its picture, words, the key that does the
/// same and whether it can be chosen now (and why not), opened at the cursor and kept on the screen. A click on an entry
/// does it; Esc or a click anywhere else closes it (a see-through backdrop under it takes that click). Every entry is
/// something the window also does another way, so nothing needs a right click; Shift+F10 or the menu key opens one from
/// the keyboard where a window asks (see <see cref="KeyPressed"/>). One open at a time: <see cref="Open"/> shows it,
/// <see cref="Close"/> hides it. Its look is ContextMenu.scss, which a window that opens one imports.
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
	private static Panel _backdrop;

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

		// Under the menu and over everything else: a click anywhere but the menu lands on it and closes the menu
		_backdrop = window.AddChild<Panel>( "context-backdrop" );
		_backdrop.AddEventListener( "onmousedown", Close );

		var menu = window.AddChild<ContextMenu>();
		menu._at = window.MousePosition;
		if ( title is not null )
			menu.AddChild<Label>( "title" ).Text = title;

		foreach ( var entry in entries )
			menu.AddEntry( entry );

		_open = menu;
		return menu;
	}

	/// <summary>
	/// Opens a menu as <see cref="Open"/> does on the window <paramref name="inside"/> is in: its nearest ancestor marked
	/// <c>menu-window</c> (the Advanced editor, the selector's page). Nothing opens outside one.
	/// </summary>
	public static ContextMenu OpenFrom( Panel inside, string title, IReadOnlyList<Entry> entries )
	{
		for ( var panel = inside; panel is not null; panel = panel.Parent )
		{
			if ( panel.HasClass( WindowClass ) )
				return Open( panel, title, entries );
		}

		return null;
	}

	/// <summary>The class of a panel menus open on from within it (see <see cref="OpenFrom"/>): it fills the screen and imports ContextMenu.scss.</summary>
	public const string WindowClass = "menu-window";

	/// <summary>Closes the menu open now, if any.</summary>
	public static void Close()
	{
		if ( _open.IsValid() )
			_open.Delete( true );

		if ( _backdrop.IsValid() )
			_backdrop.Delete( true );

		_open = null;
		_backdrop = null;
	}

	/// <summary>
	/// Whether Shift+F10 or the menu key was pressed this frame, for a window to open its menu on what is selected; read
	/// even while the window keeps the keys from the game (see InputLock).
	/// </summary>
	public static bool KeyPressed()
	{
		var suppressed = Input.Suppressed;
		Input.Suppressed = false;
		var pressed = Input.Keyboard.Pressed( "app" ) || Input.Keyboard.Down( "shift" ) && Input.Keyboard.Pressed( "f10" );
		Input.Suppressed = suppressed;
		return pressed;
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
}
