using System;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// Something to drag a window by: its title bar, or one of its edges or corners to size it. Says where the mouse went
/// since the drag started, in the UI's units, however far it went (the engine keeps the drag while the button is held,
/// over the window or not), and a double-click (the title bar's maximize).
/// </summary>
public sealed class DragHandle : Panel
{
	/// <summary>Called as a drag starts, as it goes (with how far the mouse moved since it started) and when it ends.</summary>
	[Parameter] public Action Started { get; set; }
	[Parameter] public Action<Vector2> Moved { get; set; }
	[Parameter] public Action Ended { get; set; }

	/// <summary>Called on a double-click.</summary>
	[Parameter] public Action DoubleClicked { get; set; }

	protected override void OnDragStart( DragEvent e )
	{
		e.StopPropagation();
		Started?.Invoke();
	}

	protected override void OnDrag( DragEvent e )
	{
		e.StopPropagation();
		Moved?.Invoke( (e.ScreenPosition - e.ScreenGrabPosition) / MathF.Max( ScaleToScreen, 0.01f ) );
	}

	protected override void OnDragEnd( DragEvent e )
	{
		e.StopPropagation();
		Moved?.Invoke( (e.ScreenPosition - e.ScreenGrabPosition) / MathF.Max( ScaleToScreen, 0.01f ) );
		Ended?.Invoke();
	}

	protected override void OnDragCancel( DragEvent e )
	{
		e.StopPropagation();
		Ended?.Invoke();
	}

	protected override void OnDoubleClick( MousePanelEvent e )
	{
		DoubleClicked?.Invoke();
		e.StopPropagation();
	}
}
