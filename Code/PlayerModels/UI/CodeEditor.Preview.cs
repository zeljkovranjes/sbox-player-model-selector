using System;
using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.UI;

// The code editor's preview: the player's own character beside the code (their model and look as the code applies it,
// holding the weapon they hold in the game), the test bar under it; a sticker shows worn over the head. On by default,
// switched from the toolbar and kept on this machine
public partial class CodeEditor
{
	/// <summary>The look a sticker shows on: the player's own, as they are editing it. Only for a sticker.</summary>
	public Func<PlayerLook> Wearer { get; init; }

	// How often the weapon held in the game is looked for
	private const float HeldInterval = 0.5f;

	// A sticker worn in the preview: one sprite of it this far over the crown, this big
	private const float StickerAbove = 8f;
	private const float StickerSize = 12f;

	private ModelPreview _stage;
	private bool _previewOn = LocalPrefs.CodePreview;
	private ModelPreview.HeldWeapon? _held;
	private RealTimeSince _sinceHeld = HeldInterval;
	private DrawnShape _stickerShown;
	private PlayerLook _stickerLook;

	private void TogglePreview()
	{
		_previewOn = !_previewOn;
		LocalPrefs.CodePreview = _previewOn;
		StateHasChanged();
	}

	private void TickPreview()
	{
		if ( !_previewOn || _stage?.IsValid != true || PreviewLook() is not { } look )
			return;

		if ( _sinceHeld >= HeldInterval )
		{
			_sinceHeld = 0;
			_held = ModelPreview.HeldInGame( out _ );
		}

		_stage.Look = look;
		_stage.Weapon = _held;
		_ = _stage.ShowAsync( look.Model ?? "" );
	}

	// The look as the code has it now: the page's (each good build is put on it), or for a sticker the player's own wearing it
	private PlayerLook PreviewLook()
	{
		if ( Sticker is null )
			return LookNow?.Invoke() is { } edited ? Built( edited ) : null;

		var sticker = StickerNow?.Invoke();
		if ( _stickerLook is null || !ReferenceEquals( sticker, _stickerShown ) )
		{
			_stickerShown = sticker;
			_stickerLook = Built( Wearing( Wearer?.Invoke() ?? new PlayerLook(), sticker ) );
		}

		return _stickerLook;
	}

	private static PlayerLook Built( PlayerLook look ) => ScriptPrograms.Built( look, Guid.Empty, ScriptLimits.MaxScriptBytes, false, out _ );

	// The wearer without their effects, the sticker over their head instead (a sticker shows its own colours)
	private static PlayerLook Wearing( PlayerLook wearer, DrawnShape sticker )
	{
		var look = wearer.WithoutEffects();
		if ( sticker is null || string.IsNullOrEmpty( sticker.Id ) )
			return look;

		var layer = new EffectLayer { Picture = sticker.Reference, Keep = 1f, Amount = 1f, Size = StickerSize, Follow = true };
		if ( sticker.HasOwnColours )
			layer.Solid = true;

		var effect = new Effect
		{
			Id = "e1",
			Name = sticker.Name ?? "Sticker",
			Anchor = new EffectAnchor { Joint = nameof( Effect.Joint.Crown ), Up = StickerAbove },
			Layers = new List<EffectLayer> { layer },
			Shapes = new List<DrawnShape> { sticker },
		};

		look.Adopt( effect );
		return look;
	}
}
