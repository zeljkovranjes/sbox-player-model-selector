using System.Text.Json;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.UI;

// The code editor's preview, beside the code: what is written as it is applied, shown on what it needs. An effect or a
// weapon's design shows on the player's own character (their model and look, holding the weapon they hold in the game);
// a weapon's skin on the weapon alone, close up, with its own animations (see ModelPreview.WeaponAlone); both with the
// test bar under them. A sticker shows flat on its own, turning, its finish shimmering. On by default, switched from the
// toolbar and kept on this machine
public partial class CodeEditor
{
	private enum Preview { Body, Sticker, Weapon }

	// How often the weapon held in the game is looked for; a weapon not held is held by this bone, as a pistol is
	private const float HeldInterval = 0.5f;
	private const string HoldBone = "hold_R";
	private const int PistolHold = 1;

	private Preview Shows => Kind switch { ScriptKind.Shape or ScriptKind.Sticker => Preview.Sticker, ScriptKind.Skin => Preview.Weapon, _ => Preview.Body };

	private ModelPreview _stage;
	private ShapeIcon _stickerShown;
	private bool _previewOn = LocalPrefs.CodePreview;
	private ModelPreview.HeldWeapon? _held;
	private RealTimeSince _sinceHeld = HeldInterval;

	// The look the weapon alone shows: a copy of the page's, made again whenever something is put on it (a skin's script
	// changes it in place, and the weapon is dressed again only for another look)
	private PlayerLook _lookShown;
	private string _lookOf;

	private void TogglePreview()
	{
		_previewOn = !_previewOn;
		LocalPrefs.CodePreview = _previewOn;
		StateHasChanged();
	}

	private void TickPreview()
	{
		if ( !_previewOn )
			return;

		if ( _stickerShown?.IsValid == true )
			_stickerShown.Shape = StickerNow?.Invoke();

		if ( _stage?.IsValid != true || LookNow?.Invoke() is not { } edited )
			return;

		if ( _sinceHeld >= HeldInterval )
		{
			_sinceHeld = 0;
			_held = ModelPreview.HeldInGame( out _ );

			// A skin shows on its own weapon, held in the game or not
			if ( Shows == Preview.Weapon && _held?.Model.ResourcePath != Weapon && Model.Load( Weapon ) is { IsError: false } model )
				_held = new ModelPreview.HeldWeapon( model, HoldBone, global::Transform.Zero, _held?.HoldType ?? PistolHold );
		}

		var look = ScriptPrograms.Built( Shows == Preview.Weapon ? Copied( edited ) : edited, System.Guid.Empty, ScriptLimits.MaxScriptBytes, false, out _ );
		_stage.WeaponAlone = Shows == Preview.Weapon;
		_stage.Look = look;
		_stage.Weapon = _held;
		_ = _stage.ShowAsync( look.Model ?? "" );
	}

	private PlayerLook Copied( PlayerLook look )
	{
		if ( _lookShown is null || !ReferenceEquals( _lookOf, _applied ) )
		{
			_lookOf = _applied;
			_lookShown = PlayerLook.Parse( look.Serialize() ) ?? look;
		}

		return _lookShown;
	}
}
