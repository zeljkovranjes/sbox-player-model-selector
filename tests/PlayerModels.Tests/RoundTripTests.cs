using System.Collections.Generic;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>
/// Scripts are stored minified and opened formatted: format(minify(s)) == format(s), minify(format(m)) == m, and the
/// original, formatted and minified texts compile to the same program, with every comment, blank-line paragraph, string
/// literal and colour literal kept.
/// </summary>
public class RoundTripTests
{
	private const string Tricky = """
		-- Header comment

		local s = "-- not a comment" -- a comment; with a semicolon
		local q = 'it\'s; "quoted" \\ \t'
		local r = "a;b" --[[ a block
		comment ]] local c = Color( 1, 0.5, 0 )


		local d = Color.Parse( "#ABCDEF" ) ;; local e = { Color.FromHsv( 120, 1, 1 ), Color.Red, d }
		if c == d then
			-- inside
			s = "x"

			s = "y" -- trailing
			-- before the end
		elseif r ~= q then s = q else
		end
		local f = function( x: number ): number
			return - -x
		end
		""";

	public static IEnumerable<object[]> Scripts()
	{
		yield return new object[] { "tricky", Tricky, ScriptKind.Sticker };
		foreach ( var (name, script) in PresetScripts.All )
			yield return new object[] { name, script, ScriptKind.Effect };

		foreach ( var (name, script) in new[] { ("Rain", PresetScripts.Rain), ("Cycl", PresetScripts.Cycl), ("Cyln", PresetScripts.Cyln), ("Jiggle", PresetScripts.Jiggle) } )
			yield return new object[] { name, script, ScriptKind.Sticker };

		foreach ( var preset in PlayerModels.Core.EffectPresets.All )
			yield return new object[] { "codegen " + preset.Name, ScriptCodegen.Effect( preset ), ScriptKind.Effect };
	}

	[Theory]
	[MemberData( nameof( Scripts ) )]
	public void MinifyAndFormatGiveEachOtherBack( string name, string source, ScriptKind kind )
	{
		var formatted = Formatter.Format( source );
		var minified = Minifier.Minify( source );
		Assert.NotNull( formatted );
		Assert.Equal( formatted, Formatter.Format( minified ) );
		Assert.Equal( minified, Minifier.Minify( formatted ) );
		Assert.Equal( minified, Minifier.Minify( minified ) );
		Assert.Equal( formatted, Formatter.Format( formatted ) );
		Assert.True( minified.Length <= source.Length, name );
	}

	[Theory]
	[MemberData( nameof( Scripts ) )]
	public void AllThreeCompileToTheSameProgram( string name, string source, ScriptKind kind )
	{
		var programs = new[] { source, Formatter.Format( source ), Minifier.Minify( source ) };
		var first = ScriptKit.Compile( programs[ 0 ], kind, ExampleRunner.Library );
		foreach ( var text in programs )
		{
			var program = ScriptKit.Compile( text, kind, ExampleRunner.Library );
			Assert.Equal( first.Code, program.Code );
			Assert.Equal( first.Constants, program.Constants );
			Assert.Equal( first.Strings, program.Strings );
			Assert.Equal( first.Arena, program.Arena );
		}

		Assert.NotNull( name );
	}

	[Theory]
	[MemberData( nameof( Scripts ) )]
	public void CommentsStringsAndColoursSurvive( string name, string source, ScriptKind kind )
	{
		foreach ( var text in new[] { Formatter.Format( source ), Minifier.Minify( source ) } )
		{
			Assert.Equal( Pieces( source, TokenKind.Comment ), Pieces( text, TokenKind.Comment ) );
			Assert.Equal( Pieces( source, TokenKind.String ), Pieces( text, TokenKind.String ) );
			Assert.Equal( Colours( source ), Colours( text ) );
		}

		Assert.NotNull( name + kind );
	}

	[Fact]
	public void BlankLinesBecomeSemicolonsAndBack()
	{
		Assert.Equal( "local a=1;local b=2\nlocal c=3", Minifier.Minify( "local a = 1\n\n\nlocal b = 2\nlocal c = 3" ) );
		Assert.Equal( "local a = 1\n\nlocal b = 2\nlocal c = 3\n", Formatter.Format( "local a=1;local b=2\nlocal c=3" ) );
	}

	[Fact]
	public void ATextThatDoesntParseIsKeptTrimmed() => Assert.Equal( "local a = (\nb", Minifier.Minify( "  local a = (  \n\t\tb  " ) );

	private static List<string> Pieces( string source, TokenKind kind )
	{
		var pieces = new List<string>();
		foreach ( var token in Lexer.All( source, new LineMap( source ), out _ ) )
		{
			if ( token.Kind == kind )
				pieces.Add( source.Substring( token.Start, token.Length ) );
		}

		return pieces;
	}

	private static List<(double, double, double, double, ColorForm)> Colours( string source )
	{
		var colours = new List<(double, double, double, double, ColorForm)>();
		foreach ( var literal in ColorLiterals.Find( source ) )
			colours.Add( (literal.R, literal.G, literal.B, literal.A, literal.Form) );

		return colours;
	}
}
