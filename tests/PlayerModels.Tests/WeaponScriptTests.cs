using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>
/// A weapon design as a script (<see cref="ScriptKind.Weapon"/>): the script builds the design the weapon panel makes,
/// codegen writes a design as a script that builds it back, the panel's changes are made to a script in place, the
/// caps hold, and the design keeps its script within the look's limits.
/// </summary>
public class WeaponScriptTests
{
	private const string Weapon = "models/weapons/sbox_assault_m4a1/w_m4a1.vmdl";

	public const string RainbowFlowingSkin = """
		-- Rainbow flowing skin: the colour wheel flowing along the weapon, twice the clan tags' pace, over carbon fibre
		Skin.Pattern = Pattern.Carbon
		Skin.Animation = SkinAnimation.Rainbow
		Skin.Speed = 2
		""";

	public const string GoldWithAHoloSticker = """
		-- Gold with a holo sticker: a gold finish, and a star printed on holo film on the left of the receiver
		Weapon.Finish = Finish.Gold

		local star = Weapon.AddSticker( Shapes.Star5 )
		star.Position = Vector3( 4, 0, 6.5 )
		star.Rotation = Vector3( 0, 0, -90 )
		star.Size = 4
		star.Finish = StickerFinish.Holo
		star.Border = true
		""";

	public const string MagazineReplacedByACan = """
		-- Magazine replaced by a can: the can takes the magazine's place and follows it through a reload
		local can = Weapon.AddProp( "models/sbox_props/cola_can/cola_can.vmdl" )
		can.Part = "magazine"
		can.Replaces = true
		can.Scale = 0.8
		""";

	private static WeaponDesign Build( string source, WeaponDesign carried = null )
	{
		var build = WeaponScripts.Build( source, carried, new ScriptCache(), ExampleRunner.Library );
		Assert.True( build.Succeeded, "Build failed: " + build.Error + "\n---\n" + source );
		build.Design.Weapon = Weapon;
		return build.Design;
	}

	// A design as what makes it look the same: within bounds, without its origin or script
	private static string Same( WeaponDesign design )
	{
		var copy = design.Sanitized();
		if ( copy is null )
			return "";

		copy.ForkedFrom = null;
		copy.Script = null;
		return JsonSerializer.Serialize( copy );
	}

	[Fact]
	public void TheRainbowExampleFlowsOverCarbon()
	{
		var design = Build( RainbowFlowingSkin );
		Assert.Equal( WeaponSkin.Pattern.Carbon, design.Skin.PatternIs );
		Assert.Equal( WeaponSkin.Motion.Rainbow, design.Skin.AnimationIs );
		Assert.Equal( 2f, design.Skin.Speed );
	}

	[Fact]
	public void TheGoldExampleHasAHoloSticker()
	{
		var design = Build( GoldWithAHoloSticker );
		Assert.Equal( "Gold", design.Finish );
		var star = Assert.Single( design.Stickers );
		Assert.Equal( nameof( StandardShape.Star5 ), star.Picture );
		Assert.Equal( (4f, 0f, 6.5f), (star.Forward, star.Left, star.Up) );
		Assert.Equal( -90f, star.Roll );
		Assert.Equal( "Holo", star.Finish );
		Assert.True( star.Border );
	}

	[Fact]
	public void TheCanExampleReplacesTheMagazine()
	{
		var design = Build( MagazineReplacedByACan );
		var can = Assert.Single( design.Props );
		Assert.Equal( "magazine", can.Part );
		Assert.True( can.Replaces );
		Assert.Equal( 0.8f, can.Scale );
	}

	public static WeaponDesign Rich()
	{
		var tracer = EffectPresets.Copy( EffectPresets.All.First( x => x.Name == "Bullet tracer" ), new List<Effect>() );
		tracer.Hue = 0.25f;
		var own = EffectPresets.Copy( EffectPresets.All.First( x => x.Name == "Sparkle halo" ), new List<Effect>() );
		own.Name = "My halo";
		own.Layers[ 0 ].Color = "#123456";
		WeaponDesign.ShowWhen( own, nameof( LayerWhen.OnReload ) );
		return new WeaponDesign
		{
			Weapon = Weapon, Finish = "Matte black", Tint = "#ff8800",
			Skin = new WeaponSkin { PatternName = "Stripes", PatternColor = "#101010", PatternColorEnd = "#e0c040", PatternScale = 0.6f, PatternTurn = 45f, PatternScroll = 0.5f,
				Animation = "Rain", Speed = 2f, Bands = 12, Palette = new List<string> { "#ff0000", "#00ff00" } },
			Stickers = new List<WeaponSticker>
			{
				new() { Picture = "Star5", Color = "#ffd000", Forward = 8, Up = 7, Roll = -90, Size = 4, Finish = "Holo", Border = true },
				new() { Picture = "Heart", Animation = "Rain", Palette = new List<string> { "#ff00ff", "#00ffff" }, Speed = 2, Scrape = 0.3f },
			},
			Props = new List<WeaponProp>
			{
				new() { Model = "models/sbox_props/cola_can/cola_can.vmdl", Part = "magazine", Replaces = true, Scale = 0.8f, NudgeUp = 1, NudgeYaw = 15 },
				new() { Model = "models/citizen_props/hotdog01.vmdl", Forward = 6, Up = 11, Scale = 0.4f, Yaw = 90 },
			},
			HiddenParts = new List<string> { "rail" },
			Effects = new List<Effect> { tracer, own },
		}.Sanitized();
	}

	[Fact]
	public void ADesignWrittenAsAScriptBuildsBackTheSame()
	{
		var design = Rich();
		var script = ScriptCodegen.Weapon( design );
		Assert.Contains( "Effects.Preset( \"Bullet tracer\" )", script );
		Assert.Contains( "Effects.Mine( \"My halo\" )", script );
		Assert.Equal( Same( design ), Same( Build( script, design ) ) );

		// Formatted and minified alike
		Assert.Equal( script, Formatter.Format( script ) );
		Assert.Equal( Same( design ), Same( Build( Minifier.Minify( script ), design ) ) );
	}

	[Fact]
	public void AnEmptyDesignIsAnEmptyScript()
	{
		Assert.Equal( "", ScriptCodegen.Weapon( new WeaponDesign { Weapon = Weapon } ) );
		Assert.True( Build( "-- nothing yet" ).IsEmpty );
	}

	[Theory]
	[InlineData( "for i = 1, 9 do\n\tWeapon.AddSticker( Shapes.Star5 )\nend", "at most 8 stickers" )]
	[InlineData( "for i = 1, 9 do\n\tWeapon.AddProp( \"models/sbox_props/cola_can/cola_can.vmdl\" )\nend", "at most 8 props" )]
	[InlineData( "Weapon.AddProp( \"not a model\" )", "isn't a model" )]
	[InlineData( "local a = Weapon.AddEffect( Effects.Preset( \"Bullet tracer\" ) )\nlocal b = Weapon.AddEffect( a )", "on the weapon already" )]
	[InlineData( "Weapon.AddEffect( Effects.Preset( \"Dark aura\" ) )\nWeapon.AddEffect( Effects.Preset( \"Fire hands\" ) )\nWeapon.AddEffect( Effects.Preset( \"Electric body\" ) )", "layers between them" )]
	[InlineData( "Weapon.HidePart( \"\" )", "isn't a part's name" )]
	public void TheCapsHold( string source, string expected )
	{
		var build = WeaponScripts.Build( source, null, new ScriptCache(), ExampleRunner.Library );
		Assert.False( build.Succeeded );
		Assert.Contains( expected, build.Error.Message );
	}

	[Fact]
	public void BuiltSizesAreHeldToTheirBounds()
	{
		var design = Build( "local star = Weapon.AddSticker( Shapes.Star5 )\nstar.Size = 900\nSkin.Speed = -5\nlocal can = Weapon.AddProp( \"models/sbox_props/cola_can/cola_can.vmdl\" )\ncan.Scale = 100\ncan.Position = Vector3( 1000, 0, 0 )" );
		Assert.Equal( WeaponSticker.MaxSize, design.Stickers[ 0 ].Size );
		Assert.Equal( WeaponProp.MostScale, design.Props[ 0 ].Scale );
		Assert.Equal( WeaponProp.MostOffset, design.Props[ 0 ].Forward );
	}

	[Theory]
	[InlineData( "Weapon.Finish = Finish.Gold", ScriptKind.Effect, "is for weapon design scripts" )]
	[InlineData( "local glow = Sprite()", ScriptKind.Weapon, "in a weapon design, put one on with Weapon.AddEffect" )]
	public void WeaponNamesBelongToWeaponScripts( string source, ScriptKind kind, string expected ) => Assert.Contains( expected, ScriptKit.Error( source, kind ) );

	// ----- The weapon panel's changes, made in place -----

	private static (string Edited, WeaponDesign Built) Edit( string source, System.Action<WeaponDesign> change )
	{
		var carried = Rich();
		var build = WeaponScripts.Build( source, carried, new ScriptCache(), ExampleRunner.Library );
		Assert.True( build.Succeeded, build.Error?.ToString() );
		var before = JsonSerializer.Deserialize<WeaponDesign>( JsonSerializer.Serialize( build.Design ) );
		before.Weapon = Weapon;
		var after = JsonSerializer.Deserialize<WeaponDesign>( JsonSerializer.Serialize( before ) );
		change( after );
		var result = ScriptEdit.ApplyWeapon( source, build, before, after );
		Assert.True( result.Succeeded, result.Problem );
		var rebuilt = Build( result.Source, carried );
		Assert.Equal( Same( after ), Same( rebuilt ) );
		return (result.Source, rebuilt);
	}

	private static string Commented => "-- Mine, keep this\n" + ScriptCodegen.Weapon( Rich() );

	[Fact]
	public void AFieldChangedIsRewrittenInPlace()
	{
		var (edited, _) = Edit( Commented, d => { d.Finish = "Chrome"; d.Stickers[ 1 ].Size = 6; d.Props[ 0 ].Scale = 0.5f; d.Skin.Speed = 3; } );
		Assert.Contains( "-- Mine, keep this", edited );
		Assert.Contains( "Weapon.Finish = Finish.Chrome", edited );
	}

	[Fact]
	public void AFieldNotWrittenYetIsAddedAfterItsOwnersLines() => Edit( Commented, d => { d.Stickers[ 1 ].Border = true; d.Props[ 1 ].Part = "slide"; } );

	[Fact]
	public void ATintTakenOffLosesItsLine()
	{
		var (edited, built) = Edit( Commented, d => d.Tint = null );
		Assert.DoesNotContain( "Weapon.Tint", edited );
		Assert.Null( built.Tint );
	}

	[Fact]
	public void StickersPropsPartsAndEffectsAreAddedAndTakenOff()
	{
		Edit( Commented, d => d.Stickers.Add( new WeaponSticker { Picture = "Diamond", Size = 2 } ) );
		Edit( Commented, d => d.Stickers.RemoveAt( 0 ) );
		Edit( Commented, d => d.Props.Add( new WeaponProp { Model = "models/citizen_props/hotdog01.vmdl", Scale = 0.3f } ) );
		Edit( Commented, d => d.Props.RemoveAt( 1 ) );
		Edit( Commented, d => d.HiddenParts.Add( "sights" ) );
		Edit( Commented, d => d.HiddenParts.Clear() );
		Edit( Commented, d => d.Effects.RemoveAt( 0 ) );
		Edit( Commented, d =>
		{
			var flash = EffectPresets.Copy( EffectPresets.All.First( x => x.Name == "Muzzle flash" ), d.Effects );
			flash.Anchor.Kind = nameof( AnchorKind.HeldWeapon );
			d.Effects.Add( flash );
		} );
	}

	[Fact]
	public void AnEffectsSlidersAndWhenAreChangedInPlace() => Edit( Commented, d =>
	{
		d.Effects[ 0 ].Size = 0.5f;
		WeaponDesign.ShowWhen( d.Effects[ 0 ], nameof( LayerWhen.OnHit ) );
	} );

	[Fact]
	public void AFieldWorkedOutByCodeIsReportedWithItsLine()
	{
		const string source = "local size = 2\nfor i = 1, 2 do\n\tsize = size + 1\nend\nlocal star = Weapon.AddSticker( Shapes.Star5 )\nstar.Size = size + 1";
		var build = WeaponScripts.Build( source, null, new ScriptCache(), ExampleRunner.Library );
		var before = build.Design;
		before.Weapon = Weapon;
		var after = JsonSerializer.Deserialize<WeaponDesign>( JsonSerializer.Serialize( before ) );
		after.Stickers[ 0 ].Size = 9;
		var result = ScriptEdit.ApplyWeapon( source, build, before, after );
		Assert.False( result.Succeeded );
		Assert.Equal( 6, result.Line );
	}

	// ----- Kept with the design -----

	[Fact]
	public void ADesignKeepsItsScriptWithinTheCap()
	{
		var design = Rich();
		design.Script = Minifier.Minify( ScriptCodegen.Weapon( design ) );
		Assert.Equal( design.Script, design.Sanitized().Script );

		design.Script = new string( 'x', ScriptLimits.MaxScriptBytes + 1 );
		Assert.Null( design.Sanitized().Script );
	}

	[Fact]
	public void ALooksScriptsAreHeldTogether()
	{
		var look = new PlayerLook();
		for ( var i = 0; i < 3; i++ )
		{
			var design = Rich();
			design.Weapon = $"models/weapons/w_{i}.vmdl";
			design.Effects = null;
			design.Script = "-- " + new string( 'x', ScriptLimits.MaxScriptBytes - 10 );
			look.Weapons.Add( design );
		}

		var kept = look.Sanitized().Weapons.Count( x => x.Script is not null );
		Assert.Equal( ScriptLimits.MaxLookBytes / ScriptLimits.MaxScriptBytes, kept );
	}
}

/// <summary>Completion in a weapon design script: its fields, and the held weapon's parts and the stock props in strings.</summary>
public class WeaponCompletionTests
{
	private sealed class Held : IPlayerLibrary
	{
		public IReadOnlyList<LibraryItem> Items { get; } = new[]
		{
			new LibraryItem( "magazine", LibraryItemKind.Part, "" ), new LibraryItem( "rail", LibraryItemKind.Part, "" ),
			new LibraryItem( "models/sbox_props/cola_can/cola_can.vmdl", LibraryItemKind.Prop, "" ),
		};

		public Effect Effect( string name ) => null;
		public DrawnShape Shape( string name, LibraryItemKind kind ) => null;
	}

	private static List<string> At( string source ) =>
		ScriptCompletion.At( source, source.Length, ScriptKind.Weapon, new Held() ).Select( x => x.Label ).ToList();

	[Fact]
	public void TheWeaponsFieldsAreOffered()
	{
		Assert.Contains( "Finish", At( "Weapon." ) );
		Assert.Contains( "AddSticker", At( "Weapon." ) );
		Assert.Contains( "Animation", At( "Skin." ) );
		Assert.Contains( "Scrape", At( "local star = Weapon.AddSticker( Shapes.Star5 )\nstar." ) );
		Assert.Contains( "Replaces", At( "local can = Weapon.AddProp( \"x.vmdl\" )\ncan." ) );
	}

	[Fact]
	public void PartsAndPropsAreOfferedInTheirStrings()
	{
		Assert.Equal( new[] { "magazine", "rail" }, At( "Weapon.HidePart( \"" ) );
		Assert.Equal( new[] { "magazine" }, At( "local can = Weapon.AddProp( \"x.vmdl\" )\ncan.Part = \"mag" ) );
		Assert.Contains( "models/sbox_props/cola_can/cola_can.vmdl", At( "Weapon.AddProp( \"" ) );
	}
}

/// <summary>The weapon panel's change to a design with a script, while its code isn't open, is written into the script.</summary>
public class WeaponScriptFollowTests
{
	[Fact]
	public void APanelChangeIsFollowedInTheScript()
	{
		var design = WeaponScriptTests.Rich();
		design.Script = Minifier.Minify( "-- kept\n" + ScriptCodegen.Weapon( design ) );
		var after = JsonSerializer.Deserialize<WeaponDesign>( JsonSerializer.Serialize( design ) );
		after.Stickers[ 0 ].Size = 7;
		after.Finish = "Copper";
		var script = WeaponScripts.Follow( design.Script, design, after, ExampleRunner.Library );
		Assert.NotNull( script );
		Assert.Contains( "-- kept", script );
		var built = WeaponScripts.Build( script, after, new ScriptCache(), ExampleRunner.Library ).Design;
		Assert.Equal( 7, built.Stickers[ 0 ].Size );
		Assert.Equal( "Copper", built.Finish );
	}

	[Fact]
	public void AChangeOnlyCodeCanMakeLetsTheScriptGo()
	{
		var design = new WeaponDesign { Weapon = "models/weapons/w.vmdl", Script = "local size = 2\nlocal star = Weapon.AddSticker( Shapes.Star5 )\nstar.Size = size * 2" };
		design.Stickers = new List<WeaponSticker> { new() { Picture = "Star5", Size = 4 } };
		var after = JsonSerializer.Deserialize<WeaponDesign>( JsonSerializer.Serialize( design ) );
		after.Stickers[ 0 ].Size = 9;
		Assert.Null( WeaponScripts.Follow( design.Script, design, after, ExampleRunner.Library ) );
	}
}
