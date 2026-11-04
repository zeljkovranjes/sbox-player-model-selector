using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.UI;

// The code editor's script: opened formatted, compiled after a pause in typing (Auto-apply puts each good build on the
// effect for the preview; off, only Save does), saved by putting it on the effect and wearing it, kept in step with the
// side editor both ways (see ScriptSync), and its errors shown quietly: underlined and counted, never mid-word
public partial class CodeEditor
{
	// How long typing pauses before the script compiles, and before an error on the line being typed shows
	private const float CompileAfter = 0.3f;
	private const float ErrorsAfter = 1f;

	// A new effect written in code starts as this
	private const string Starter = """
		Effect.Name = "My effect"
		Effect.Joint = Joint.Chest

		local glow = Sprite()
		glow.Color = Color.Parse( "#7af0ff" )
		glow.Size = 3
		glow.Amount = 20
		""";

	private string _source = "";
	private string _saved = "";
	private string _drafted;

	// The draft this script is kept as (see Drafts), once it is; and the oldest draft, when keeping one asks to drop it
	private string _draftId;
	private PlayerModels.Core.Draft _confirmDrop;
	private bool _closeAfterDraft;
	private string _name = "";
	private bool _autoApply = true;
	private MyLibrary _library;
	private RealTimeSince _sinceEdit;
	private bool _pending;

	// The last compile (its errors and size), the last program that compiled (for keys and help), the last good build and
	// the text it was of, and what was last put on the effect (to tell the side editor's changes from ours)
	private List<ScriptError> _errors = new();
	private int _storedBytes;
	private ScriptProgram _program;
	private ScriptBuild _build;
	private string _applied;
	private string _openedAs;
	private bool _errorsQuiet;
	private int _caretLayer = -1;

	private bool IsDirty => _source != _saved && _source != _drafted;

	// Opens the effect's script formatted (a visual effect as the script it would be, a draft as it was left)
	private void Open()
	{
		_autoApply = LocalPrefs.CodeAutoApply;
		(_matchCase, _wholeWord) = LocalPrefs.CodeFind;
		var look = LookNow?.Invoke();
		_library = new MyLibrary( look?.DrawnShapes );
		var effect = EffectOf( look );
		_openedAs = effect is null ? null : JsonSerializer.Serialize( effect );
		_applied = _openedAs;

		string text;
		if ( Weapon is not null )
			text = OpenWeapon( look );
		else if ( Draft is { } draft )
		{
			text = draft.Item;
			_name = draft.Name;
			_drafted = text;
			_draftId = string.IsNullOrEmpty( draft.Id ) ? null : draft.Id;
		}
		else if ( effect?.Script is { } script )
		{
			text = Formatter.Format( script ) ?? script;
			_name = effect.Name;
		}
		else if ( effect is not null )
		{
			text = Formatter.Format( ScriptCodegen.Effect( effect, look.DrawnShapes ) ) ?? ScriptCodegen.Effect( effect, look.DrawnShapes );
			_name = effect.Name;
		}
		else
		{
			text = Starter;
			_name = "My effect";
		}

		_entry.Theme = Theme;
		_entry.Load( text );
		_source = _entry.Source;
		_saved = Draft is null ? _source : "";
		Lines();
		Compile( false );
		_entry.Focus();
		_opened = true;
		if ( _startLine > 0 )
			JumpTo( CodeText.OffsetOfLine( _source, _startLine ) );
	}

	// The effect edited, in the look now; null when it isn't worn (yet)
	private Effect EffectOf( PlayerLook look ) => EffectId is null ? null : look?.Effects.Find( x => x.Id == EffectId );

	private void TextEdited( string text )
	{
		_source = text;
		_sinceEdit = 0;
		_pending = true;
		_errorsQuiet = true;
		_note = null;
		Lines();
		Restyle();
		_assistPending = true;
		StateHasChanged();
	}

	private void TickCompile()
	{
		if ( _pending && _sinceEdit >= CompileAfter )
			Compile( _autoApply );

		// An error on the line being typed shows once typing pauses
		if ( _errorsQuiet && _sinceEdit >= ErrorsAfter )
		{
			_errorsQuiet = false;
			Restyle();
		}
	}

	// Compiles the text now: its errors and size, and when it builds, on the effect (apply) for the preview
	private bool Compile( bool apply )
	{
		_pending = false;
		var result = ScriptCompiler.Compile( _source, Kind, Names );
		_storedBytes = result.StoredBytes;
		_errors = result.Errors.ToList();
		if ( result.Succeeded && Kind == ScriptKind.Skin )
		{
			_program = result.Program;
			if ( RunsAsSkin( result.Program ) && apply )
				PutSkin();
		}
		else if ( result.Succeeded )
		{
			_program = result.Program;
			var machine = new ScriptMachine( result.Program );
			var kept = machine.SetParameters( EffectOf( LookNow?.Invoke() )?.Parameters );
			var build = machine.Build( Names, ScriptPrograms.Stack );
			if ( build.Succeeded )
			{
				_build = build;
				if ( apply && Weapon is null )
					Put( build, kept );
				else if ( apply )
					PutDesign( build );
			}
			else
				_errors = new List<ScriptError> { build.Error };
		}

		Restyle();
		StateHasChanged();
		return _errors.Count == 0;
	}

	// The build on the effect (made when it isn't worn yet): its fields, its script minified, the parameters still its
	// own, and the shapes it drew carried by the look
	private void Put( ScriptBuild build, Dictionary<string, string> parameters )
	{
		var look = LookNow?.Invoke();
		if ( look is null )
			return;

		var effect = EffectOf( look );
		if ( effect is null )
		{
			if ( look.Effects.Count >= Effect.MaxWorn )
			{
				_note = $"You wear as many effects as a look can ({Effect.MaxWorn}): remove one to wear this";
				return;
			}

			effect = new Effect { Id = Effect.NewId( look.Effects ), Anchor = new EffectAnchor() };
			look.Effects.Add( effect );
			EffectId = effect.Id;
		}

		ScriptSync.Adopt( effect, build.Effect );
		effect.Script = Minifier.Minify( _source );
		effect.Parameters = parameters.Count > 0 ? parameters : null;
		foreach ( var shape in effect.Shapes ?? new List<DrawnShape>() )
		{
			if ( !look.Carry( shape ) )
				_note = $"A look carries at most {DrawnShape.MaxPerLook} drawn shapes: this one's aren't all kept";
		}

		effect.Shapes = null;
		_name = effect.Name;
		_applied = JsonSerializer.Serialize( effect );
		Changed?.Invoke();
	}

	/// <summary>
	/// Follows a change made elsewhere (the side editor, the sliders, a parameter, undo): a field changed is written into
	/// the script in place, without moving the caret or losing undo; one the script works out (or a layer moved) is put
	/// back, and why is shown. Called by the page after every change to the look.
	/// </summary>
	public void Sync()
	{
		if ( Weapon is not null )
		{
			SyncDesign();
			return;
		}

		var look = LookNow?.Invoke();
		var effect = EffectOf( look );
		if ( effect is null || _entry is null )
			return;

		var now = JsonSerializer.Serialize( effect );
		if ( now == _applied )
			return;

		// Its script changed elsewhere (undo, a draft, a preset put back): that is the text now
		if ( effect.Script is { } script && script != Minifier.Minify( _source ) && _applied is not null
			&& JsonSerializer.Deserialize<Effect>( _applied )?.Script is { } before && before != script )
		{
			_entry.Rewrite( Formatter.Format( script ) ?? script );
			_source = _entry.Source;
			_applied = now;
			Compile( false );
			return;
		}

		var result = _errors.Count == 0 ? ScriptSync.Reconcile( _source, effect, _library, ShapeName ) : new EditResult( _source, "The code has errors: fix them first" );
		if ( result.Succeeded )
		{
			if ( result.Source != _source )
			{
				_entry.Rewrite( result.Source );
				_source = _entry.Source;
			}

			// A parameter's value, or a field written in, is built again (and the fields given by parameters with it)
			_applied = now;
			Compile( true );
			return;
		}

		// Not something the script lets be changed here: put back, saying why (and where)
		if ( _build?.Effect is { } built )
			ScriptSync.Adopt( effect, built );

		_applied = JsonSerializer.Serialize( effect );
		_note = result.Line > 0 ? $"{result.Problem} · line {result.Line}" : result.Problem;
		Changed?.Invoke();
		StateHasChanged();
	}

	// A drawn shape a layer names, as the script names it: one of My shapes (or the look's) by its name
	private string ShapeName( string reference )
	{
		var shape = LookNow?.Invoke()?.DrawnShapeOf( reference?.TrimStart( DrawnShape.ReferencePrefix ) );
		if ( shape?.Name is null )
			return null;

		var kind = LookLibrary.KindOf( shape ) == LibraryItemKind.Sticker ? "Stickers" : "Shapes";
		return $"{kind}.Mine( {ScriptLiteral.Text( shape.Name )} )";
	}

	/// <summary>Puts the caret on the lines making layer <paramref name="layer"/> of the effect (the side editor selected it).</summary>
	public void ShowLayer( int layer )
	{
		if ( _build?.LayerSite( layer ) is not { } site || _program?.Source != _source )
			return;

		_caretLayer = layer;
		_entry.Select( CodeText.LineStart( _source, site.StatementStart ), CodeText.LineEnd( _source, site.StatementEnd ) );
		EnsureVisible( site.StatementStart );
	}

	// The layer the caret is in: the last made at or before it, while the build is of the text now
	private int LayerAt( int caret )
	{
		if ( _build is null || _program?.Source != _source )
			return -1;

		var found = -1;
		for ( var layer = 0; layer < _build.Effect.Layers.Count; layer++ )
		{
			if ( _build.LayerSite( layer ) is { IsTopLevel: true } site && site.StatementStart <= caret )
				found = layer;
		}

		return found;
	}

	/// <summary>Saves: compiles, puts it on the effect and wears it. False, saying why, when it has errors.</summary>
	private bool Save()
	{
		if ( !Compile( true ) )
		{
			_showErrors = true;
			_note = "Fix the errors to save (or keep it as a draft)";
			StateHasChanged();
			return false;
		}

		// Finished: no longer a draft
		if ( _draftId is not null )
		{
			Drafts.Delete( PlayerModels.Core.Draft.Of.Script, _draftId );
			_draftId = null;
		}

		_drafted = null;

		_saved = _source;
		_openedAs = _applied;
		Wear?.Invoke();
		_note = "Saved and worn";
		StateHasChanged();
		return true;
	}

	// Keeps the text as it is, errors and all, as a script draft (the same one again once kept); when drafts are full it
	// asks before dropping the oldest. True once kept
	private bool KeepDraft( bool dropOldest )
	{
		_confirmDrop = null;
		var draft = Drafts.Make( PlayerModels.Core.Draft.Of.Script, _draftId, TitleText, _source, EffectId );
		if ( !Drafts.Save( draft, dropOldest, out var dropped ) )
		{
			if ( Drafts.Find( PlayerModels.Core.Draft.Of.Script, draft.Id ) is null && Drafts.Of( PlayerModels.Core.Draft.Of.Script ).Count >= PlayerModels.Core.Draft.MaxPerKind )
				_confirmDrop = Drafts.Oldest( PlayerModels.Core.Draft.Of.Script );
			else
				_note = "This script is too long to keep as a draft";

			StateHasChanged();
			return false;
		}

		_draftId = draft.Id;
		_drafted = _source;
		_note = dropped is null ? "Kept as a draft: open it again from My effects" : $"Kept as a draft (\"{dropped.Name}\" was dropped)";
		StateHasChanged();
		return true;
	}

	private void DropOldestDraft()
	{
		if ( KeepDraft( true ) && _closeAfterDraft )
		{
			PutBack();
			Delete();
		}

		_closeAfterDraft = false;
	}

	private void CancelDrop()
	{
		_confirmDrop = null;
		_closeAfterDraft = false;
		StateHasChanged();
	}

	/// <summary>Closes it, first asking what to do with changes not saved (Save, Discard, Keep as draft, Cancel).</summary>
	public void RequestClose()
	{
		if ( IsDirty )
			SetClosing( true );
		else
			Delete();
	}

	private void SaveAndClose()
	{
		_closing = false;
		if ( Save() )
			Delete();
	}

	private void DiscardAndClose()
	{
		PutBack();
		Delete();
	}

	private void DraftAndClose()
	{
		_closing = false;
		if ( KeepDraft( false ) )
		{
			PutBack();
			Delete();
			return;
		}

		_closeAfterDraft = _confirmDrop is not null;
	}

	// The effect as it was when opened (or last saved): Auto-apply's changes to the preview are let go
	private void PutBack()
	{
		if ( Weapon is not null )
		{
			PutBackDesign();
			return;
		}

		var look = LookNow?.Invoke();
		var effect = EffectOf( look );
		if ( effect is null )
			return;

		var index = look.Effects.IndexOf( effect );
		if ( _openedAs is null )
			look.Effects.RemoveAt( index );
		else if ( JsonSerializer.Deserialize<Effect>( _openedAs ) is { } opened )
			look.Effects[ index ] = opened;

		Changed?.Invoke();
	}

	/// <summary>Puts the caret on line <paramref name="line"/> (from 1), now or as soon as the script opens.</summary>
	public void GoTo( int line )
	{
		if ( _entry is null || !_opened )
		{
			_startLine = line;
			return;
		}

		JumpTo( CodeText.OffsetOfLine( _source, line ) );
	}

	private int _startLine;
	private bool _opened;

	/// <summary>Puts <paramref name="script"/> (an example copied from the reference) in at the caret, on lines of its own.</summary>
	public void Insert( string script )
	{
		var caret = _entry.Caret;
		var at = CodeText.LineEnd( _source, caret );
		var block = "\n\n" + (script ?? "").Trim( '\n' ) + "\n";
		_entry.Replace( at, at, block );
		_entry.Focus();
	}

	private void FormatSource()
	{
		var formatted = Formatter.Format( _source );
		if ( formatted is null )
		{
			_note = "Fix the errors to format it";
			StateHasChanged();
			return;
		}

		_entry.Rewrite( formatted.TrimEnd( '\n' ) );
	}

	private void ToggleComment()
	{
		var (start, end) = _entry.Selection;
		_entry.Apply( CodeText.ToggleComment( _source, start, end ) );
	}

	private void JumpTo( int offset )
	{
		_entry.MoveCaret( offset );
		_entry.Focus();
		EnsureVisible( offset );
	}

	// Scrolls the text so offset shows
	private void EnsureVisible( int offset )
	{
		var rect = _entry.RectOf( offset );
		var box = _entry.Box.Rect;
		var scroll = _entry.ScrollOffset;
		if ( rect.Top < box.Top )
			scroll.y -= box.Top - rect.Top + rect.Height;
		else if ( rect.Bottom > box.Bottom )
			scroll.y += rect.Bottom - box.Bottom + rect.Height;

		_entry.ScrollOffset = new Vector2( scroll.x, MathF.Max( 0f, scroll.y ) );
	}

	// The line numbers down the side, written again only when the count changes
	private void Lines()
	{
		var count = CodeText.LineCount( _source );
		if ( count == _lineCount && _lineNumbers.Length > 0 )
			return;

		_lineCount = count;
		_lineNumbers = string.Join( "\n", Enumerable.Range( 1, count ) );
	}

	// Colours the text: what each piece is, the errors (not on the line being typed until typing pauses), find matches and
	// the bracket at the caret, and room before colours and keys for their pictures
	private void Restyle()
	{
		if ( _entry is null )
			return;

		_colours = ColorLiterals.Find( _source );
		var caretLine = CodeText.Position( _source, _entry.Caret ).Line;
		var errors = new List<(int, int)>();
		foreach ( var error in _errors )
		{
			if ( !_errorsQuiet || error.Line != caretLine )
				errors.Add( (Math.Min( error.Start, Math.Max( 0, _source.Length - 1 ) ), Math.Max( 1, error.Length )) );
		}

		var marks = new List<(int, int)>();
		foreach ( var match in _matches )
			marks.Add( (match, _findQuery.Length) );

		if ( CodeText.Matching( _source, _entry.Caret ) is { } pair )
		{
			marks.Add( pair.First );
			marks.Add( pair.Second );
		}

		_entry.Decorate( CodeText.Highlight( _source ), errors, marks );
	}
}
