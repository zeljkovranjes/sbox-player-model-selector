using System.Collections.Generic;
using System.Linq;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>Shape, sticker and emblem scripts, drawn shapes in effects, and the player's own shapes by name.</summary>
public class ShapeTests
{
	[Fact]
	public void AShapeScriptDrawsItsShapeOnce()
	{
		var (_, build) = ScriptKit.Build( "for k = 0, 99 do\n\tlocal a = k / 100 * Math.PI * 2\n\tshape.Point( Math.Cos( a ), Math.Sin( a ) )\nend\nshape.EndOutline()\nshape.Point( 0, 0 )\nshape.Point( 0.5, 0.5 )", ScriptKind.Shape );
		var strokes = build.Shape.Decode();
		Assert.Equal( new[] { (true, 100), (false, 2) }, strokes.Select( x => (x.Closed, x.Count) ) );
		Assert.Equal( 102, build.Shape.PointCount );
	}

	[Fact]
	public void TextLettersOtherShapesAndFitting()
	{
		var (_, build) = ScriptKit.Build( "shape.Letter( \"A\", 0, 0, 1, 90 )\nshape.Add( Shapes.Star5, 0.5, 0.5, 0.25 )\nshape.Fit()", ScriptKind.Shape );
		Assert.True( build.Shape.PointCount > 10 );
		var points = build.Shape.Decode().SelectMany( x => x.Points ).ToList();
		Assert.InRange( points.Max(), 0.85f, 0.95f );
		Assert.Empty( ScriptKit.Build( "local glow = Sprite()\nlocal t = Shapes.Text( \"HI\" )\nglow.ComesFrom = ComesFrom.Shape\nglow.Formation = t" ).Build.Effect.Shapes.Where( x => x.PointCount == 0 ) );
	}

	[Fact]
	public void StickerFieldsMakeAStickerAndCodegenGivesItBack()
	{
		const string source = "shape.Points( { -1, -1, 1, -1, 0, 1 } )\nshape.EndOutline()\nshape.FillColor = Color.Red\nshape.OutlineWidth = 0.1\nshape.Animation = Animation.Rain\nshape.Palette = { Color.Red, Color.Blue }";
		var shape = ScriptKit.Build( source, ScriptKind.Shape ).Build.Shape;
		Assert.Equal( ("#ff0000", 0.1f, "Rain"), (shape.Sticker.Fill, shape.Sticker.OutlineWidth, shape.Sticker.Animation) );
		var again = ScriptKit.Build( ScriptCodegen.Shape( shape ), ScriptKind.Shape ).Build.Shape;
		Assert.Equal( shape.Content, again.Content );
	}

	[Fact]
	public void EmblemLayersKeepTheirFieldsAndOutlineTheShape()
	{
		const string source = "local back = shape.AddLayer( Shapes.Hexagon )\nback.Color = Color.Black\nlocal star = shape.AddLayer( Shapes.Star5 )\nstar.Position = Vector3( 0.2, -0.1, 0 )\nstar.Scale = Vector3( 0.5, 9, 1 )\nstar.Rotation = 45\nstar.FlipX = true\nstar.Opacity = 0.5\nstar.Animation = Animation.Rainbow\nlocal hidden = shape.AddLayer( Shapes.Heart )\nhidden.Hidden = true";
		var build = ScriptKit.Build( source, ScriptKind.Shape ).Build;
		Assert.Equal( new[] { "Hexagon", "Star5", "Heart" }, build.Emblem.Select( x => x.Shape ) );
		var star = build.Emblem[ 1 ];
		Assert.Equal( (0.2f, -0.1f, 0.5f, 4f, 45f, true, 0.5f, "Rainbow"), (star.X, star.Y, star.ScaleX, star.ScaleY, star.Rotation, star.FlipX, star.Opacity, star.Animation) );
		Assert.Equal( "#000000", build.Emblem[ 0 ].Color );
		Assert.True( build.Shape.PointCount > 0 );
	}

	[Fact]
	public void DrawnShapesTravelWithTheEffectAndRoundTrip()
	{
		var (_, build) = ScriptKit.Build( "local cool = Shapes.Text( \"COOL\" )\nlocal glow = Sprite()\nglow.ComesFrom = ComesFrom.Shape\nglow.Formation = cool\nglow.LooksLike = cool" );
		var effect = build.Effect;
		var shape = Assert.Single( effect.Shapes );
		Assert.Equal( shape.Reference, effect.Layers[ 0 ].Formation );
		Assert.Equal( shape.Reference, effect.Layers[ 0 ].Picture );
		ScriptKit.SameEffect( effect, ScriptKit.BuildEffect( ScriptCodegen.Effect( effect ) ) );
	}

	[Fact]
	public void ThePlayersOwnAreCopiedWhenBuilt()
	{
		var library = new TestLibrary();
		var effect = ScriptKit.BuildEffect( "local skull = Stickers.Mine( \"Gold skull\" )\nlocal glow = Sprite()\nglow.LooksLike = skull", library );
		var expected = library.Shape( "Gold skull", LibraryItemKind.Sticker );
		Assert.Equal( expected.Content, Assert.Single( effect.Shapes ).Content );
		Assert.Equal( "Gold skull", effect.Shapes[ 0 ].Name );
	}

	[Theory]
	[InlineData( "local s = Stickers.Mine( \"Gold skul\" )", "No sticker called 'Gold skul' in My shapes — did you mean 'Gold skull'? Make it in the shape creator or get it from the Workshop" )]
	[InlineData( "local s = Stickers.Mine( \"COOL\" )", "No sticker called 'COOL' in My shapes" )]
	[InlineData( "local s = Emblems.Mine( \"Clan2\" )", "No emblem called 'Clan2' in My shapes — did you mean 'Clan'?" )]
	[InlineData( "local n = \"COOL\"\nlocal s = Shapes.Mine( n )", "Name it in quotes, like Shapes.Mine( \"name\" )" )]
	[InlineData( "Effect.Include( Effects.Mine( \"Fire hand\" ) )", "No effect called 'Fire hand' in My effects — did you mean 'Fire hands'? Save it from the Effects tab" )]
	public void UnknownNamesAreCompileErrors( string source, string message ) => Assert.Contains( message, ScriptKit.Error( source + "\nlocal glow = Sprite()", ScriptKind.Effect, new TestLibrary() ) );

	[Fact]
	public void WithoutALibraryThePlayersOwnDontExist() => Assert.Contains( "No shape called 'COOL'", ScriptKit.Error( "local s = Shapes.Mine( \"COOL\" )" ) );

	[Fact]
	public void IncludedEffectsBringTheirLayers()
	{
		var effect = ScriptKit.BuildEffect( "Effect.Include( Effects.Preset( \"Hearts\" ) )\nEffect.Include( Effects.Mine( \"Fire hands\" ) )", new TestLibrary() );
		Assert.Equal( 1 + 6, effect.Layers.Count );
		Assert.Equal( "Chest", effect.Layers[ 0 ].Joint );
	}

	[Theory]
	[InlineData( 1f, 0f, 0.352f )]
	[InlineData( 0f, 1f, 1f )]
	[InlineData( -1f, 0f, 1f )]
	[InlineData( 0.5f, -0.866f, 0f )]
	[InlineData( 0.866f, -0.5f, 0.104f )]
	[InlineData( 0.9659f, 0.2588f, 0.5f )]
	[InlineData( -0.5f, -0.866f, 0f )]
	public void AFadedPictureGoesFromNothingAtItsTailToWholeAtItsFront( float x, float y, float shown )
	{
		// From -60 degrees (the tail) round anticlockwise to 90 (the front, straight up)
		var shape = ScriptKit.Build( "shape.Add( Shapes.Crescent )\nshape.Fade( -60, 90 )", ScriptKind.Shape ).Build.Shape;
		Assert.Equal( (300f, 150f), (shape.FadeFrom, shape.FadeArc) );
		Assert.Equal( shown, shape.FadeAt( x, y ), 2 );
	}

	[Fact]
	public void AFadeCanGoClockwiseAndIsKeptWithinATurn()
	{
		var clockwise = ScriptKit.Build( "shape.Add( Shapes.Crescent )\nshape.Fade( 90, -60 )", ScriptKind.Shape ).Build.Shape;
		Assert.Equal( (90f, -150f), (clockwise.FadeFrom, clockwise.FadeArc) );
		Assert.Equal( 0f, clockwise.FadeAt( 0f, 1f ), 3 );
		Assert.Equal( 1f, clockwise.FadeAt( 0.5f, -0.866f ), 2 );
		Assert.Equal( 360f, new DrawnShape { Strokes = clockwise.Strokes, FadeFrom = 5, FadeArc = 9000 }.Sanitized().FadeArc );
		Assert.Equal( 0f, new DrawnShape { Strokes = clockwise.Strokes, FadeFrom = float.NaN, FadeArc = 30 }.Sanitized().FadeArc );
	}

	[Fact]
	public void AFadeIsPartOfWhatAShapeIsAndCodegenGivesItBack()
	{
		var plain = ScriptKit.Build( "shape.Add( Shapes.Crescent )", ScriptKind.Shape ).Build.Shape;
		var faded = ScriptKit.Build( "shape.Add( Shapes.Crescent )\nshape.Fade( -60, 90 )", ScriptKind.Shape ).Build.Shape;
		Assert.NotEqual( plain.Id, faded.Id );
		Assert.Equal( plain.Id, plain.Faded( 10, 10 ).Id );
		Assert.Equal( faded.Content, ScriptKit.Build( ScriptCodegen.Shape( faded ), ScriptKind.Shape ).Build.Shape.Content );
		Assert.Equal( faded.Content, faded.Named( "Swoosh" ).Content );

		var effect = ScriptKit.BuildEffect( "local swoosh = Shapes.New( \"Swoosh\" )\nswoosh.Add( Shapes.Crescent )\nswoosh.Fade( -60, 90 )\nlocal slash = Sprite()\nslash.LooksLike = swoosh" );
		Assert.Equal( 150f, effect.Shapes[ 0 ].FadeArc );
		ScriptKit.SameEffect( effect, ScriptKit.BuildEffect( ScriptCodegen.Effect( effect ) ) );
	}
}

/// <summary>The machine running an effect: events and their inputs, particle functions, live layer changes and their limits.</summary>
public class MachineTests
{
	private const string Script = """
		local glow = Sprite()
		local lamp = Light()
		local seen = 0
		local lastKey: Key = "a"
		local lastFoot = Joint.Head

		function OnLand( fallSpeed )
			seen = fallSpeed
		end

		function OnFootstep( foot )
			lastFoot = foot
		end

		function OnKey( key )
			lastKey = key
		end

		function OnShoot( shot )
			if shot.Hit then
				seen = shot.Distance + shot.HitPosition.Z
			end
		end

		function OnUpdate( dt )
			glow.Size = 1000
			lamp.Size = 1000
			glow.Color = Color( 2, -1, 0.5 )
		end

		function glow.Particle( p )
			p.Alpha = 1 - p.Life
			p.Color = Color.FromHsv( p.Index * 90, 1, 1 )
		end

		local function sneaky()
			glow.Solid = true
		end

		function OnJump()
			sneaky()
		end
		""";

	[Fact]
	public void EventsReadTheirInputs()
	{
		var (machine, _) = ScriptKit.Build( Script );
		var io = new ScriptIO();
		io.Set( ScriptInput.Amount, 312 );
		Assert.True( machine.RunEvent( ScriptEvent.Land, io ) );
		Assert.Equal( "312", machine.Variable( "seen" ) );
		io.Set( ScriptInput.Foot, (int)Effect.Joint.RightFoot );
		Assert.True( machine.RunEvent( ScriptEvent.Footstep, io ) );
		Assert.Equal( "Joint.RightFoot", machine.Variable( "lastFoot" ) );
		io.Set( ScriptInput.Key, Compiler.KeyIndex( "f3" ) );
		Assert.True( machine.RunEvent( ScriptEvent.Key, io ) );
		Assert.Equal( "\"f3\"", machine.Variable( "lastKey" ) );
		io.Set( ScriptInput.ShotHit, 1 );
		io.Set( ScriptInput.ShotDistance, 100 );
		io.Set( ScriptInput.ShotHitPositionX, 1, 2, 3 );
		Assert.True( machine.RunEvent( ScriptEvent.Shoot, io ) );
		Assert.Equal( "103", machine.Variable( "seen" ) );
		Assert.True( machine.RunEvent( ScriptEvent.Death, io ) );
		Assert.False( machine.Program.Handles( ScriptEvent.Death ) );
	}

	[Fact]
	public void LiveChangesAreHeldToEachKindsRangeAndFlagged()
	{
		var (machine, _) = ScriptKit.Build( Script );
		Assert.False( machine.IsLiveChanged( 0, PropertyId.LayerSize ) );
		Assert.True( machine.RunEvent( ScriptEvent.Update, new ScriptIO() ) );
		Assert.Equal( EffectLayer.MaxSize, machine.LiveValue( 0, PropertyId.LayerSize ) );
		Assert.Equal( EffectLayer.MaxReach, machine.LiveValue( 1, PropertyId.LayerSize ) );
		Assert.Equal( (1.0, 0.0, 0.5), (machine.LiveValue( 0, PropertyId.LayerColor, 0 ), machine.LiveValue( 0, PropertyId.LayerColor, 1 ), machine.LiveValue( 0, PropertyId.LayerColor, 2 )) );
		Assert.True( machine.IsLiveChanged( 0, PropertyId.LayerSize ) && machine.IsLiveChanged( 0, PropertyId.LayerColor ) );
		Assert.False( machine.IsLiveChanged( 0, PropertyId.LayerAmount ) );
		machine.ClearLiveChanges();
		Assert.False( machine.IsLiveChanged( 0, PropertyId.LayerSize ) );
	}

	[Fact]
	public void ParticleFunctionsReadTheParticleAndWriteItsLook()
	{
		var (machine, _) = ScriptKit.Build( Script );
		Assert.True( machine.HasParticleFunction( 0 ) );
		Assert.False( machine.HasParticleFunction( 1 ) );
		var io = new ScriptIO();
		io.Set( ScriptInput.Index, 1 );
		io.Set( ScriptInput.Life, 0.25 );
		Assert.True( machine.RunParticle( 0, io ) );
		Assert.Equal( 0.75, io.Get( ScriptOutput.Alpha ) );
		Assert.Equal( "#80ff00", ScriptValue.ColorText( io.Outputs[ 0 ], io.Outputs[ 1 ], io.Outputs[ 2 ], 1 ) );
		Assert.Equal( ScriptIO.ColorSet | ScriptIO.AlphaSet, io.OutputsSet );
	}

	[Fact]
	public void BuildOnlyChangesFromAHelperStopTheRun()
	{
		var (machine, _) = ScriptKit.Build( Script );
		Assert.False( machine.RunEvent( ScriptEvent.Jump, new ScriptIO() ) );
		Assert.Equal( FaultKind.NotLive, machine.Fault.Kind );
		Assert.Equal( 37, machine.Fault.Describe( machine.Program ).Line );
	}

	[Fact]
	public void ShapesAreOnlyMadeWhileBuilding()
	{
		var (machine, _) = ScriptKit.Build( "local glow = Sprite()\nlocal function more()\n\tlocal l = Sprite()\nend\nfunction OnJump()\n\tmore()\nend" );
		Assert.False( machine.RunEvent( ScriptEvent.Jump, new ScriptIO() ) );
		Assert.Equal( FaultKind.BuildOnly, machine.Fault.Kind );
	}

	[Fact]
	public void AStickersPaletteGivesItsHighlightAndRest()
	{
		var machine = new ScriptMachine( ScriptKit.Compile( "local h = highlight\nlocal r = rest\nlocal n = palette.Count", ScriptKind.Sticker ) );
		machine.SetPalette( new[] { "#010203", "#040506", "#070809" } );
		Assert.True( machine.RunElement( new ScriptIO() ) );
		Assert.Equal( ("#010203", "#040506", "3"), (machine.Variable( "h" ), machine.Variable( "r" ), machine.Variable( "n" )) );
		machine.SetPalette( new[] { "#010203" } );
		Assert.True( machine.RunElement( new ScriptIO() ) );
		Assert.Equal( ("#ff3333", "#ffffff", "1"), (machine.Variable( "h" ), machine.Variable( "r" ), machine.Variable( "n" )) );
	}
}
