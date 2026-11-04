using System;
using PlayerModels.Engine;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

// The code editor as a window: dragged by its title bar, sized from its edges and corner, maximized and restored
// (double-click the title), kept on screen, and opened where it was last (kept on this machine)
public partial class CodeEditor
{
	// Its size the first time, and the least it can be sized to, in the UI's units
	private const float StartWidth = 760f;
	private const float StartHeight = 560f;
	private const float LeastWidth = 380f;
	private const float LeastHeight = 240f;

	// How much of it always stays on screen, so its title bar can be reached
	private const float KeptOnScreen = 80f;

	private Rect _window;
	private Rect _grabbed;
	private bool _maximized;

	// Where it opens: where it was last, else beside the Advanced editor's options or in the middle
	private void PlaceWindow()
	{
		var screen = ScreenSize();
		if ( LocalPrefs.CodeWindow is { } kept )
		{
			_window = new Rect( kept.Left, kept.Top, kept.Width, kept.Height );
			_maximized = kept.Maximized;
		}
		else
		{
			var width = MathF.Min( StartWidth, screen.x * 0.6f );
			var height = MathF.Min( StartHeight, screen.y * 0.8f );
			var left = BesideOptions ? screen.x * 0.97f - width - screen.x * 0.3f : (screen.x - width) / 2f;
			_window = new Rect( MathF.Max( 0f, left ), (screen.y - height) / 2f, width, height );
		}

		KeepOnScreen();
		UseWindow();
	}

	private void StartMove()
	{
		if ( !_maximized )
			_grabbed = _window;
	}

	private void Move( Vector2 moved )
	{
		if ( _maximized )
			return;

		_window = new Rect( _grabbed.Left + moved.x, _grabbed.Top + moved.y, _grabbed.Width, _grabbed.Height );
		KeepOnScreen();
		UseWindow();
	}

	private void EndMove()
	{
		RememberWindow();
	}

	private void StartResize()
	{
		_grabbed = _window;
	}

	// Sized from an edge (sideways -1 left, 1 right; upwards -1 top, 1 bottom) by how far it was dragged
	private void Resize( Vector2 moved, int sideways, int upwards )
	{
		var left = _grabbed.Left;
		var top = _grabbed.Top;
		var width = _grabbed.Width;
		var height = _grabbed.Height;
		if ( sideways > 0 )
			width = MathF.Max( LeastWidth, width + moved.x );
		else if ( sideways < 0 )
		{
			var grown = MathF.Max( LeastWidth, width - moved.x );
			left += width - grown;
			width = grown;
		}

		if ( upwards > 0 )
			height = MathF.Max( LeastHeight, height + moved.y );
		else if ( upwards < 0 )
		{
			var grown = MathF.Max( LeastHeight, height - moved.y );
			top += height - grown;
			height = grown;
		}

		_window = new Rect( left, top, width, height );
		KeepOnScreen();
		UseWindow();
	}

	private void ToggleMaximize()
	{
		_maximized = !_maximized;
		UseWindow();
		RememberWindow();
		StateHasChanged();
	}

	private void KeepOnScreen()
	{
		var screen = ScreenSize();
		var width = Math.Clamp( _window.Width, LeastWidth, MathF.Max( LeastWidth, screen.x ) );
		var height = Math.Clamp( _window.Height, LeastHeight, MathF.Max( LeastHeight, screen.y ) );
		var left = Math.Clamp( _window.Left, KeptOnScreen - width, MathF.Max( 0f, screen.x - KeptOnScreen ) );
		var top = Math.Clamp( _window.Top, 0f, MathF.Max( 0f, screen.y - KeptOnScreen ) );
		_window = new Rect( left, top, width, height );
	}

	private void UseWindow()
	{
		if ( _maximized )
		{
			Style.Left = Length.Pixels( 0 );
			Style.Top = Length.Pixels( 0 );
			Style.Width = Length.Percent( 100 );
			Style.Height = Length.Percent( 100 );
			return;
		}

		Style.Left = Length.Pixels( _window.Left );
		Style.Top = Length.Pixels( _window.Top );
		Style.Width = Length.Pixels( _window.Width );
		Style.Height = Length.Pixels( _window.Height );
	}

	private void RememberWindow()
	{
		LocalPrefs.CodeWindow = (_window.Left, _window.Top, _window.Width, _window.Height, _maximized);
	}

	// The screen in the UI's units (the window's parent covers it)
	private Vector2 ScreenSize()
	{
		var scale = MathF.Max( ScaleToScreen, 0.01f );
		var size = Parent?.Box.Rect.Size ?? Screen.Size;
		return size.x > 0f ? size / scale : Screen.Size / scale;
	}
}
