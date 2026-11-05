using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>Type and name errors are caught when compiling, in plain words, at their line and column.</summary>
public class TypeErrorTests
{
	[Theory]
	[InlineData( "local x = 1 + true", "1:13: '+' can't be used on a number and a bool" )]
	[InlineData( "local x: number = Color.Red", "1:19: Cannot use a Color as a number" )]
	[InlineData( "local x = 1\nx = \"a\"", "2:5: Cannot use a string as a number" )]
	[InlineData( "if 1 then end", "1:4: A condition must be a bool (true or false), not a number: compare it, like n > 0" )]
	[InlineData( "local x = not 3", "1:15: 'not' needs a bool, not a number" )]
	[InlineData( "local x = -true", "1:11: '-' can't be used on a bool" )]
	[InlineData( "local x = Vector3.Up * Vector3.Up", "1:22: '*' can't be used on a Vector3 and a Vector3: use Vector3.Dot or Vector3.Cross" )]
	[InlineData( "local x = 1 == \"a\"", "1:13: a number can't be compared with a string" )]
	[InlineData( "local x = { 1, \"a\" }", "1:16: Every item of a list has one type: this is a string, the list holds numbers" )]
	[InlineData( "local x = {}", "1:11: An empty list needs a type: local items: {number} = {}" )]
	[InlineData( "local x = 3[ 0 ]", "1:11: Only a list can be indexed with [ ], not a number" )]
	[InlineData( "local x = { 1 }[ true ]", "1:18: A list is indexed by a number (from 0), not a bool" )]
	[InlineData( "local x = y", "1:11: 'y' isn't declared" )]
	[InlineData( "local x = Math.Flor( 1 )", "1:11: 'Math.Flor' doesn't exist: did you mean 'Floor'?" )]
	[InlineData( "local x = Math.Floor( 1, 2 )", "1:11: Math.Floor takes 1 argument, but 2 were given: Math.Floor( x: number ): number" )]
	[InlineData( "local x = Math.Floor( Color.Red )", "1:23: Argument 1 ('x') of Math.Floor should be a number, not a Color" )]
	[InlineData( "local x = Math.Floor", "1:11: Math.Floor is a function: call it, like Math.Floor( x: number ): number" )]
	[InlineData( "local x = Math", "1:11: 'Math' is a namespace: use one of its members, like Math.PI" )]
	[InlineData( "local x = Color.Parse( \"#zz\" )", "1:24: \"#zz\" isn't a colour: write it like \"#ff8800\" (or \"#f80\", or \"#ff880080\" with alpha)" )]
	[InlineData( "local c = Color.Red\nlocal x = c.Q", "2:13: a Color has no 'Q'" )]
	[InlineData( "Math.PI = 3", "1:1: Math.PI can't be changed" )]
	[InlineData( "time = 3", "1:1: 'time' can't be changed" )]
	[InlineData( "local f = function( x ) end", "1:21: Give 'x' a type, like x: number" )]
	[InlineData( "local function f( x: number ): number\n\treturn true\nend", "2:9: Cannot use a bool as a number" )]
	[InlineData( "local function f( n: number )\n\treturn f( n )\nend", "2:9: 'f' calls itself, so give it a return type: function f( ... ): number" )]
	[InlineData( "local function f( x: number ): number\n\treturn x\nend\nlocal y = f( 1, 2 )", "4:11: 'f' takes 1 argument, but 2 were given" )]
	[InlineData( "local x: Colour = Color.Red", "1:10: There is no type 'Colour': did you mean 'Color'?" )]
	[InlineData( "local x", "1:1: Give 'x' a value or a type: local x = 0, or local x: number" )]
	[InlineData( "break", "1:1: 'break' is only for leaving a loop" )]
	[InlineData( "return 3", "1:8: The top of a script can't give back a value: just return" )]
	[InlineData( "local a = { 1 }\na[ 0 ] = \"x\"", "2:10: Cannot use a string as a number" )]
	[InlineData( "local a = 1\na[ 0 ] = 2", "2:1: Only a list's items can be assigned to with [ ], not a number's" )]
	[InlineData( "local a = { 1 }\na[ true ] = 2", "2:4: A list is indexed by a number (from 0), not a bool" )]
	[InlineData( "local a = List.Create( 2, 0 )", "1:11: 'List.Create' can only be used where a shape or effect is built (at the top of its script)" )]
	[InlineData( "local x = nil", "1:11: There is no nil: every value has a type and starts with a value" )]
	[InlineData( "local x = 2 ^ 3", "1:13: There is no ^ operator: use Math.Pow( x, y )" )]
	[InlineData( "local s = \"a\" .. \"b\"", "1:15: Joining text with .. isn't supported: text never changes while a script runs" )]
	[InlineData( "local x = #{ 1 }", "1:11: There is no # operator: use list.Count" )]
	[InlineData( "if a != b then end", "1:6: Write 'not equal' as ~=" )]
	[InlineData( "if true then", "1:13: Expected 'end' to close the 'if' on line 1, found the end of the script" )]
	[InlineData( "x.y:z()", "1:4: Use '.' rather than ':' (methods are called like C#: layer.Emit( 10 ))" )]
	[InlineData( "for k, v in list do end", "1:6: Only counting loops: for i = 0, list.Count - 1 do ... end" )]
	[InlineData( "local x = 1\nx", "2:1: This does nothing on its own: a statement assigns (x = 1) or calls (f())" )]
	[InlineData( "local x = \"abc", "1:11: This text is never closed: end it with \"" )]
	[InlineData( "local x = 1e", "1:11: This number is malformed" )]
	[InlineData( "local x = 12abc", "1:11: This number is malformed" )]
	[InlineData( "local x = 'a\\q'", "1:13: Unknown escape: use \\n, \\t, \\\\, \\\" or \\'" )]
	[InlineData( "local x = 1 @ 2", "1:13: '@' isn't part of the language" )]
	public void ReportsAtLineAndColumn( string source, string expected ) => Assert.Equal( expected, ScriptKit.Error( source, ScriptKind.Sticker ) );

	[Theory]
	[InlineData( "local glow = Sprite()\nlocal x = time", "2:11: 'time' changes while the effect runs: use it inside a function, like function OnUpdate( dt ) ... end" )]
	[InlineData( "function OnUpdate( dt )\n\tlocal glow = Sprite()\nend", "2:15: 'Sprite' can only be used while the effect is built (at the top of the script), not while it runs" )]
	[InlineData( "local glow = Sprite()\nfunction OnUpdate( dt )\n\tglow.LooksLike = Picture.Flame\nend", "3:2: Layer.LooksLike can only be set while the effect is built (at the top of the script); while it runs, change things like Color, Size, Amount or Off" )]
	[InlineData( "local glow = Sprite()\nlocal x = i", "2:11: 'i' is for sticker and weapon skin scripts: in a particle function use p.Index" )]
	[InlineData( "local glow = Sprite()\nfunction OnLnad()\nend", "2:10: There is no event 'OnLnad': did you mean 'OnLand'?" )]
	[InlineData( "local glow = Sprite()\nfunction OnLand( a, b )\nend", "2:21: OnLand takes at most 1 parameter: function OnLand( fallSpeed: number )" )]
	[InlineData( "local glow = Sprite()\nfunction OnJump()\nend\nfunction OnJump()\nend", "4:10: OnJump is already handled earlier in the script" )]
	[InlineData( "local glow = Sprite()\nOnJump()", "2:1: OnJump is called by the game, not by the script: put shared code in a function of your own" )]
	[InlineData( "local glow = Sprite()\nglow.Size = Color.Red", "2:13: Cannot use a Color as a number" )]
	[InlineData( "local glow = Sprite()\nglow.Kind = Kind.Light", "2:1: Layer.Kind is read-only" )]
	[InlineData( "local glow = Sprite()\nglow.Sise = 2", "2:6: a Layer has no 'Sise': did you mean 'Size'?" )]
	[InlineData( "Effect.Key = \"gg\"\nlocal glow = Sprite()", "1:14: \"gg\" isn't a key you can bind: use a letter, a digit, f1 to f12, mouse4 or mouse5" )]
	[InlineData( "Effect.Include( Effects.Preset( \"Hart\" ) )", "1:33: There is no preset called \"Hart\": did you mean 'Hearts'?" )]
	[InlineData( "local glow = Sprite()\nfunction glow.Particle( p )\n\tp.Size = true\nend", "3:11: Cannot use a bool as a number" )]
	[InlineData( "local glow = Sprite()\nfunction glow.Particle( p )\n\tlocal q = p\nend", "3:12: A Particle can't be kept in a variable: read its fields" )]
	[InlineData( "if true then\n\tfunction f()\n\tend\nend", "2:11: Functions are made at the top of the script, not inside functions or blocks" )]
	public void EffectScriptsKnowWhereEachNameWorks( string source, string expected ) => Assert.Equal( expected, ScriptKit.Error( source ) );

	[Fact]
	public void NamesOutsideTheLibraryDontCompile()
	{
		foreach ( var name in new[] { "System.IO.File", "Game", "Scene", "GameObject", "load", "require", "os", "io", "print", "Input" } )
			Assert.Contains( "isn't declared", ScriptKit.Error( $"local x = {name}", ScriptKind.Sticker ) );
	}
}
