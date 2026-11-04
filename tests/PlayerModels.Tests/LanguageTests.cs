using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>Each language feature: statements, expressions, functions, lists, scoping, comments.</summary>
public class LanguageTests
{
	[Theory]
	[InlineData( "1 + 2 * 3", "7" )]
	[InlineData( "(1 + 2) * 3", "9" )]
	[InlineData( "10 - 4 - 3", "3" )]
	[InlineData( "2 * 3 / 4", "1.5" )]
	[InlineData( "7 % 3", "1" )]
	[InlineData( "-7 % 3", "-1" )]
	[InlineData( "- -2", "2" )]
	[InlineData( "1 / 0", "0" )]
	[InlineData( "5 % 0", "0" )]
	[InlineData( "1e15 * 1e15", "1000000000000000000000000000000" )]
	[InlineData( "3 < 4", "true" )]
	[InlineData( "3 <= 3", "true" )]
	[InlineData( "3 > 4", "false" )]
	[InlineData( "4 >= 5", "false" )]
	[InlineData( "2 == 2", "true" )]
	[InlineData( "2 ~= 2", "false" )]
	[InlineData( "true and false", "false" )]
	[InlineData( "true or false", "true" )]
	[InlineData( "not false", "true" )]
	[InlineData( "not (1 > 2) and 3 > 2", "true" )]
	[InlineData( "\"a\" == \"a\"", "true" )]
	[InlineData( "\"a\" ~= \"b\"", "true" )]
	[InlineData( "Color.Red == Color( 1, 0, 0 )", "true" )]
	[InlineData( "Vector3( 1, 2, 3 ) + Vector3( 1, 1, 1 )", "Vector3( 2, 3, 4 )" )]
	[InlineData( "Vector3( 1, 2, 3 ) - Vector3( 1, 1, 1 )", "Vector3( 0, 1, 2 )" )]
	[InlineData( "Vector3( 1, 2, 3 ) * 2", "Vector3( 2, 4, 6 )" )]
	[InlineData( "2 * Vector3( 1, 2, 3 )", "Vector3( 2, 4, 6 )" )]
	[InlineData( "Vector3( 2, 4, 6 ) / 2", "Vector3( 1, 2, 3 )" )]
	[InlineData( "-Vector3( 1, 2, 3 )", "Vector3( -1, -2, -3 )" )]
	[InlineData( "Color( 0.5, 0.5, 0.5 ) * Color( 1, 0, 1 )", "#800080" )]
	[InlineData( "Color.Red + Color.Blue", "#ff00ff" )]
	[InlineData( "Color.White / 2", "#80808080" )]
	[InlineData( "if 1 > 2 then 1 elseif 2 > 1 then 2 else 3", "2" )]
	[InlineData( "if false then Color.Red else Color.Blue", "#0000ff" )]
	[InlineData( "Joint.Head == Joint.Head", "true" )]
	[InlineData( "Joint.Head ~= Joint.Hips", "true" )]
	[InlineData( "{ 1, 2, 3 }[ 2 ]", "3" )]
	[InlineData( "{ 1, 2, 3 }[ 1.9 ]", "2" )]
	[InlineData( "{ Color.Red, Color.Blue }.Count", "2" )]
	[InlineData( "Shapes.Heart", "Shapes.Heart" )]
	public void Evaluates( string expression, string expected ) => Assert.Equal( expected, Eval.Of( expression ) );

	[Fact]
	public void ShortCircuitsAndAndOr()
	{
		// The right side would fault (an index out of range) if it ran
		Assert.Equal( "false", Eval.Of( "false and { 1 }[ 5 ] == 1" ) );
		Assert.Equal( "true", Eval.Of( "true or { 1 }[ 5 ] == 1" ) );
	}

	[Fact]
	public void LocalsTakeTheirValueOrATypesDefault()
	{
		var machine = Eval.Script( "local a: number\nlocal b: bool\nlocal c: Color\nlocal d: {number}\nlocal e = 2" );
		Assert.Equal( "0", machine.Variable( "a" ) );
		Assert.Equal( "false", machine.Variable( "b" ) );
		Assert.Equal( "#00000000", machine.Variable( "c" ) );
		Assert.Equal( "{}", machine.Variable( "d" ) );
		Assert.Equal( "2", machine.Variable( "e" ) );
	}

	[Fact]
	public void IfElseIfElse()
	{
		const string script = "local n = {0}\nlocal r = 0\nif n < 0 then\n\tr = -1\nelseif n == 0 then\n\tr = 0\nelse\n\tr = 1\nend";
		Assert.Equal( "-1", Eval.Script( string.Format( script, "-5" ) ).Variable( "r" ) );
		Assert.Equal( "0", Eval.Script( string.Format( script, "0" ) ).Variable( "r" ) );
		Assert.Equal( "1", Eval.Script( string.Format( script, "5" ) ).Variable( "r" ) );
	}

	[Theory]
	[InlineData( "for k = 1, 5 do s = s + k end", "15" )]
	[InlineData( "for k = 5, 1, -1 do s = s * 10 + k end", "54321" )]
	[InlineData( "for k = 0, 1, 0.25 do s = s + 1 end", "5" )]
	[InlineData( "for k = 1, 0 do s = s + 1 end", "0" )]
	[InlineData( "for k = 1, 3 do s = s + k k = 100 end", "6" )]
	[InlineData( "for k = 1, 10 do if k > 3 then break end s = s + k end", "6" )]
	[InlineData( "while s < 100 do s = s + 7 end", "105" )]
	[InlineData( "while true do s = s + 1 if s == 4 then break end end", "4" )]
	public void Loops( string loop, string expected ) => Assert.Equal( expected, Eval.Script( "local s = 0\n" + loop ).Variable( "s" ) );

	[Fact]
	public void FunctionsTakeTypedParametersAndInferTheirReturn()
	{
		var machine = Eval.Script( "local function area( w: number, h: number )\n\treturn w * h\nend\nlocal a = area( 3, 4 )" );
		Assert.Equal( "12", machine.Variable( "a" ) );
	}

	[Fact]
	public void RecursionWithAReturnType()
	{
		Assert.Equal( "120", Eval.Script( "local function fact( n: number ): number\n\tif n <= 1 then\n\t\treturn 1\n\tend\n\treturn n * fact( n - 1 )\nend\nlocal f = fact( 5 )" ).Variable( "f" ) );
	}

	[Fact]
	public void FunctionValuesAndAnonymousFunctions()
	{
		var machine = Eval.Script( "local twice = function( x: number ): number\n\treturn x * 2\nend\nlocal apply: (number) -> number = twice\nlocal r = apply( 21 )" );
		Assert.Equal( "42", machine.Variable( "r" ) );
	}

	[Fact]
	public void FunctionsSeeScriptVariablesDeclaredBeforeThem()
	{
		var machine = Eval.Script( "local base = 10\nlocal function plus( x: number ): number\n\treturn base + x\nend\nbase = 20\nlocal r = plus( 1 )" );
		Assert.Equal( "21", machine.Variable( "r" ) );
	}

	[Fact]
	public void BlocksScopeTheirLocals()
	{
		var machine = Eval.Script( "local x = 1\nif true then\n\tlocal x = 2\n\tx = 3\nend\nlocal y = x" );
		Assert.Equal( "1", machine.Variable( "y" ) );
	}

	[Fact]
	public void ListsBuiltAtRunTimeHoldTheirValues()
	{
		var machine = Eval.Script( "local n = 3\nlocal list = { n, n * 2, n * 3 }\nlocal last = list[ list.Count - 1 ]" );
		Assert.Equal( "9", machine.Variable( "last" ) );
		Assert.Equal( "{ 3, 6, 9 }", machine.Variable( "list" ) );
	}

	[Fact]
	public void AnIndexOutOfRangeStopsTheRun()
	{
		var fault = Eval.Fault( "local list = { 1, 2 }\nlocal x = list[ 2 ]" );
		Assert.Equal( FaultKind.IndexOutOfRange, fault.Kind );
		Assert.Equal( 2, fault.Index );
		Assert.Equal( 2, fault.Count );
	}

	[Fact]
	public void CommentsSemicolonsAndBlockComments()
	{
		var machine = Eval.Script( "--[[ a block\ncomment ]] local a = 1; local b = --[[inline]] 2 -- trailing\n;;local c = a + b" );
		Assert.Equal( "3", machine.Variable( "c" ) );
	}

	[Fact]
	public void TextKeepsItsEscapes()
	{
		Assert.Equal( "\"a\\\"b\\\\c\\nd\\te'f\"", Eval.Of( "\"a\\\"b\\\\c\\nd\\te\\'f\"" ) );
		Assert.Equal( "\"-- not a comment; really\"", Eval.Of( "'-- not a comment; really'" ) );
	}

	[Fact]
	public void AListItemChangesInPlaceForEveryVariableHoldingIt()
	{
		var machine = Eval.Script( "local a = { 1, 2, 3 }\nlocal b = a\nb[ 1 ] = 20\na[ 2 ] = a[ 2 ] + 1\nlocal colours = { Color.Red, Color.Blue }\ncolours[ 0 ] = Color.Green" );
		Assert.Equal( "{ 1, 20, 4 }", machine.Variable( "a" ) );
		Assert.Equal( "{ 1, 20, 4 }", machine.Variable( "b" ) );
		Assert.Equal( "{ #00ff00, #0000ff }", machine.Variable( "colours" ) );
	}

	[Fact]
	public void AnItemPastTheEndFaultsAndChangesNothing()
	{
		var fault = Eval.Fault( "local a = { 1, 2 }\na[ 2 ] = 5" );
		Assert.Equal( FaultKind.IndexOutOfRange, fault.Kind );
		Assert.Equal( FaultKind.IndexOutOfRange, Eval.Fault( "local a = { 1, 2 }\na[ -1 ] = 5" ).Kind );
	}

	[Fact]
	public void AWrittenListIsMadeAfreshEachRunOnceItemsChange()
	{
		// Without changes a list of fixed values is made once; with them, each run starts from what is written, like Lua
		var (machine, _) = ScriptKit.Build( "local glow = Sprite()\nlocal last = 0\nfunction OnUpdate( dt )\n\tlocal counts = { 0, 0 }\n\tcounts[ 0 ] = counts[ 0 ] + 1\n\tlast = counts[ 0 ]\nend" );
		for ( var i = 0; i < 3; i++ )
			Assert.True( machine.RunEvent( ScriptEvent.Update, new ScriptIO() ) );

		Assert.Equal( "1", machine.Variable( "last" ) );
	}

	[Fact]
	public void ListCreateMakesStateThatLastsBetweenRuns()
	{
		var (machine, _) = ScriptKit.Build( "local glow = Sprite()\nlocal seen = List.Create( 4, -1 )\nlocal at = 0\nfunction OnJump()\n\tseen[ at ] = t\n\tat = (at + 1) % seen.Count\nend" );
		var io = new ScriptIO();
		for ( var i = 0; i < 6; i++ )
		{
			io.Set( ScriptInput.Seconds, i );
			Assert.True( machine.RunEvent( ScriptEvent.Jump, io ) );
		}

		Assert.Equal( "{ 4, 5, 2, 3 }", machine.Variable( "seen" ) );
	}

	[Theory]
	[InlineData( "local a = List.Create( 257, 0 )", "List.Create makes at most 256 items, not 257" )]
	[InlineData( "local a = List.Create( -1, 0 )", "List.Create makes at most 256 items, not -1" )]
	[InlineData( "for k = 1, 100 do\n\tlocal a = List.Create( 256, Color.Red )\nend", "lists are too big together" )]
	public void ListCreateIsHeldToTheListCaps( string script, string message )
	{
		var build = new ScriptMachine( ScriptKit.Compile( script + "\nlocal glow = Sprite()" ) ).Build();
		Assert.False( build.Succeeded );
		Assert.Contains( message, build.Error.Message );
	}

	[Fact]
	public void ReturnAtTheTopEndsTheRun()
	{
		Assert.Equal( "1", Eval.Script( "local a = 1\nif a == 1 then\n\treturn\nend\na = 2" ).Variable( "a" ) );
	}

	[Fact]
	public void AFunctionThatCanEndWithoutItsValueFaultsIfItDoes()
	{
		Assert.Contains( "must give back a number on every path", ScriptKit.Error( "local function f( x: number ): number\n\tif x > 0 then\n\t\treturn 1\n\tend\nend", ScriptKind.Sticker ) );
	}

	[Fact]
	public void OutputsAreHeldToTheirRanges()
	{
		var io = ExampleRunner.Inputs();
		var machine = new ScriptMachine( ScriptKit.Compile( "color = Color( 5, -1, 0.5, 9 )\nalpha = -3\nsize = 1e9\noffset = Vector3( 100, -100, 0 )", ScriptKind.Sticker ) );
		Assert.True( machine.RunElement( io ) );
		Assert.Equal( new[] { 1.0, 0, 0.5, 1 }, io.Outputs[ ..4 ] );
		Assert.Equal( 0, io.Get( ScriptOutput.Alpha ) );
		Assert.Equal( PlayerModels.Core.EffectLayer.MaxSize, io.Get( ScriptOutput.Size ) );
		Assert.Equal( PlayerModels.Core.EffectLayer.MaxOffset, io.Get( ScriptOutput.OffsetX ) );
		Assert.Equal( ScriptIO.ColorSet | ScriptIO.AlphaSet | ScriptIO.SizeSet | ScriptIO.OffsetSet, io.OutputsSet );
	}

	[Fact]
	public void UnsafeInputsReadSafe()
	{
		var io = ExampleRunner.Inputs();
		io.Set( ScriptInput.Speed, double.NaN );
		io.Set( ScriptInput.Seconds, double.PositiveInfinity );
		var machine = new ScriptMachine( ScriptKit.Compile( "local s = speed\nlocal later = t", ScriptKind.Sticker ) );
		Assert.True( machine.RunElement( io ) );
		Assert.Equal( "0", machine.Variable( "s" ) );
		Assert.Equal( "1000000000000000", machine.Variable( "later" ) );
	}

	[Fact]
	public void SameScriptSameResultsOnEveryMachine()
	{
		const string script = "local n = Math.Noise( t * 3, i ) + Math.Random( i * 17 + time )\ncolor = Color.FromHsv( n * 180, 1, 1 )";
		var (a, b) = (new ScriptMachine( ScriptKit.Compile( script, ScriptKind.Sticker ) ), new ScriptMachine( ScriptKit.Compile( script, ScriptKind.Sticker ) ));
		var (ioA, ioB) = (ExampleRunner.Inputs(), ExampleRunner.Inputs());
		for ( var step = 0; step < 200; step++ )
		{
			foreach ( var io in new[] { ioA, ioB } )
			{
				io.Set( ScriptInput.Time, step * 16 );
				io.Set( ScriptInput.Seconds, step * 0.016 );
				io.Set( ScriptInput.Index, step % 7 );
			}

			Assert.True( a.RunElement( ioA ) && b.RunElement( ioB ) );
			Assert.Equal( ioA.Outputs, ioB.Outputs );
		}
	}
}
