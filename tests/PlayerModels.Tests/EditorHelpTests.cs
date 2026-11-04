using System.Collections.Generic;
using System.Linq;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>Completion, hover and signature help, at a caret marked | in each script.</summary>
public class EditorHelpTests
{
	private static (string Source, int Caret) At( string marked ) => (marked.Replace( "|", "" ), marked.IndexOf( '|' ));

	private static List<CompletionItem> Complete( string marked, ScriptKind kind = ScriptKind.Effect )
	{
		var (source, caret) = At( marked );
		return ScriptCompletion.At( source, caret, kind, ExampleRunner.Library );
	}

	private static List<string> Labels( string marked, ScriptKind kind = ScriptKind.Effect ) => Complete( marked, kind ).Select( x => x.Label ).ToList();

	[Fact]
	public void MembersAfterANamespace()
	{
		var labels = Labels( "local x = Math.|" );
		Assert.Contains( "Floor", labels );
		Assert.Contains( "PI", labels );
		Assert.Equal( "Floor", Labels( "local x = Math.Fl|" )[ 0 ] );
	}

	[Fact]
	public void MembersOfALayerByItsName()
	{
		var labels = Labels( "local glow = Sprite()\nglow.|" );
		Assert.Contains( "Size", labels );
		Assert.Contains( "Particle", labels );
		Assert.DoesNotContain( "Emit", labels );
		Assert.Contains( "Emit", Labels( "local glow = Sprite()\nfunction OnJump()\n\tglow.|\nend" ) );
	}

	[Fact]
	public void MembersOfAParticleAndAShot()
	{
		Assert.Contains( "Index", Labels( "local glow = Sprite()\nfunction glow.Particle( p )\n\tp.|\nend" ) );
		Assert.Contains( "HitPosition", Labels( "local glow = Sprite()\nfunction OnShoot( shot )\n\tlocal at = shot.Hit|\nend" ) );
	}

	[Fact]
	public void MembersOfAValueByItsType()
	{
		Assert.Contains( "Length", Labels( "local v = Vector3.Up\nlocal l = v.|", ScriptKind.Sticker ) );
		Assert.Contains( "Count", Labels( "local l = { 1, 2 }\nlocal n = l.|", ScriptKind.Sticker ) );
		Assert.Contains( "R", Labels( "local x = Color.Red.|", ScriptKind.Sticker ) );
	}

	[Fact]
	public void StockShapesAfterShapes()
	{
		var items = Complete( "local glow = Sprite()\nglow.Formation = Shapes.|" );
		var heart = items.Single( x => x.Label == "Heart" );
		Assert.Equal( "Stock", heart.Group );
		Assert.Equal( "shape:Heart", heart.Icon );
		Assert.Contains( items, x => x.Label == "Mine" );
	}

	[Fact]
	public void ThePlayersOwnInsideMine()
	{
		var shapes = Complete( "local s = Shapes.Mine( \"|" );
		Assert.Equal( new[] { "COOL" }, shapes.Select( x => x.Label ) );
		Assert.Equal( ("My shapes", "c-cool"), (shapes[ 0 ].Group, shapes[ 0 ].Icon) );
		Assert.Equal( new[] { "Gold skull" }, Labels( "local s = Stickers.Mine( \"Go|" ) );
		Assert.Empty( Labels( "local s = Stickers.Mine( \"Zz|" ) );
		Assert.Equal( new[] { "Clan" }, Labels( "local s = Emblems.Mine( \"|" ) );
		Assert.Equal( new[] { "Fire hands" }, Labels( "local e = Effects.Mine( \"|" ) );
		Assert.Contains( "Hearts", Labels( "local e = Effects.Preset( \"He|" ) );
	}

	[Fact]
	public void KeysWithTheirIcons()
	{
		var keys = Complete( "Effect.Key = \"f|" );
		Assert.All( keys, x => Assert.StartsWith( "f", x.Label ) );
		Assert.Contains( keys, x => x.Label == "f1" && x.Icon == "key:f1" );
		Assert.Contains( "g", Labels( "local glow = Sprite()\nfunction OnKey( key )\n\tif key == \"|" ) );
		Assert.Equal( PlayerModels.Core.Effect.Keys.Count, Labels( "Effect.Key = \"|" ).Count );
	}

	[Fact]
	public void EventsAfterFunction()
	{
		var items = Complete( "local glow = Sprite()\nfunction On|" );
		Assert.Contains( items, x => x.Label == "OnShoot" && x.Insert == "OnShoot( shot )" );
		Assert.Contains( items, x => x.Label == "OnUpdate" && x.Insert == "OnUpdate( dt )" );
		Assert.Contains( "glow", Labels( "local glow = Sprite()\nfunction |" ) );
	}

	[Fact]
	public void TheScriptsOwnNamesAndWhatWorksWhereTheCaretIs()
	{
		var top = Labels( "local speedUp = 2\nlocal x = sp|" );
		Assert.Contains( "speedUp", top );
		Assert.Contains( "Sprite", top );
		Assert.DoesNotContain( "speed", top );
		Assert.Contains( "speed", Labels( "local glow = Sprite()\nfunction OnUpdate( dt )\n\tglow.Size = sp|\nend" ) );
		Assert.Contains( "local", Labels( "lo|" ) );
	}

	[Fact]
	public void TheAttributeAndItsOptions()
	{
		Assert.Equal( new[] { "Parameter" }, Labels( "[|" ) );
		var options = Labels( "[Parameter( \"Size\", M|" );
		Assert.Equal( new[] { "Max", "Min" }, options );
	}

	[Fact]
	public void ColoursCarryTheirSwatch() => Assert.Contains( Complete( "local c = Color.|", ScriptKind.Sticker ), x => x.Label == "Red" && x.Icon == "color:#ff0000" );

	[Fact]
	public void NothingInsideAComment() => Assert.Empty( Complete( "-- Math.|" ) );

	[Fact]
	public void HoverShowsALibraryEntrysOwnText()
	{
		var (source, caret) = At( "local x = Math.Fl|oor( 2 )" );
		var hover = ScriptHover.At( source, caret, ScriptKind.Sticker );
		Assert.Equal( "Math.Floor( x: number ): number", hover.Title );
		Assert.Equal( ScriptLibrary.InNamespace( "Math", "Floor" ).Doc, hover.Doc );
		Assert.Contains( "-->", hover.Example );
	}

	[Theory]
	[InlineData( "i|f true then end", "if" )]
	[InlineData( "local glow = Sprite()\nglow.Si|ze = 2", "Layer.Size: number  (0.1 to 48)" )]
	[InlineData( "local glow = Sprite()\nfunction OnLa|nd( fallSpeed )\nend", "function OnLand( fallSpeed: number )" )]
	[InlineData( "[Param|eter( Min = 0, Max = 1 )]\nlocal a = 0.5", "Parameter( [title: string] )" )]
	[InlineData( "[Parameter( M|in = 0, Max = 1 )]\nlocal a = 0.5", "Parameter.Min: number" )]
	[InlineData( "local x: Vec|tor3 = Vector3.Up", "Vector3" )]
	[InlineData( "local x = Ma|th.PI", "Math" )]
	[InlineData( "local x = Joint.He|ad", "Joint.Head: Joint" )]
	public void HoverTitles( string marked, string title )
	{
		var (source, caret) = At( marked );
		Assert.Equal( title, ScriptHover.At( source, caret )?.Title );
	}

	[Fact]
	public void HoverOnTheScriptsOwnName()
	{
		var (source, caret) = At( "local glow = Sprite()\n\nlocal size = 2\nglow.Size = si|ze" );
		var hover = ScriptHover.At( source, caret );
		Assert.Equal( "size: number", hover.Title );
		Assert.Equal( "Your variable, declared on line 3.", hover.Doc );
	}

	[Fact]
	public void HoverOnColoursAndKeys()
	{
		var (source, caret) = At( "Effect.Key = \"|g\"\nlocal glow = Sprite()\nglow.Color = Color( 1, 0.5, 0 )" );
		var key = ScriptHover.At( source, caret );
		Assert.Equal( ("Key \"g\"", "g"), (key.Title, key.Key) );
		var colour = ScriptHover.At( source, source.IndexOf( "0.5" ) );
		Assert.Equal( "#ff8000", colour.Color );
		Assert.Equal( source.IndexOf( "Color( 1" ), colour.Start );
	}

	[Fact]
	public void EveryLibraryEntryHasHoverText()
	{
		foreach ( var entry in ScriptLibrary.All )
		{
			Assert.False( string.IsNullOrWhiteSpace( entry.Doc ), entry.Name );
			Assert.False( string.IsNullOrWhiteSpace( entry.Signature() ), entry.Name );
		}
	}

	[Theory]
	[InlineData( "local x = Math.Clamp( 1, |", "Math.Clamp( x: number, min: number, max: number ): number", 1 )]
	[InlineData( "local x = Math.Clamp( Math.Abs( 2 ), 0, |", "Math.Clamp( x: number, min: number, max: number ): number", 2 )]
	[InlineData( "local c = Color.FromHsv( |", "Color.FromHsv( hue: number, saturation: number, value: number, [a: number] ): Color", 0 )]
	[InlineData( "local c = Color( 1, { 2, 3 }, |", "Color( r: number, g: number, b: number, [a: number] ): Color", 2 )]
	[InlineData( "local glow = Sprite()\nfunction OnJump()\n\tglow.EmitAt( Vector3.Up, |", "Layer.EmitAt( position: Vector3, count: number )", 1 )]
	[InlineData( "local function twice( x: number ): number\n\treturn x * 2\nend\nlocal y = twice( |", "twice( x: number )", 0 )]
	public void SignatureHelpKnowsTheParameter( string marked, string signature, int active )
	{
		var (source, caret) = At( marked );
		var help = ScriptSignature.At( source, caret, ScriptKind.Sticker );
		Assert.Equal( (signature, active), (help?.Signature, help?.Active ?? -1) );
	}

	[Fact]
	public void NoSignatureOutsideACall() => Assert.Null( ScriptSignature.At( "local x = { 1, 2 }", 15 ) );

	[Fact]
	public void KeyLiteralsAreFoundForTheirIcons()
	{
		var program = ScriptKit.Compile( "Effect.Key = \"G\"\nlocal glow = Sprite()\nfunction OnKey( key )\n\tif \"f1\" == key then\n\t\tglow.Emit( 1 )\n\tend\nend" );
		Assert.Equal( new[] { "g", "f1" }, program.Keys.Select( x => x.Key ) );
		Assert.Equal( 13, program.Keys[ 0 ].Start );
	}
}
