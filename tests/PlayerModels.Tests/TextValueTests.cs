using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>
/// Text a player types (an effect's or shape's name, a text parameter) reaches a script only as a string literal, never
/// as code: whatever it holds (a leading dot, quotes, backslashes, line breaks, control characters), the script written
/// with it compiles and gives the text back.
/// </summary>
public class TextValueTests
{
	public static IEnumerable<object[]> Texts()
	{
		foreach ( var text in new[] { ".title", ".Title", "my title", "say \"hi\"", "back\\slash", "a\nb", "tab\there", "cr\rlf", "bell\u0007", "end\\", "\"", "--not a comment", "]] ; local x = 1", "ünïcödé ✓" } )
			yield return new object[] { text };
	}

	// Text as a script keeps it: every character but the control ones it can't write (a line break and a tab it can)
	private static string Written( string text ) => string.Concat( text.Split( '\r', '\u0007' ) );

	[Theory]
	[MemberData( nameof( Texts ) )]
	public void AnEffectsNameIsWrittenAsText( string name )
	{
		var effect = ScriptKit.BuildEffect( "local glow = Sprite()" );
		effect.Name = name;
		var source = ScriptCodegen.Effect( effect );
		Assert.Equal( Written( effect.Sanitized().Name ), ScriptKit.BuildEffect( source ).Name );
	}

	[Theory]
	[MemberData( nameof( Texts ) )]
	public void ANameEditedInIsWrittenAsText( string name )
	{
		const string source = "local glow = Sprite()";
		var (_, build) = ScriptKit.Build( source );
		var effect = ScriptKit.BuildEffect( source );
		effect.Name = name;
		var edited = ScriptEdit.SetEffect( source, build, PropertyId.EffectName, effect );
		Assert.True( edited.Succeeded, edited.Problem );
		Assert.Equal( Written( effect.Sanitized().Name ), ScriptKit.BuildEffect( edited.Source ).Name );
	}

	[Theory]
	[MemberData( nameof( Texts ) )]
	public void ATextParameterSavedAsDefaultIsWrittenAsText( string value )
	{
		const string source = "[Parameter]\nlocal label = \"x\"\nlocal glow = Sprite()";
		var edited = ScriptEdit.SaveAsDefault( source, ScriptKit.Compile( source ), "label", value );
		if ( !edited.Succeeded )
			return;

		var machine = new ScriptMachine( ScriptKit.Compile( edited.Source ) );
		machine.Build();
		Assert.StartsWith( "\"", machine.Variable( "label" ) );
	}
}
