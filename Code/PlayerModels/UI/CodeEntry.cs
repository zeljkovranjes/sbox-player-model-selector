using System;
using System.Collections.Generic;
using System.Globalization;
using PlayerModels.Core.Script;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// The code editor's text: a multi-line text entry (so selecting, copying, pasting, the caret and undo are the engine's
/// own) that edits the way a code editor does (see <see cref="CodeText"/>): Enter keeps the indent (one more after a line
/// opening a block), brackets and quotes close themselves, Tab and Shift+Tab indent the selected lines, Backspace between
/// an empty pair takes both, and typing <c>end</c> steps it back out. Colours what it shows by kind
/// (<see cref="CodeText.Highlight"/>), underlines errors, marks find matches and the bracket or block at the caret, and
/// leaves room before colour and key literals for their swatches and key pictures. The text is only ever shown as text:
/// never markup, never translated. Other keys (Ctrl+S, Ctrl+F, ...) go to <see cref="Shortcut"/>.
/// </summary>
public sealed class CodeEntry : TextEntry
{
	/// <summary>The theme it colours text with.</summary>
	[Parameter]
	public CodeTheme Theme
	{
		get => _theme;
		set
		{
			_theme = value ?? CodeTheme.Dark;
			SelectionColor = _theme.Selection;
		}
	}

	private CodeTheme _theme = CodeTheme.Dark;

	/// <summary>Called after every change to the text, with the text.</summary>
	[Parameter] public Action<string> Edited { get; set; }

	/// <summary>
	/// Gets keys the editor handles itself, by name with modifiers ("ctrl+s", "ctrl+space", "escape", "up" while a list is
	/// open...); true when it took the key.
	/// </summary>
	[Parameter] public Func<string, bool> Shortcut { get; set; }

	/// <summary>Called as the mouse moves over the text, with the place under it (-1 off the text).</summary>
	[Parameter] public Action<int> Hovered { get; set; }

	// The text's elements (what the label counts in) and where each starts in the string, worked out once per text
	private string _measured;
	private int[] _elements = Array.Empty<int>();

	// The styles each look of a piece of text takes, made once per theme
	private readonly Dictionary<int, Styles> _styles = new();
	private CodeTheme _stylesFor;

	public CodeEntry()
	{
		AddClass( "code-entry" );
		Multiline = true;
		Label.Tokenize = false;
		Label.IsRich = false;
		Label.Style.WhiteSpace = WhiteSpace.Pre;
	}

	/// <summary>The text, its line breaks as <c>\n</c>.</summary>
	public string Source => Text ?? "";

	/// <summary>Where the caret is in <see cref="Source"/>.</summary>
	public int Caret => OffsetOf( CaretPosition );

	/// <summary>The selection in <see cref="Source"/>, first to last (both the caret without one).</summary>
	public (int Start, int End) Selection
	{
		get
		{
			var (a, b) = (Label.SelectionStart, Label.SelectionEnd);
			if ( a == b )
				return (Caret, Caret);

			var (start, end) = (OffsetOf( Math.Min( a, b ) ), OffsetOf( Math.Max( a, b ) ));
			return (start, end);
		}
	}

	/// <summary>Sets the whole text, starting the undo over (a script opened). Not undoable.</summary>
	public void Load( string text )
	{
		Text = Normalized( text );
		Label.Style.WhiteSpace = WhiteSpace.Pre;
		ClearUndoHistory();
		CaretPosition = 0;
	}

	/// <summary>Selects <paramref name="start"/> to <paramref name="end"/> of <see cref="Source"/>, the caret at the end.</summary>
	public void Select( int start, int end )
	{
		var (a, b) = (ElementOf( start ), ElementOf( end ));
		Label.SetSelection( a, b );
		CaretPosition = b;
	}

	/// <summary>Puts the caret at <paramref name="offset"/>, with nothing selected.</summary>
	public void MoveCaret( int offset )
	{
		var element = ElementOf( offset );
		Label.SetSelection( element, element );
		CaretPosition = element;
	}

	/// <summary>Replaces <paramref name="start"/> to <paramref name="end"/> with <paramref name="text"/> as one step undo takes back.</summary>
	public void Replace( int start, int end, string text )
	{
		// The caret too: with nothing selected, pasting puts the text at the caret, not where the selection was set
		Select( start, end );
		base.OnPaste( Normalized( text ) );
	}

	/// <summary>
	/// Makes the text <paramref name="change"/>'s, changing only what differs (so undo takes back just that, and the caret
	/// and scroll elsewhere stay), then selects as it says.
	/// </summary>
	public void Apply( TextChange change )
	{
		var now = Source;
		var wanted = change.Text ?? "";
		if ( now != wanted )
		{
			var prefix = 0;
			var most = Math.Min( now.Length, wanted.Length );
			while ( prefix < most && now[ prefix ] == wanted[ prefix ] )
				prefix++;

			var suffix = 0;
			while ( suffix < most - prefix && now[ now.Length - 1 - suffix ] == wanted[ wanted.Length - 1 - suffix ] )
				suffix++;

			Replace( prefix, now.Length - suffix, wanted[ prefix..(wanted.Length - suffix) ] );
		}

		Select( change.SelectionStart, change.SelectionEnd );
	}

	/// <summary>
	/// Makes the text <paramref name="text"/> as one undoable step (a change from elsewhere, like the side editor's),
	/// keeping the caret where it was in the text around the change.
	/// </summary>
	public void Rewrite( string text )
	{
		var caret = Caret;
		var now = Source;
		text = Normalized( text );
		if ( now == text )
			return;

		var prefix = 0;
		while ( prefix < Math.Min( now.Length, text.Length ) && now[ prefix ] == text[ prefix ] )
			prefix++;

		Apply( new TextChange( text, 0, 0 ) );
		var moved = caret <= prefix ? caret : Math.Clamp( caret + text.Length - now.Length, 0, text.Length );
		MoveCaret( moved );
	}

	/// <summary>
	/// Where <paramref name="offset"/> of <see cref="Source"/> is drawn now, in screen pixels (a caret's rect there); an
	/// empty rect before the text is first laid out, when there is nowhere yet.
	/// </summary>
	public Rect RectOf( int offset ) => Label.Box.Rect.Width > 0f ? Label.GetCaretRect( ElementOf( offset ) ) : default;

	/// <summary>
	/// Colours the text: <paramref name="spans"/> by their kind, <paramref name="errors"/> underlined, <paramref name="marks"/>
	/// on a background (find matches, the matching bracket). Pieces may overlap: each character takes all that cover it.
	/// </summary>
	public void Decorate( List<CodeSpan> spans, List<(int Start, int Length)> errors, List<(int Start, int Length)> marks )
	{
		var text = Source;
		var keys = new int[ text.Length ];
		foreach ( var span in spans ?? new List<CodeSpan>() )
			Fill( keys, span.Start, span.Length, (int)span.Class, 0xF );

		foreach ( var (start, length) in errors ?? new List<(int, int)>() )
			Fill( keys, start, Math.Max( 1, length ), 0x10, 0 );

		foreach ( var (start, length) in marks ?? new List<(int, int)>() )
			Fill( keys, start, length, 0x20, 0 );

		Label.ClearStyleSpans();
		if ( _stylesFor != Theme )
		{
			_styles.Clear();
			_stylesFor = Theme;
		}

		var run = 0;
		for ( var i = 1; i <= keys.Length; i++ )
		{
			if ( i < keys.Length && keys[ i ] == keys[ run ] )
				continue;

			if ( keys[ run ] != 0 )
				Label.SetStyleSpan( run, i, StyleOf( keys[ run ] ) );

			run = i;
		}
	}

	private static void Fill( int[] keys, int start, int length, int value, int mask )
	{
		var end = Math.Min( keys.Length, start + length );
		for ( var i = Math.Max( 0, start ); i < end; i++ )
			keys[ i ] = mask == 0 ? keys[ i ] | value : (keys[ i ] & ~mask) | value;
	}

	// One look: a kind's colour, underlined for an error, on a background for a mark
	private Styles StyleOf( int key )
	{
		if ( _styles.TryGetValue( key, out var style ) )
			return style;

		style = new Styles { FontColor = Theme.ColorOf( (CodeClass)(key & 0xF) ) };
		if ( (key & 0x10) != 0 )
		{
			style.TextDecorationLine = TextDecoration.Underline;
			style.TextDecorationColor = Theme.Error;
		}

		if ( (key & 0x20) != 0 )
			style.BackgroundColor = Theme.Match;

		_styles[ key ] = style;
		return style;
	}

	public override void OnValueChanged()
	{
		base.OnValueChanged();
		Edited?.Invoke( Source );
	}

	public override void OnPaste( string text )
	{
		base.OnPaste( Normalized( text ) );
	}

	// Ctrl+Space opens completion (see Shortcut): it types no space
	public override void OnKeyTyped( char k, KeyboardModifiers modifiers )
	{
		if ( k == ' ' && modifiers.Contains( KeyboardModifiers.Ctrl ) && !modifiers.Contains( KeyboardModifiers.Alt ) )
			return;

		base.OnKeyTyped( k, modifiers );
	}

	public override void OnKeyTyped( char k )
	{
		if ( !CanEdit )
			return;

		var (start, end) = Selection;
		switch ( k )
		{
			case '\n' or '\r':
				Apply( CodeText.NewLine( Source, start, end ) );
				return;
			case '\t':
				Apply( CodeText.IndentLines( Source, start, end, false ) );
				return;
			case '(' or '[' or '{' or '"' or '\'' or ')' or ']' or '}' or 'd' or 'e' or 'f':
			{
				var change = CodeText.Type( Source, start, end, k );
				if ( change.Text != Source[ ..start ] + k + Source[ end.. ] || change.SelectionStart != start + 1 )
				{
					Apply( change );
					return;
				}

				break;
			}
		}

		base.OnKeyTyped( k );
	}

	public override void OnButtonTyped( ButtonEvent e )
	{
		var name = (e.HasCtrl ? "ctrl+" : "") + (e.HasAlt ? "alt+" : "") + (e.HasShift ? "shift+" : "") + e.Button;
		if ( Shortcut?.Invoke( name ) == true )
		{
			e.StopPropagation = true;
			return;
		}

		var (start, end) = Selection;
		switch ( e.Button )
		{
			case "tab" when !e.HasCtrl && CanEdit:
				e.StopPropagation = true;
				if ( start == end && !e.HasShift )
					Replace( start, end, CodeText.Indent );
				else
					Apply( CodeText.IndentLines( Source, start, end, e.HasShift ) );

				return;

			case "backspace" when !e.HasCtrl && CanEdit && CodeText.DeletePair( Source, start, end ) is { } pair:
				e.StopPropagation = true;
				Apply( pair );
				return;
		}

		base.OnButtonTyped( e );
	}

	protected override void OnMouseMove( MousePanelEvent e )
	{
		base.OnMouseMove( e );
		var element = Label.GetCharacterAtScreenPosition( PanelPositionToScreenPosition( MousePosition ) );
		Hovered?.Invoke( element < 0 ? -1 : OffsetOf( element ) );
	}

	protected override void OnMouseOut( MousePanelEvent e )
	{
		base.OnMouseOut( e );
		Hovered?.Invoke( -1 );
	}

	protected override void OnDoubleClick( MousePanelEvent e )
	{
		var element = Label.GetCharacterAtScreenPosition( PanelPositionToScreenPosition( MousePosition ) );
		if ( element < 0 )
		{
			base.OnDoubleClick( e );
			return;
		}

		var (start, end) = CodeText.WordAt( Source, OffsetOf( element ) );
		if ( end > start )
			Select( start, end );

		e.StopPropagation();
	}

	/// <summary>The place in <see cref="Source"/> of element <paramref name="element"/> of the text.</summary>
	public int OffsetOf( int element )
	{
		Measure();
		return element <= 0 ? 0 : element >= _elements.Length ? Source.Length : _elements[ element ];
	}

	/// <summary>The element of the text at place <paramref name="offset"/> of <see cref="Source"/>.</summary>
	public int ElementOf( int offset )
	{
		Measure();
		if ( offset <= 0 )
			return 0;

		if ( offset >= Source.Length )
			return _elements.Length;

		var index = Array.BinarySearch( _elements, offset );
		return index >= 0 ? index : Math.Max( 0, ~index - 1 );
	}

	// Most text is one string unit per element: worked out only when it isn't
	private void Measure()
	{
		var text = Source;
		if ( ReferenceEquals( text, _measured ) )
			return;

		_measured = text;
		_elements = StringInfo.ParseCombiningCharacters( text );
	}

	private static string Normalized( string text ) => (text ?? "").Replace( "\r\n", "\n" ).Replace( '\r', '\n' );
}
