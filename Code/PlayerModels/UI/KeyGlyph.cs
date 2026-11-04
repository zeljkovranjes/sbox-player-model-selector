using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>The picture of a key on the keyboard, the way the game shows its own keys; nothing without one.</summary>
public sealed class KeyGlyph : Panel
{
	/// <summary>The key's name, as the keyboard reads it ("g", "f5", "mouse4"); null or empty for none.</summary>
	[Parameter] public string Key { get; set; }

	private string _shown = "";

	public override void Tick()
	{
		base.Tick();

		var key = Key ?? "";
		if ( key == _shown )
			return;

		_shown = key;
		Style.BackgroundImage = key == "" ? null : Input.Keyboard.GetGlyph( key, InputGlyphSize.Small, false );
		SetClass( "none", key == "" );
	}
}
