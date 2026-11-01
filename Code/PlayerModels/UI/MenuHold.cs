using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// Keeps the spawn menu up for a window that lives in it: the menu only asks the panels in its popup layer. Draws nothing.
/// </summary>
internal sealed class MenuHold : Panel, IKeepsMenuOpen
{
	public MenuHold()
	{
		Style.Display = DisplayMode.None;
	}
}
