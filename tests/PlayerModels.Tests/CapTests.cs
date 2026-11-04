using System.Text;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>Every compile cap refuses what is over it, whole, with a message saying which; and the runtime limits stop runs cleanly.</summary>
public class CapTests
{
	private static string Repeat( string text, int times ) => new StringBuilder().Insert( 0, text, times ).ToString();

	[Fact]
	public void TooLongStoredIsRefusedWithItsSize()
	{
		var source = Repeat( "-- " + Repeat( "x", 100 ) + "\n", 170 ) + "local a = 1";
		Assert.Matches( @"Script is too long: \d+\.\d KB of 16 KB", ScriptKit.Error( source, ScriptKind.Sticker ) );
	}

	[Fact]
	public void ScriptsAreMeasuredMinified()
	{
		var formatted = "local a = 1\n" + Repeat( "if a > 0 then\n\t\t\t\t\t\t\t\ta = a + 1\nend\n", 600 );
		var result = ScriptCompiler.Compile( formatted, ScriptKind.Sticker );
		Assert.True( formatted.Length > ScriptLimits.MaxScriptBytes );
		Assert.True( result.Succeeded, string.Join( "\n", result.Errors ) );
		Assert.True( result.StoredBytes <= ScriptLimits.MaxScriptBytes );
	}

	[Theory]
	[InlineData( "tokens", "The script has too many words and symbols" )]
	[InlineData( "line", "This line is too long" )]
	[InlineData( "functions", "A script has at most 64 functions" )]
	[InlineData( "locals", "A function has at most 128 local value slots" )]
	[InlineData( "globals", "The script has too many variables" )]
	[InlineData( "constants", "The script has too many constant values" )]
	[InlineData( "list", "A list has at most 256 items" )]
	[InlineData( "lists", "The script's lists are too big together" )]
	[InlineData( "parameters", "A script has at most 32 parameters" )]
	[InlineData( "name", "This name is too long" )]
	public void CompileCapsRefuseWhatIsOver( string cap, string message )
	{
		var source = cap switch
		{
			"tokens" => "local x = 0\n" + Repeat( "x=1\n", 5600 ),
			"line" => "local a = 1" + Repeat( " + 1", 1100 ),
			"functions" => Repeat( "local function f()\nend\n", 65 ),
			"locals" => "local function f()\n" + Repeat( "\tlocal v: Color\n", 33 ) + "end",
			"globals" => Repeat( "local v = Color.Red\n", 65 ),
			"constants" => "local c = Color.Red\n" + Repeat( "c = Color.Blue\n", 1030 ),
			"list" => "local l = { " + Repeat( "1, ", 257 ) + "}",
			"lists" => Repeat( "local l = { " + Repeat( "1, ", 255 ) + "}\n", 17 ),
			"parameters" => Parameters( 33 ),
			_ => "local " + Repeat( "a", 65 ) + " = 1",
		};

		Assert.Contains( message, string.Join( "\n", ScriptCompiler.Compile( source, ScriptKind.Sticker ).Errors ) );
	}

	private static string Parameters( int count )
	{
		var text = new StringBuilder();
		for ( var i = 0; i < count; i++ )
			text.Append( $"[Parameter]\nlocal p{i} = 1\n" );

		return text.ToString();
	}

	[Fact]
	public void ALooksScriptsAreCappedTogether()
	{
		var big = Repeat( "a", 12 * 1024 );
		Assert.True( ScriptLimits.Fits( big ) );
		Assert.False( ScriptLimits.Fits( big + big ) );
		Assert.True( ScriptLimits.LookFits( new[] { big, big } ) );
		Assert.False( ScriptLimits.LookFits( new[] { big, big, big } ) );
		Assert.False( ScriptLimits.Fits( Repeat( "é", 9 * 1024 ) ) );
	}

	[Theory]
	[InlineData( "while true do\nend", FaultKind.Budget )]
	[InlineData( "for k = 0, 1e15 do\nend", FaultKind.Budget )]
	[InlineData( "local n = 0\nfor k = 1, 1e6 do\n\tn = n + k\nend", FaultKind.Budget )]
	[InlineData( "local function down( n: number ): number\n\treturn down( n + 1 )\nend\nlocal x = down( 0 )", FaultKind.CallDepth )]
	public void RunsStopAtTheirLimits( string source, FaultKind kind ) => Assert.Equal( kind, Eval.Fault( source ).Kind );

	[Fact]
	public void AnEventThatRunsOutStopsForThatEventOnly()
	{
		var (machine, _) = ScriptKit.Build( "local glow = Sprite()\nlocal ticks = 0\nfunction OnUpdate( dt )\n\tticks = ticks + 1\nend\nfunction OnJump()\n\twhile true do\n\tend\nend" );
		var io = new ScriptIO();
		Assert.False( machine.RunEvent( ScriptEvent.Jump, io ) );
		Assert.Equal( FaultKind.Budget, machine.Fault.Kind );
		Assert.True( machine.RunEvent( ScriptEvent.Update, io ) );
		Assert.Equal( "1", machine.Variable( "ticks" ) );
	}

	[Fact]
	public void AHostCanOnlyLowerTheBudget()
	{
		var (machine, _) = ScriptKit.Build( "local glow = Sprite()\nfunction OnUpdate( dt )\n\tfor k = 1, 100 do\n\t\tglow.Size = k\n\tend\nend" );
		Assert.False( machine.RunEvent( ScriptEvent.Update, new ScriptIO(), budget: 50 ) );
		Assert.True( machine.RunEvent( ScriptEvent.Update, new ScriptIO() ) );
	}

	[Fact]
	public void BuildingStopsAtItsBudget()
	{
		var machine = new ScriptMachine( ScriptKit.Compile( "local glow = Sprite()\nwhile true do\nend" ) );
		var build = machine.Build();
		Assert.False( build.Succeeded );
		Assert.Contains( "ran too long", build.Error.Message );
	}

	[Theory]
	[InlineData( "for k = 1, 9 do\n\tlocal l = Sprite()\nend", "An effect has at most 8 layers" )]
	[InlineData( "local glow = Sprite()\nglow.Palette = { " + "Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red, Color.Red }", "A palette has at most 32 colours" )]
	[InlineData( "local glow = Sprite()\nlocal s = Shapes.New( \"x\" )\nfor k = 0, 600 do\n\ts.Point( 0, k / 600 )\nend", "A shape has at most 500 points" )]
	[InlineData( "local glow = Sprite()\nlocal s = Shapes.New( \"x\" )\nfor k = 0, 32 do\n\tlocal l = s.AddLayer( Shapes.Heart )\nend", "An emblem has at most 32 layers" )]
	[InlineData( "local glow = Sprite()\nfor k = 0, 8 do\n\tlocal s = Shapes.New( \"x\" )\nend", "A script makes at most 8 shapes" )]
	[InlineData( "local glow = Sprite()\nShapes.Heart.Point( 0, 0 )", "a standard shape is fixed" )]
	[InlineData( "", "An effect needs at least one layer" )]
	public void BuildCapsStopTheBuildWithAMessage( string source, string message )
	{
		var build = new ScriptMachine( ScriptKit.Compile( source ) ).Build();
		Assert.False( build.Succeeded );
		Assert.Contains( message, build.Error.Message );
	}
}
