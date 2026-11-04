using System;
using System.Collections.Generic;
using System.Linq;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using PlayerModels.Engine;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

// What helps while writing: the keys the editor takes, completion as you type (members, names, keys with their pictures,
// shapes and stickers with theirs), signature help in a call, docs on hover ("More…" opens the reference), find and
// replace, go to line, the swatch before each colour (click: pick another, written back in the same form) and the
// picture before each key, and where the caret is
public partial class CodeEditor
{
	// How many completions show at once
	private const int CompletionRows = 10;

	// How long the mouse rests on a word before its docs show
	private const float HoverAfter = 0.5f;

	// A swatch's and a key picture's size, and how far before its literal it sits, in the UI's units
	private const float SwatchSize = 11f;
	private const float GapBefore = 14f;

	private List<CompletionItem> _completions = new();
	private int _completionIndex;
	private int _completionTop;
	private int _completionStart;
	private SignatureInfo _signature;
	private HoverInfo _hover;
	private bool _overHover;
	private int _hoverAt = -1;
	private int _hoverShownAt = -1;
	private RealTimeSince _sinceHoverMove;

	private List<ColorLiteral> _colours = new();
	private (ColorLiteral Literal, string Hex)? _picking;
	private readonly List<Panel> _swatches = new();
	private readonly List<KeyGlyph> _keyPictures = new();
	private int _lastCaret = -1;

	private string _findQuery = "";
	private string _replaceText = "";
	private string _goTo = "";
	private bool _matchCase;
	private bool _wholeWord;
	private List<int> _matches = new();
	private int _match = -1;

	private string FindText
	{
		get => _findQuery;
		set
		{
			_findQuery = value ?? "";
			Search();
		}
	}

	private string GoToText
	{
		get => _goTo;
		set => _goTo = value ?? "";
	}

	private string FindCount => _findQuery.Length == 0 ? "" : _matches.Count == 0 ? "No results"
		: $"{_match + 1} of {(_matches.Count >= CodeText.MostMatches ? $"{CodeText.MostMatches}+" : _matches.Count.ToString())}";

	// The keys the editor takes before the text does; true when taken
	private bool Key( string key )
	{
		if ( _completions.Count > 0 )
		{
			switch ( key )
			{
				case "up" or "down":
					MoveCompletion( key == "up" ? -1 : 1 );
					return true;
				case "enter" or "tab" or "pad_enter":
					AcceptCompletion( _completionIndex );
					return true;
				case "escape":
					CloseAssist();
					return true;
			}
		}

		switch ( key )
		{
			case "ctrl+s":
				Save();
				return true;
			case "ctrl+f":
				OpenFind( false );
				return true;
			case "ctrl+h":
				OpenFind( true );
				return true;
			case "ctrl+g":
				OpenGoTo();
				return true;
			case "ctrl+/" or "ctrl+slash" or "ctrl+divide":
				ToggleComment();
				return true;
			case "ctrl+space":
				ShowCompletions( true );
				return true;
			case "alt+shift+f":
				FormatSource();
				return true;
			case "f1":
				OpenReference( _hover?.Title );
				return true;
			case "escape" when _signature is not null || _hover is not null || _picking is not null:
				CloseAssist();
				return true;
			case "escape" when _bar != Bar.None:
				CloseBar();
				return true;
			case "f3" or "shift+f3":
				FindNext( key == "f3" ? 1 : -1 );
				return true;
			default:
				return false;
		}
	}

	// After each edit: completions follow the word being typed, signature help the call
	private void Assist()
	{
		var caret = _entry.Caret;
		var before = caret > 0 ? _source[ caret - 1 ] : ' ';
		if ( char.IsLetterOrDigit( before ) || before is '_' or '.' or '"' )
			ShowCompletions( false );
		else
			_completions = new List<CompletionItem>();

		_signature = ScriptSignature.At( _source, caret, ScriptKind.Effect, _library );
		_hover = null;
	}

	private void ShowCompletions( bool asked )
	{
		var caret = _entry.Caret;
		var items = ScriptCompletion.At( _source, caret, ScriptKind.Effect, _library );
		var start = caret;
		while ( start > 0 && (char.IsLetterOrDigit( _source[ start - 1 ] ) || _source[ start - 1 ] == '_' || _source[ start - 1 ] is ' ' && InMine( start )) )
			start--;

		// Only a word started, or a list asked for, opens it: an exact match alone isn't worth a list
		if ( !asked && (items.Count == 0 || items.Count == 1 && items[ 0 ].Insert == _source[ start..caret ]) )
			items.Clear();

		_completions = items;
		_completionIndex = 0;
		_completionTop = 0;
		_completionStart = start;
		StateHasChanged();
	}

	// Inside the quotes of Mine( "..." ) a name can have spaces
	private bool InMine( int at ) => _source.LastIndexOf( '"', Math.Max( 0, at - 1 ) ) is var quote and >= 0 && CodeText.LineStart( _source, at ) <= quote;

	private void MoveCompletion( int step )
	{
		_completionIndex = Math.Clamp( _completionIndex + step, 0, _completions.Count - 1 );
		if ( _completionIndex < _completionTop )
			_completionTop = _completionIndex;
		else if ( _completionIndex >= _completionTop + CompletionRows )
			_completionTop = _completionIndex - CompletionRows + 1;

		StateHasChanged();
	}

	private void AcceptCompletion( int index )
	{
		if ( index < 0 || index >= _completions.Count )
			return;

		var item = _completions[ index ];
		var caret = _entry.Caret;
		var start = Math.Clamp( _completionStart, 0, caret );
		var inserted = item.Insert ?? item.Label;
		_completions = new List<CompletionItem>();
		_entry.Apply( new TextChange( _source[ ..start ] + inserted + _source[ caret.. ], start + inserted.Length, start + inserted.Length ) );
		_entry.Focus();
		StateHasChanged();
	}

	private void CloseAssist()
	{
		_completions = new List<CompletionItem>();
		_signature = null;
		_hover = null;
		_picking = null;
		StateHasChanged();
	}

	// The mouse over the text: its docs show once it rests on a word
	private void Hover( int offset )
	{
		if ( offset == _hoverAt )
			return;

		_hoverAt = offset;
		_sinceHoverMove = 0;
	}

	private void TickCaret()
	{
		var caret = _entry.Caret;
		if ( caret != _lastCaret )
		{
			_lastCaret = caret;
			var (line, column) = CodeText.Position( _source, caret );
			_position = $"Ln {line}, Col {column}";
			if ( _errorsQuiet )
				_errorsQuiet = _errors.Exists( x => x.Line == line );

			Restyle();
			if ( _completions.Count > 0 && (caret < _completionStart || caret > _source.Length) )
				_completions = new List<CompletionItem>();

			var layer = LayerAt( caret );
			if ( layer >= 0 && layer != _caretLayer )
			{
				_caretLayer = layer;
				CaretInLayer?.Invoke( layer );
			}

			StateHasChanged();
		}

		if ( _hoverAt != _hoverShownAt && _sinceHoverMove >= HoverAfter && !_overHover )
		{
			_hoverShownAt = _hoverAt;
			_hover = _hoverAt < 0 ? null : ScriptHover.At( _source, _hoverAt, ScriptKind.Effect, _library );
			StateHasChanged();
		}
	}

	// The swatches and key pictures before their literals, the line numbers and the popups follow the text as it scrolls.
	// Moved, not drawn again, each frame
	private void TickOverlays()
	{
		if ( _body is null || !_body.IsValid() )
			return;

		var scale = MathF.Max( ScaleToScreen, 0.01f );
		var origin = _body.Box.Rect.Position;
		var visible = _entry.Box.Rect;

		var keys = KeysNow();
		Fit( _swatches, _colours.Count, () => { var swatch = _body.AddChild<Panel>( "swatch" ); swatch.AddEventListener( "onclick", PickAtSwatch ); return swatch; } );
		for ( var i = 0; i < _colours.Count; i++ )
		{
			var literal = _colours[ i ];
			Place( _swatches[ i ], _entry.RectOf( literal.Start ), origin, visible, scale );
			_swatches[ i ].Style.BackgroundColor = new Color( (float)literal.R, (float)literal.G, (float)literal.B, 1f );
			_swatches[ i ].SetClass( "index-" + i, true );
		}

		Fit( _keyPictures, keys.Count, () => _body.AddChild<KeyGlyph>( "key-picture" ) );
		for ( var i = 0; i < keys.Count; i++ )
		{
			_keyPictures[ i ].Key = keys[ i ].Key;
			Place( _keyPictures[ i ], _entry.RectOf( keys[ i ].Start ), origin, visible, scale );
		}

		if ( _numbers is not null && _numbers.IsValid() )
			_numbers.Style.Top = Length.Pixels( -_entry.ScrollOffset.y / scale );

		var caret = _entry.RectOf( _entry.Caret );
		PlacePopup( _completionPanel, caret, origin, scale );
		PlacePopup( _signaturePanel, caret, origin, scale, above: true );
		if ( _hover is { } hover )
			PlacePopup( _hoverPanel, _entry.RectOf( hover.Start ), origin, scale );

		if ( _picking is { } picking )
			PlacePopup( _pickerPanel, _entry.RectOf( picking.Literal.Start ), origin, scale );
	}

	// As many overlay panels as wanted, made or let go of only when the count changes
	private static void Fit<T>( List<T> panels, int count, Func<T> make ) where T : Panel
	{
		// The body draws itself again now and then, and may let go of what was added to it
		panels.RemoveAll( x => !x.IsValid() );
		while ( panels.Count < count )
			panels.Add( make() );

		while ( panels.Count > count )
		{
			panels[ ^1 ].Delete();
			panels.RemoveAt( panels.Count - 1 );
		}
	}

	// A picture just before the literal at rect, hidden while that is scrolled out of view
	private static void Place( Panel panel, Rect rect, Vector2 origin, Rect visible, float scale )
	{
		var shows = rect.Top >= visible.Top && rect.Bottom <= visible.Bottom && rect.Left >= visible.Left && rect.Left <= visible.Right;
		panel.Style.Display = shows ? DisplayMode.Flex : DisplayMode.None;
		if ( !shows )
			return;

		panel.Style.Left = Length.Pixels( (rect.Left - origin.x) / scale - GapBefore );
		panel.Style.Top = Length.Pixels( (rect.Top - origin.y) / scale + (rect.Height / scale - SwatchSize) / 2f );
	}

	// A popup under (or over) the caret, kept within the text
	private void PlacePopup( Panel popup, Rect caret, Vector2 origin, float scale, bool above = false )
	{
		if ( popup is null || !popup.IsValid() )
			return;

		var size = popup.Box.Rect.Size / scale;
		var room = _body.Box.Rect.Size / scale;
		var left = Math.Clamp( (caret.Left - origin.x) / scale, 0f, MathF.Max( 0f, room.x - size.x ) );
		var top = above ? (caret.Top - origin.y) / scale - size.y - 2f : (caret.Bottom - origin.y) / scale + 2f;
		if ( !above && top + size.y > room.y )
			top = (caret.Top - origin.y) / scale - size.y - 2f;

		popup.Style.Left = Length.Pixels( left );
		popup.Style.Top = Length.Pixels( MathF.Max( 0f, top ) );
	}

	// The key literals of the text now: the compiler's, while its program is of this very text
	private IReadOnlyList<KeyLiteral> KeysNow() => _program is not null && _program.Source == _source ? _program.Keys : Array.Empty<KeyLiteral>();

	// A swatch clicked: the picker opens on its colour
	private void PickAtSwatch( PanelEvent e )
	{
		var index = _swatches.IndexOf( e.This );
		if ( index < 0 || index >= _colours.Count )
			return;

		var literal = _colours[ index ];
		_picking = (literal, Hex( literal ));
		StateHasChanged();
	}

	// A colour picked: its literal written again in the same form (hex stays hex), as one step undo takes back
	private void PickColour( string hex )
	{
		if ( _picking is not { } picking )
			return;

		if ( string.IsNullOrEmpty( hex ) || Color.Parse( hex ) is not { } colour )
		{
			_picking = null;
			StateHasChanged();
			return;
		}

		var literal = ColorLiterals.Find( _source ).FirstOrDefault( x => x.Start == picking.Literal.Start );
		if ( literal.Length == 0 )
			return;

		var written = ColorLiterals.Replace( _source, literal, colour.r, colour.g, colour.b, literal.A );
		_entry.Rewrite( written );
		_picking = (ColorLiterals.Find( _entry.Source ).FirstOrDefault( x => x.Start == literal.Start ), hex);
		StateHasChanged();
	}

	private static string Hex( ColorLiteral literal ) => new Color( (float)literal.R, (float)literal.G, (float)literal.B ).Hex[ ..7 ].ToLowerInvariant();

	private void OpenFind( bool replace )
	{
		_bar = replace ? Bar.Replace : Bar.Find;
		var (start, end) = _entry.Selection;
		if ( end > start && end - start < 100 && !_source[ start..end ].Contains( '\n' ) )
			_findQuery = _source[ start..end ];

		Search();
		StateHasChanged();
		_barEntry?.Focus();
	}

	private void OpenGoTo()
	{
		_bar = Bar.GoTo;
		_goTo = "";
		StateHasChanged();
		_barEntry?.Focus();
	}

	private void CloseBar()
	{
		_bar = Bar.None;
		_matches = new List<int>();
		_match = -1;
		Restyle();
		StateHasChanged();
		_entry.Focus();
	}

	private void SetFindOption( bool matchCase, bool wholeWord )
	{
		(_matchCase, _wholeWord) = (matchCase, wholeWord);
		LocalPrefs.CodeFind = (matchCase, wholeWord);
		Search();
	}

	// Every match of what is looked for, the one at or after the caret current
	private void Search()
	{
		_matches = CodeText.Find( _source, _findQuery, _matchCase, _wholeWord );
		var caret = _entry?.Caret ?? 0;
		_match = _matches.Count == 0 ? -1 : Math.Max( 0, _matches.FindIndex( x => x >= caret ) );
		Restyle();
		StateHasChanged();
	}

	private void FindNext( int step )
	{
		if ( _matches.Count == 0 )
			return;

		_match = (_match + step + _matches.Count) % _matches.Count;
		var at = _matches[ _match ];
		_entry.Select( at, at + _findQuery.Length );
		EnsureVisible( at );
		StateHasChanged();
	}

	private void ReplaceOne()
	{
		if ( _match < 0 || _match >= _matches.Count )
			return;

		var at = _matches[ _match ];
		_entry.Replace( at, at + _findQuery.Length, _replaceText );
		_source = _entry.Source;
		Search();
		FindNext( 0 );
	}

	private void ReplaceAll()
	{
		var replaced = CodeText.ReplaceAll( _source, _findQuery, _replaceText, _matchCase, _wholeWord, out var count );
		if ( count == 0 )
			return;

		_entry.Rewrite( replaced );
		_source = _entry.Source;
		_note = count == 1 ? "Replaced 1" : $"Replaced {count}";
		Search();
	}

	private void GoToLine()
	{
		if ( !int.TryParse( _goTo.Trim(), out var line ) )
			return;

		var at = CodeText.OffsetOfLine( _source, Math.Clamp( line, 1, CodeText.LineCount( _source ) ) );
		CloseBar();
		JumpTo( at );
	}

	private void OpenReference( string entry )
	{
		ShowReference?.Invoke( entry );
	}

	// A completion's picture: a stock shape's
	private static StandardShape? StockShapeOf( CompletionItem item ) =>
		item.Icon is { } icon && icon.StartsWith( "shape:" ) && Enum.TryParse<StandardShape>( icon[ 6.. ], out var shape ) ? shape : null;

	// One of the player's own shapes or stickers, drawn as it is
	private DrawnShape DrawnOf( CompletionItem item ) => item.Kind == CompletionKind.Shape && item.Group is not null && item.Group.StartsWith( "My" )
		? _library?.ShapeNamed( item.Label )
		: null;

	// A completion's icon by its kind (Material icons)
	private static string IconOf( CompletionKind kind ) => kind switch
	{
		CompletionKind.Keyword => "key",
		CompletionKind.Variable => "data_object",
		CompletionKind.Function or CompletionKind.Method => "functions",
		CompletionKind.Property => "label",
		CompletionKind.Constant => "pin",
		CompletionKind.Input => "input",
		CompletionKind.Output => "output",
		CompletionKind.Event => "bolt",
		CompletionKind.Namespace or CompletionKind.Type => "category",
		CompletionKind.Choice => "list",
		CompletionKind.Key => "keyboard",
		CompletionKind.Shape => "interests",
		CompletionKind.Effect => "auto_awesome",
		_ => "tag",
	};

	// A signature split into its pieces, the parameter being typed marked
	private static List<(string Text, bool Active)> SignatureParts( SignatureInfo signature )
	{
		var parts = new List<(string, bool)>();
		var text = signature.Signature ?? "";
		var open = text.IndexOf( '(' );
		if ( open < 0 || signature.Parameters is not { Count: > 0 } )
		{
			parts.Add( (text, false) );
			return parts;
		}

		parts.Add( (text[ ..(open + 1) ] + " ", false) );
		for ( var i = 0; i < signature.Parameters.Count; i++ )
			parts.Add( (signature.Parameters[ i ] + (i < signature.Parameters.Count - 1 ? ", " : " "), i == signature.Active) );

		var close = text.LastIndexOf( ')' );
		parts.Add( (close >= 0 ? text[ close.. ] : ")", false) );
		return parts;
	}
}
