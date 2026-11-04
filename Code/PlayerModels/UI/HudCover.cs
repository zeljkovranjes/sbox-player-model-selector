using System.Linq;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// Keeps the menu, and the windows this addon opens in it, over the game's HUD while one of them is open: the menu draws
/// at 1000 and the HUD's health at 1050. Each window holds it every frame, so a menu built again or another window closing
/// never leaves the HUD over one still open; the last window to close lets it go.
/// </summary>
internal static class HudCover
{
	// Over the HUD (1050), under what is dragged (10000)
	private const int AboveHud = 1100;

	/// <summary>Puts the menu over the HUD, if it isn't already.</summary>
	internal static void Hold()
	{
		var menu = Menu();
		if ( menu is not null && menu.Style.ZIndex != AboveHud )
			menu.Style.ZIndex = AboveHud;
	}

	/// <summary>Lets the menu back under the HUD, unless another of the addon's windows is still open in it.</summary>
	internal static void Release( Panel closing )
	{
		var menu = Menu();
		if ( menu is null || menu.Descendants.Any( x => x != closing && x is AdvancedEditor or CodeEditor or ScriptReferencePanel ) )
			return;

		menu.Style.ZIndex = null;
	}

	private static Panel Menu() => Game.ActiveScene?.Get<SpawnMenuHost>()?.Panel;
}
