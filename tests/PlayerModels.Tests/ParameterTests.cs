using System.Collections.Generic;
using System.Linq;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>[Parameter]: what each widget is, chosen values (clamped, unknown and stale ones dropped), errors, and "save as default".</summary>
public class ParameterTests
{
	private const string Every = """
		[Parameter( "Glow size", Min = 0.5, Max = 10, Step = 0.1, Group = "Look", Tip = "How big each glow is", Order = 2 )]
		local glowSize = 2.6

		[Parameter( Min = 1, Max = 8, Step = 1 )]
		local points = 5

		[Parameter]
		local sparkly = true

		[Parameter]
		local glowColor = Color( 1, 0.5, 0 )

		[Parameter]
		local colours = { Color.Red, Color.Parse( "#00ff00" ) }

		[Parameter]
		local label = "hi"

		[Parameter( Choices = { "Spin", "Pulse" } )]
		local motion = "Pulse"

		[Parameter]
		local hand = Joint.LeftHand

		[Parameter]
		local outline = Shapes.Heart

		[Parameter]
		local look: Picture = Picture.Flame

		[Parameter]
		local key: Key = "g"

		[Parameter( Min = -4, Max = 4 )]
		local nudge = Vector3( 0, 1, 0 )

		local glow = Sprite()
		glow.Size = glowSize
		""";

	private static ScriptProgram Program() => ScriptKit.Compile( Every );

	[Fact]
	public void EachTypeIsItsWidget()
	{
		var parameters = Program().Parameters;
		Assert.Equal(
			new[]
			{
				("glowSize", ParameterKind.Number, "Glow size", "2.6"), ("points", ParameterKind.Integer, "Points", "5"), ("sparkly", ParameterKind.Bool, "Sparkly", "true"),
				("glowColor", ParameterKind.Color, "Glow color", "#ff8000"), ("colours", ParameterKind.Palette, "Colours", "#ff0000,#00ff00"),
				("label", ParameterKind.Text, "Label", "hi"), ("motion", ParameterKind.Choice, "Motion", "Pulse"), ("hand", ParameterKind.Choice, "Hand", "LeftHand"),
				("outline", ParameterKind.Shape, "Outline", "Heart"), ("look", ParameterKind.Picture, "Look", "Flame"), ("key", ParameterKind.Key, "Key", "g"),
				("nudge", ParameterKind.Vector3, "Nudge", "0,1,0"),
			},
			parameters.Select( x => (x.Name, x.Kind, x.Title, x.Default) ) );

		var size = parameters[ 0 ];
		Assert.Equal( (0.5, 10, 0.1, "Look", "How big each glow is", 2.0), (size.Min, size.Max, size.Step, size.Group, size.Tip, size.Order) );
		Assert.Equal( new[] { "Spin", "Pulse" }, parameters[ 6 ].Choices );
		Assert.Contains( "RightHand", parameters[ 7 ].Choices );
		Assert.Equal( 2, size.Line );
	}

	[Fact]
	public void ChosenValuesRoundTripForEveryWidget()
	{
		var chosen = new Dictionary<string, string>
		{
			["glowSize"] = "7.5", ["points"] = "3", ["sparkly"] = "false", ["glowColor"] = "#123456", ["colours"] = "#010203,#040506,#070809",
			["label"] = "new text", ["motion"] = "Spin", ["hand"] = "RightHand", ["outline"] = "Star5", ["look"] = "Spark", ["key"] = "f2", ["nudge"] = "1,-2,3",
		};

		var machine = new ScriptMachine( Program() );
		Assert.Equal( chosen, machine.SetParameters( chosen ) );
		var build = machine.Build();
		Assert.True( build.Succeeded );
		Assert.Equal( 7.5f, build.Effect.Layers[ 0 ].Size );
		Assert.Equal( "7.5", machine.Variable( "glowSize" ) );
		Assert.Equal( "{ #010203, #040506, #070809 }", machine.Variable( "colours" ) );
		Assert.Equal( "\"new text\"", machine.Variable( "label" ) );
		Assert.Equal( "Joint.RightHand", machine.Variable( "hand" ) );
		Assert.Equal( "Shapes.Star5", machine.Variable( "outline" ) );
		Assert.Equal( "Picture.Spark", machine.Variable( "look" ) );
		Assert.Equal( "\"f2\"", machine.Variable( "key" ) );
		Assert.Equal( "Vector3( 1, -2, 3 )", machine.Variable( "nudge" ) );
	}

	// A player's own library holding one sticker made of layers
	private sealed class OwnStickers : IPlayerLibrary
	{
		public static readonly PlayerModels.Core.DrawnShape Badge = new PlayerModels.Core.DrawnShape
		{
			Name = "Clan badge", Finish = "Holo", Emblem = new List<PlayerModels.Core.EmblemLayer>
			{
				new() { Shape = "Circle", Color = "#202020" }, new() { Shape = "Star5", Color = "#ffcc00", ScaleX = 0.6f, ScaleY = 0.6f },
			},
		}.Sanitized();

		public IReadOnlyList<LibraryItem> Items { get; } = new[] { new LibraryItem( "Clan badge", LibraryItemKind.Sticker, Badge.Content ) };
		public PlayerModels.Core.Effect Effect( string name ) => null;
		public PlayerModels.Core.DrawnShape Shape( string name, LibraryItemKind kind ) => name == Badge.Name && LookLibrary.Is( Badge, kind ) ? Badge : null;
	}

	[Fact]
	public void AShapeParameterTakesThePlayersOwnWholeAndCarriesIt()
	{
		const string script = """
			[Parameter]
			local badge = Shapes.Heart

			local glow = Sprite()
			glow.LooksLike = badge
			""";

		var chosen = new Dictionary<string, string> { ["badge"] = ScriptParameter.OwnShapeText( LibraryItemKind.Sticker, "Clan badge" ) };
		var machine = new ScriptMachine( ScriptKit.Compile( script ) );
		Assert.Equal( chosen, machine.SetParameters( chosen ) );
		var build = machine.Build( new OwnStickers() );
		Assert.True( build.Succeeded, build.Error?.ToString() );
		Assert.Equal( OwnStickers.Badge.Reference, build.Effect.Layers[ 0 ].Picture );
		var carried = Assert.Single( build.Effect.Shapes );
		Assert.Equal( OwnStickers.Badge.Content, carried.Content );
		Assert.Equal( 2, carried.Emblem.Count );

		// Without it (another machine whose look doesn't carry it, or it was deleted) the script's own default shows
		var without = new ScriptMachine( ScriptKit.Compile( script ) );
		without.SetParameters( chosen );
		var fallback = without.Build( new LookLibrary( null ) );
		Assert.True( fallback.Succeeded );
		Assert.Equal( "Heart", fallback.Effect.Layers[ 0 ].Picture );

		Assert.Equal( chosen, ScriptParameter.Sanitized( ScriptKit.Compile( script ), chosen ) );
		Assert.Empty( ScriptParameter.Sanitized( ScriptKit.Compile( script ), new Dictionary<string, string> { ["badge"] = "@effect:Fire hands" } ) );
		Assert.Empty( ScriptParameter.Sanitized( ScriptKit.Compile( script ), new Dictionary<string, string> { ["badge"] = "@sticker:" + new string( 'x', 200 ) } ) );
	}

	[Fact]
	public void TamperedValuesAreClampedOrDropped()
	{
		var tampered = new Dictionary<string, string>
		{
			["glowSize"] = "1e999", ["points"] = "2.7", ["sparkly"] = "maybe", ["glowColor"] = "red", ["colours"] = string.Join( ",", Enumerable.Repeat( "#ffffff", 40 ) ),
			["label"] = new string( 'x', 1000 ), ["motion"] = "Explode", ["hand"] = "Tail", ["outline"] = "@0123456789abcdef", ["key"] = "ctrl", ["nudge"] = "9,NaN,-9",
			["unknown"] = "1",
		};

		var kept = ScriptParameter.Sanitized( Program(), tampered );
		Assert.Equal( new Dictionary<string, string> { ["glowSize"] = "10", ["points"] = "3", ["nudge"] = "4,0,-4" }, kept );
	}

	[Fact]
	public void ARenamedParameterDropsItsStaleValue()
	{
		var renamed = ScriptKit.Compile( Every.Replace( "glowSize", "glowWidth" ) );
		var machine = new ScriptMachine( renamed );
		var kept = machine.SetParameters( new Dictionary<string, string> { ["glowSize"] = "9", ["points"] = "4" } );
		Assert.Equal( new Dictionary<string, string> { ["points"] = "4" }, kept );
		Assert.Equal( 2.6f, machine.Build().Effect.Layers[ 0 ].Size );
	}

	[Theory]
	[InlineData( "[Parameter( Min = 5, Max = 1 )]\nlocal a = 2", "Min (5) is above Max (1)" )]
	[InlineData( "[Parameter( Min = 0 )]\nlocal a = 2", "Give both Min and Max, or neither" )]
	[InlineData( "[Parameter( Min = 0, Max = 1 )]\nlocal a = 2", "The default (2) is outside Min to Max (0 to 1)" )]
	[InlineData( "[Parameter( Step = 0 )]\nlocal a = 2", "Step must be above 0" )]
	[InlineData( "[Parameter( Step = 1 )]\nlocal a = 2.5", "A whole-number parameter (Step 1) needs a whole default" )]
	[InlineData( "[Parameter]\nlocal a = 1 + 1", "A parameter's default must be written out" )]
	[InlineData( "local b = 1\n[Parameter]\nlocal a = b", "A parameter's default must be written out" )]
	[InlineData( "if true then\n\t[Parameter]\n\tlocal a = 1\nend", "[Parameter] goes on a local at the top of the script" )]
	[InlineData( "[Parameter]\nlocal a = 1\n[Parameter]\nlocal a = 2", "There is already a parameter called 'a'" )]
	[InlineData( "[Parameter( Mni = 1 )]\nlocal a = 1", "[Parameter] has no option 'Mni': did you mean 'Min'?" )]
	[InlineData( "[Parameter( Min = \"a\", Max = 2 )]\nlocal a = 1", "Min must be a number written out" )]
	[InlineData( "[Parameter( Group = 1 )]\nlocal a = 1", "Group must be text in quotes" )]
	[InlineData( "[Parameter( Choices = 1 )]\nlocal a = \"x\"", "Choices is a list of text" )]
	[InlineData( "[Parameter( Choices = { \"x\" } )]\nlocal a = 1", "Choices are for text parameters" )]
	[InlineData( "[Parameter( Choices = { \"x\" } )]\nlocal a = \"y\"", "The default must be one of the Choices" )]
	[InlineData( "[Parameter( Min = 0, Max = 1 )]\nlocal a = true", "Min, Max and Step are for number and Vector3 parameters" )]
	[InlineData( "[Parameter( Min = 1, Min = 2, Max = 3 )]\nlocal a = 2", "Min is given twice" )]
	[InlineData( "[Parameter( 1 )]\nlocal a = 1", "[Parameter] takes its title as text" )]
	[InlineData( "[Paramter]\nlocal a = 1", "There is no attribute [Paramter]" )]
	[InlineData( "[Parameter]\nlocal a = Sprite()", "A parameter's default must be written out" )]
	[InlineData( "[Parameter]\nlocal function f()\nend", "An attribute goes on a value, not a function" )]
	public void BadParametersDontCompile( string source, string message ) =>
		Assert.Contains( message, string.Join( "\n", ScriptCompiler.Compile( source + "\nlocal glow = Sprite()" ).Errors ) );

	[Theory]
	[InlineData( "local size = 2.6", "size", "4", "local size = 4" )]
	[InlineData( "local c = Color( 1, 0.5, 0 )", "c", "#00ff00", "local c = Color( 0, 1, 0 )" )]
	[InlineData( "local c = Color.Parse( \"#FF0000\" )", "c", "#00ff00", "local c = Color.Parse( \"#00FF00\" )" )]
	[InlineData( "local c = Color.FromHsv( 0, 1, 1 )", "c", "#0000ff", "local c = Color.FromHsv( 240, 1, 1 )" )]
	[InlineData( "local p = { Color.Red }", "p", "#000000,#ffffff", "local p = { Color.Parse( \"#000000\" ), Color.Parse( \"#ffffff\" ) }" )]
	[InlineData( "local j = Joint.Head", "j", "Hips", "local j = Joint.Hips" )]
	[InlineData( "local on = true", "on", "false", "local on = false" )]
	[InlineData( "local s = Shapes.Heart", "s", "Cube", "local s = Shapes.Cube" )]
	[InlineData( "local t = \"a\"", "t", "b \"quoted\"", "local t = \"b \\\"quoted\\\"\"" )]
	public void SaveAsDefaultRewritesTheLiteralInItsForm( string declaration, string name, string value, string expected )
	{
		var source = $"-- keep me\n[Parameter]\n{declaration} -- and me\nlocal glow = Sprite()";
		var edited = ScriptEdit.SaveAsDefault( source, ScriptKit.Compile( source ), name, value );
		Assert.True( edited.Succeeded, edited.Problem );
		Assert.Equal( $"-- keep me\n[Parameter]\n{expected} -- and me\nlocal glow = Sprite()", edited.Source );
		Assert.Equal( value.Contains( '"' ) ? "b \"quoted\"" : value, ScriptKit.Compile( edited.Source ).Parameters[ 0 ].Default );
	}
}
