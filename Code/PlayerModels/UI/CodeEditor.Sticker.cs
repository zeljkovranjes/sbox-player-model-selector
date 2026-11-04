using System;
using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using PlayerModels.Engine;

namespace PlayerModels.UI;

// The code editor on the sticker being made in the creator's Sticker mode, in place of an effect: its layers as a shape
// script (each good build put on the sticker editor, its changes written into the code while the code is as it would
// write it, else put back saying why), or its own sticker script (see DrawnShape.Script), which colours its layers and
// moves its sprites
public partial class CodeEditor
{
	/// <summary>What of a sticker the code edits: its layers, or its own script.</summary>
	public enum StickerCode { Layers, Animation }

	/// <summary>What of the creator's sticker it edits; null for an effect or a weapon.</summary>
	public StickerCode? Sticker { get; set; }

	/// <summary>The sticker as the creator has it now (null while it has no layers).</summary>
	public Func<DrawnShape> StickerNow { get; init; }

	/// <summary>Called with the sticker a good build of its layers made, for the creator to show and keep.</summary>
	public Action<DrawnShape> StickerBuilt { get; init; }

	/// <summary>Called with the sticker's own script (minified; null for none), for the creator to keep.</summary>
	public Action<string> StickerScriptChanged { get; init; }

	// A sticker script started from nothing: each layer's colour going round the wheel, one after another
	private const string StickerStarter = """
		-- This sticker's own script: runs for each layer as it shows (i the layer, origin where it is), and for each sprite
		-- showing the sticker
		color = Color.FromHsv( (t * 90 + i * 40) % 360, 0.8, 1 )
		""";

	// The layers' code as the creator's sticker would be written (see ScriptCodegen.Sticker), the sticker it was written
	// from and the script it opened with: while the code is still that, the creator's changes write it again
	private string _stickerWritten;
	private DrawnShape _stickerOpened;
	private string _scriptOpened;

	private string OpenSticker()
	{
		var sticker = StickerNow?.Invoke();
		_stickerOpened = sticker;
		_scriptOpened = sticker?.Script;
		_applied = sticker?.Content;
		_name = $"{(string.IsNullOrEmpty( sticker?.Name ) ? "Sticker" : sticker.Name)} {(Sticker == StickerCode.Layers ? "layers" : "script")}";
		if ( Sticker == StickerCode.Animation )
			return Formatter.Format( sticker?.Script ?? StickerStarter ) ?? StickerStarter;

		_stickerWritten = Written( sticker );
		return _stickerWritten;
	}

	private static string Written( DrawnShape sticker )
	{
		var code = ScriptCodegen.Sticker( sticker );
		return Formatter.Format( code ) ?? code;
	}

	// A good compile on the sticker: its layers built (as the creator's, its name, finish and script kept), or its script
	private void PutSticker( ScriptBuild build )
	{
		if ( Sticker == StickerCode.Animation )
		{
			_putting = true;
			try
			{
				StickerScriptChanged?.Invoke( Minifier.Minify( _source ) );
			}
			finally
			{
				_putting = false;
			}

			_applied = StickerNow?.Invoke()?.Content;
			return;
		}

		if ( build?.Shape is not { IsEmblem: true } made )
		{
			_note = "A sticker is made of layers: add them with shape.AddLayer( ... )";
			return;
		}

		Putting( made );
	}

	// The creator shown made, its change not followed back into the code
	private bool _putting;

	private void Putting( DrawnShape made )
	{
		_putting = true;
		try
		{
			StickerBuilt?.Invoke( made );
		}
		finally
		{
			_putting = false;
		}

		_applied = StickerNow?.Invoke()?.Content;
	}

	// One run of a sticker script, as a layer runs it, so a fault shows here first; true when it ran
	private bool RunsAsSticker( ScriptProgram program )
	{
		var io = new ScriptIO();
		io.Set( ScriptInput.Time, 1500 );
		io.Set( ScriptInput.Seconds, 1.5 );
		io.Set( ScriptInput.Index, 1 );
		io.Set( ScriptInput.Count, 4 );
		io.Set( ScriptInput.OriginX, 0.5, -0.25, 0 );
		var machine = new ScriptMachine( program );
		if ( machine.RunElement( io, ScriptPrograms.Stack ) )
			return true;

		_errors = new List<ScriptError> { machine.Fault.Describe( program ) };
		return false;
	}

	/// <summary>
	/// Follows the creator's change to the sticker: its layers' code written again while it is still the code the editor
	/// would write; code of the player's own (a loop, a sum) can't be rewritten, so the change is put back, saying why.
	/// </summary>
	private void SyncSticker()
	{
		var sticker = StickerNow?.Invoke();
		if ( _entry is null || _putting || sticker?.Content == _applied )
			return;

		_applied = sticker?.Content;
		if ( Sticker == StickerCode.Animation )
		{
			if ( sticker?.Script is { } script && script != Minifier.Minify( _source ) )
			{
				_entry.Rewrite( Formatter.Format( script ) ?? script );
				_source = _entry.Source;
				Compile( false );
			}

			return;
		}

		if ( _source == _stickerWritten || _errors.Count > 0 && _build?.Shape is null )
		{
			_stickerWritten = Written( sticker );
			_entry.Rewrite( _stickerWritten );
			_source = _entry.Source;
			Compile( false );
			return;
		}

		if ( _build?.Shape is { IsEmblem: true } built )
			Putting( built );

		_note = "The code works these layers out: change them in the code";
		StateHasChanged();
	}

	// Discarded: the sticker as it was when the code opened
	private void PutBackSticker()
	{
		if ( Sticker == StickerCode.Animation )
			StickerScriptChanged?.Invoke( _scriptOpened );
		else if ( _stickerOpened is { IsEmblem: true } opened )
			Putting( opened );
	}
}
