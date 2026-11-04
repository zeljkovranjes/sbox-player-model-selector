using System.Linq;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>
/// The code editor's text work: every edit a key makes (Enter, Tab, brackets and quotes, Backspace, Ctrl+/), find and
/// replace, words, matching brackets and blocks, lines and columns, and what each piece is coloured as.
/// </summary>
public class CodeTextTests
{
	[Fact]
	public void EnterKeepsTheIndentAndStepsInAfterABlockOpens()
	{
		var text = "if grounded then";
		var change = CodeText.NewLine( text, text.Length, text.Length );
		Assert.Equal( "if grounded then\n\t", change.Text );
		Assert.Equal( change.Text.Length, change.SelectionStart );

		text = "\tlocal x = 1";
		Assert.Equal( "\tlocal x = 1\n\t", CodeText.NewLine( text, text.Length, text.Length ).Text );

		text = "function glow.Particle( p )";
		Assert.EndsWith( "\n\t", CodeText.NewLine( text, text.Length, text.Length ).Text );
	}

	[Fact]
	public void EnterBetweenBracketsPutsTheCloseOnItsOwnLine()
	{
		var change = CodeText.NewLine( "local list = {}", 14, 14 );
		Assert.Equal( "local list = {\n\t\n}", change.Text );
		Assert.Equal( 16, change.SelectionStart );
	}

	[Theory]
	[InlineData( '(', "f()", 2 )]
	[InlineData( '[', "f[]", 2 )]
	[InlineData( '{', "f{}", 2 )]
	public void OpeningBracketsAndQuotesCloseThemselves( char typed, string expected, int caret )
	{
		var change = CodeText.Type( "f", 1, 1, typed );
		Assert.Equal( expected, change.Text );
		Assert.Equal( caret, change.SelectionStart );
	}

	[Fact]
	public void AQuoteClosesItselfWhereAStringStarts()
	{
		var change = CodeText.Type( "Color.Parse( ", 13, 13, '"' );
		Assert.Equal( "Color.Parse( \"\"", change.Text );
		Assert.Equal( 14, change.SelectionStart );
	}

	[Fact]
	public void TypingTheCloseWhereItIsStepsOverIt()
	{
		var change = CodeText.Type( "f()", 2, 2, ')' );
		Assert.Equal( "f()", change.Text );
		Assert.Equal( 3, change.SelectionStart );
	}

	[Fact]
	public void ABracketRoundASelectionWrapsIt()
	{
		var change = CodeText.Type( "a + b", 0, 5, '(' );
		Assert.Equal( "(a + b)", change.Text );
		Assert.Equal( (1, 6), (change.SelectionStart, change.SelectionEnd) );
	}

	[Fact]
	public void AQuoteAfterAWordIsJustAQuote()
	{
		Assert.Equal( "it'", CodeText.Type( "it", 2, 2, '\'' ).Text );
	}

	[Fact]
	public void BackspaceBetweenAnEmptyPairTakesBoth()
	{
		Assert.Equal( "f", CodeText.DeletePair( "f()", 2, 2 )?.Text );
		Assert.Null( CodeText.DeletePair( "f(x)", 3, 3 ) );
	}

	[Fact]
	public void EndStepsBackOutToItsBlocksIndent()
	{
		var text = "if x then\n\tlocal y = 1\n\ten";
		var change = CodeText.Type( text, text.Length, text.Length, 'd' );
		Assert.Equal( "if x then\n\tlocal y = 1\nend", change.Text );

		text = "function OnUpdate( dt )\n\tif x then\n\t\ty = 1\n\t\tels";
		Assert.Equal( "function OnUpdate( dt )\n\tif x then\n\t\ty = 1\n\telse", CodeText.Type( text, text.Length, text.Length, 'e' ).Text );
	}

	[Fact]
	public void TabAndShiftTabIndentEveryLineSelected()
	{
		var text = "a = 1\nb = 2\nc = 3";
		var indented = CodeText.IndentLines( text, 0, 9, false );
		Assert.Equal( "\ta = 1\n\tb = 2\nc = 3", indented.Text );
		var outdented = CodeText.IndentLines( indented.Text, 0, indented.SelectionEnd, true );
		Assert.Equal( text, outdented.Text );
	}

	[Fact]
	public void ToggleCommentCommentsLinesAndBack()
	{
		var text = "\tlocal a = 1\n\tlocal b = 2";
		var commented = CodeText.ToggleComment( text, 0, text.Length );
		Assert.Equal( "\t-- local a = 1\n\t-- local b = 2", commented.Text );
		Assert.Equal( text, CodeText.ToggleComment( commented.Text, 0, commented.Text.Length ).Text );
	}

	[Fact]
	public void FindHonoursCaseAndWholeWords()
	{
		const string text = "glow glowSize Glow glow";
		Assert.Equal( new[] { 0, 5, 14, 19 }, CodeText.Find( text, "glow", false, false ) );
		Assert.Equal( new[] { 0, 5, 19 }, CodeText.Find( text, "glow", true, false ) );
		Assert.Equal( new[] { 0, 19 }, CodeText.Find( text, "glow", true, true ) );
		Assert.Empty( CodeText.Find( text, "nothing", false, false ) );
		Assert.Empty( CodeText.Find( text, "", false, false ) );
	}

	[Fact]
	public void FindWorksAcrossVeryLongLinesAndStopsCounting()
	{
		var text = string.Concat( Enumerable.Repeat( "ab ", 5000 ) );
		Assert.Equal( CodeText.MostMatches, CodeText.Find( text, "ab", true, true ).Count );
	}

	[Fact]
	public void ReplaceAllReplacesEveryMatchAndCounts()
	{
		var replaced = CodeText.ReplaceAll( "a.Size = a.Size + 1", "a", "glow", true, true, out var count );
		Assert.Equal( "glow.Size = glow.Size + 1", replaced );
		Assert.Equal( 2, count );
		Assert.Equal( "x", CodeText.ReplaceAll( "x", "y", "z", true, false, out count ) );
		Assert.Equal( 0, count );
	}

	[Fact]
	public void AWordIsLettersDigitsAndUnderscores()
	{
		Assert.Equal( (6, 16), CodeText.WordAt( "local glow_size2 = 1", 9 ) );
	}

	[Fact]
	public void BracketsAndBlocksFindTheirPartners()
	{
		const string text = "function f( a )\n\tif a then\n\t\treturn ( a )\n\tend\nend";
		var parenthesis = CodeText.Matching( text, 10 );
		Assert.Equal( (10, 1), parenthesis?.First );
		Assert.Equal( (14, 1), parenthesis?.Second );

		var function = CodeText.Matching( text, 0 );
		Assert.Equal( text.LastIndexOf( "end" ), function?.Second.Start );

		var innerEnd = text.IndexOf( "end" );
		Assert.Equal( text.IndexOf( "if" ), CodeText.Matching( text, innerEnd )?.Second.Start );
	}

	[Fact]
	public void ALoopsDoBelongsToItsHeader()
	{
		const string text = "for k = 0, 3 do\n\tx = k\nend";
		Assert.Equal( text.IndexOf( "end" ), CodeText.Matching( text, 0 )?.Second.Start );
		Assert.Equal( 0, CodeText.Matching( text, text.IndexOf( "end" ) )?.Second.Start );
	}

	[Fact]
	public void LinesAndColumnsCountFromOne()
	{
		const string text = "a\nbc\nd";
		Assert.Equal( (2, 2), CodeText.Position( text, 3 ) );
		Assert.Equal( 3, CodeText.LineCount( text ) );
		Assert.Equal( 2, CodeText.OffsetOfLine( text, 2 ) );
		Assert.Equal( 5, CodeText.OffsetOfLine( text, 99 ) );
	}

	[Fact]
	public void PiecesAreColouredByWhatTheyAre()
	{
		const string text = "local glow = Sprite() -- hi\nglow.Size = Math.Sin( t ) + 2\nif x then end";
		var spans = CodeText.Highlight( text );
		CodeClass At( string piece ) => spans.First( x => text.Substring( x.Start, x.Length ) == piece ).Class;
		Assert.Equal( CodeClass.Keyword, At( "local" ) );
		Assert.Equal( CodeClass.Function, At( "Sprite" ) );
		Assert.Equal( CodeClass.Comment, At( "-- hi" ) );
		Assert.Equal( CodeClass.Type, At( "Math" ) );
		Assert.Equal( CodeClass.Function, At( "Sin" ) );
		Assert.Equal( CodeClass.Number, At( "2" ) );
		Assert.Equal( CodeClass.Control, At( "then" ) );
	}

	[Fact]
	public void ColouringGoesOnPastAStringLeftOpen()
	{
		const string text = "local a = \"open\nlocal b = 2";
		var spans = CodeText.Highlight( text );
		Assert.Contains( spans, x => x.Start == text.LastIndexOf( "local" ) && x.Class == CodeClass.Keyword );
	}

	[Fact]
	public void MarkupInTheTextIsJustText()
	{
		const string text = "local s = \"<b>bold</b> &amp; #title\"";
		var spans = CodeText.Highlight( text );
		Assert.Contains( spans, x => x.Class == CodeClass.String && text.Substring( x.Start, x.Length ).Contains( "<b>" ) );
	}

	[Fact]
	public void DraftsKeepTheNewestWithinTheCap()
	{
		var drafts = Enumerable.Range( 0, ScriptDraft.MaxDrafts ).Select( x => new ScriptDraft { Name = "d" + x, Script = "x", Edited = x } ).ToList();
		var kept = ScriptDraft.Keep( drafts, new ScriptDraft { Name = "new", Script = "local x = (", Edited = 1000 }, out var dropped );
		Assert.Equal( ScriptDraft.MaxDrafts, kept.Count );
		Assert.Equal( "new", kept[ 0 ].Name );
		Assert.Equal( "local x = (", kept[ 0 ].Script );
		Assert.Equal( "d0", dropped );

		var again = ScriptDraft.Keep( kept, new ScriptDraft { Name = "new", Script = "local x = 1", Edited = 2000 }, out dropped );
		Assert.Null( dropped );
		Assert.Single( again, x => x.Name == "new" );
	}

	[Fact]
	public void ADraftTooLongOrNamelessIsNotKept()
	{
		Assert.Null( new ScriptDraft { Name = "", Script = "x" }.Sanitized() );
		Assert.Null( new ScriptDraft { Name = "big", Script = new string( 'x', ScriptDraft.MaxLength + 1 ) }.Sanitized() );
		Assert.Equal( "edited 5 min ago", ScriptDraft.Age( 0, 300 ) );
	}
}
