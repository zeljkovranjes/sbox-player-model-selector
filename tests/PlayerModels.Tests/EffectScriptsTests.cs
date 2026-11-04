using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using PlayerModels.Tests.StressEffects;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>
/// Scripts riding a look: how an effect keeps its script and parameters (held to their sizes on the host, by length
/// only), how each client builds a look's effects again from their scripts (and falls back to what the look kept), the
/// compile cache and per-player compile budget, and keeping a scripted effect and its script in step both ways.
/// </summary>
public class EffectScriptsTests
{
	private const string Glow = """
		[Parameter( "Glow size", Min = 0.5, Max = 10 )]
		local glowSize = 2

		Effect.Name = "Glow"
		local glow = Sprite()
		glow.Size = glowSize
		glow.Color = Color.Parse( "#ff8800" )
		""";

	// An effect as the code editor saves it: built from its script, with the script minified
	private static Effect Saved( string source, Dictionary<string, string> parameters = null )
	{
		var machine = new ScriptMachine( ScriptKit.Compile( source ) );
		machine.SetParameters( parameters );
		var effect = machine.Build().Effect;
		effect.Script = Minifier.Minify( source );
		effect.Parameters = parameters;
		effect.Id = "e1";
		return effect.Sanitized();
	}

	private static PlayerLook Wearing( params Effect[] effects )
	{
		var look = new PlayerLook();
		foreach ( var effect in effects )
		{
			effect.Id = Effect.NewId( look.Effects );
			Assert.True( look.Adopt( effect ) );
		}

		return look;
	}

	private static ScriptBuild Build( Effect effect ) => EffectScripts.Build( effect, new ScriptCache(), new LookLibrary( Array.Empty<DrawnShape>() ) );

	[Fact]
	public void AnEffectKeepsItsScriptAndParametersThroughTheLook()
	{
		var look = Wearing( Saved( Glow, new Dictionary<string, string> { ["glowSize"] = "4" } ) );
		var read = PlayerLook.Parse( look.Serialize() );
		Assert.Equal( Minifier.Minify( Glow ), read.Effects[ 0 ].Script );
		Assert.Equal( "4", read.Effects[ 0 ].Parameters[ "glowSize" ] );
		Assert.Equal( 4f, read.Effects[ 0 ].Layers[ 0 ].Size );
	}

	[Fact]
	public void AScriptOverTheCapIsDroppedAndTheEffectKeepsItsLayers()
	{
		var effect = Saved( Glow );
		effect.Script = "-- " + new string( 'x', ScriptLimits.MaxScriptBytes );
		var kept = effect.Sanitized();
		Assert.Null( kept.Script );
		Assert.Single( kept.Layers );
	}

	[Fact]
	public void ScriptsPastTheLookCapAreDroppedInOrder()
	{
		var big = "-- " + new string( 'x', 12 * 1024 ) + "\nlocal glow = Sprite()";
		var effects = Enumerable.Range( 0, 3 ).Select( _ =>
		{
			var effect = Saved( Glow );
			effect.Script = big;
			return effect;
		} ).ToArray();

		var read = PlayerLook.Parse( Wearing( effects ).Serialize() );
		Assert.Equal( new[] { true, true, false }, read.Effects.Select( x => x.Script is not null ) );
		Assert.All( read.Effects, x => Assert.NotEmpty( x.Layers ) );
	}

	[Fact]
	public void ParametersAreHeldToTheirSizesOnly()
	{
		var effect = Saved( Glow );
		effect.Parameters = Enumerable.Range( 0, 40 ).ToDictionary( x => "p" + x, x => x.ToString() );
		effect.Parameters[ "long" ] = new string( '1', Effect.MaxParameterLength + 1 );
		var kept = effect.Sanitized();
		Assert.Equal( ScriptParameter.MaxPerScript, kept.Parameters.Count );
		Assert.DoesNotContain( "long", kept.Parameters.Keys );
	}

	[Fact]
	public void KeyActionsReadAndUnknownOnesFire()
	{
		var effect = Saved( Glow );
		effect.Key = "g";
		effect.KeyMode = "Hold";
		Assert.Equal( Effect.KeyAction.Hold, effect.Sanitized().KeyActionIs );
		effect.KeyMode = "Spam";
		Assert.Null( effect.Sanitized().KeyMode );
		Assert.Equal( Effect.KeyAction.Fire, effect.Sanitized().KeyActionIs );
	}

	[Fact]
	public void AClientBuildsTheScriptNotWhatTheLookClaims()
	{
		var effect = Saved( Glow );
		effect.Layers[ 0 ].Size = 40;
		effect.Layers.Add( new EffectLayer { Kind = "Light" } );
		var shown = EffectScripts.WithScriptsBuilt( Wearing( effect ), Build, out var pending );
		Assert.False( pending );
		Assert.Single( shown.Effects[ 0 ].Layers );
		Assert.Equal( 2f, shown.Effects[ 0 ].Layers[ 0 ].Size );
		Assert.Equal( 0, shown.Effects[ 0 ].Layers[ 0 ].ScriptLayer );
		Assert.NotNull( shown.Effects[ 0 ].Script );
	}

	[Fact]
	public void ScriptedLayersAreAllOnForTheBudgetAndKeepTheirPlaceThroughBudgetTrims()
	{
		var effect = Saved( "local a = Sprite()\na.Off = true\nlocal b = Light()\nlocal c = Sprite()\nc.Amount = 90\nc.Lifetime = 5" );
		var shown = EffectScripts.WithScriptsBuilt( Wearing( effect ), Build, out _ );
		Assert.All( shown.Effects[ 0 ].Layers, x => Assert.False( x.Off ) );

		var trimmed = shown.WithinBudget( new EffectBudget( 2, 0, 100 ) );
		Assert.Equal( new[] { 0, 2 }, trimmed.Effects[ 0 ].Layers.Select( x => x.ScriptLayer ) );
	}

	[Fact]
	public void AScriptThatFailsShowsWhatTheLookKeptWithoutItsScript()
	{
		var effect = Saved( Glow );
		effect.Script = "local glow = Sprite(\n";
		var shown = EffectScripts.WithScriptsBuilt( Wearing( effect ), Build, out _ );
		Assert.Null( shown.Effects[ 0 ].Script );
		Assert.Equal( 2f, shown.Effects[ 0 ].Layers[ 0 ].Size );
	}

	[Fact]
	public void AScriptWaitingItsTurnShowsWhatTheLookKeptAndSaysToAskAgain()
	{
		var shown = EffectScripts.WithScriptsBuilt( Wearing( Saved( Glow ) ), _ => null, out var pending );
		Assert.True( pending );
		Assert.Null( shown.Effects[ 0 ].Script );
	}

	[Fact]
	public void ScriptsOffOrTooLongForTheHostShowWhatTheyBuilt()
	{
		var look = Wearing( Saved( Glow ) );
		Assert.Null( EffectScripts.WithoutScripts( look, 0 ).Effects[ 0 ].Script );
		Assert.Same( look, EffectScripts.WithoutScripts( look, ScriptLimits.MaxScriptBytes ) );
	}

	[Fact]
	public void ShapesAScriptMakesRideTheShownLook()
	{
		var effect = Saved( "local ring = Shapes.New( \"Ring\" )\nring.Add( Shapes.Heart, 0, 0, 0.5 )\nlocal s = Sprite()\ns.ComesFrom = ComesFrom.Shape\ns.Formation = ring" );
		var shown = EffectScripts.WithScriptsBuilt( Wearing( effect ), Build, out _ );
		var id = shown.Effects[ 0 ].Layers[ 0 ].DrawnFormation;
		Assert.NotNull( id );
		Assert.Contains( shown.DrawnShapes, x => x.Id == id );
	}

	[Fact]
	public void MyShapesComeFromWhatTheLookCarries()
	{
		var heart = DrawnShape.From( HersheyFont.Strokes( "L" ), "Love" );
		var library = new LookLibrary( new[] { heart } );
		Assert.Contains( library.Items, x => x.Name == "Love" && x.Kind == LibraryItemKind.Shape );
		var compiled = new ScriptCache().Compile( "local s = Sprite()\ns.ComesFrom = ComesFrom.Shape\ns.Formation = Shapes.Mine( \"Love\" )", ScriptKind.Effect, library );
		Assert.True( compiled.Succeeded, string.Join( "\n", compiled.Errors ) );
		Assert.False( new ScriptCache().Compile( "local s = Shapes.Mine( \"Hate\" )", ScriptKind.Effect, library ).Succeeded );
	}

	[Fact]
	public void TheCacheCompilesEachScriptOnceAndKeepsFailures()
	{
		var cache = new ScriptCache();
		var first = cache.Compile( Glow );
		Assert.True( cache.Has( Glow ) );
		Assert.Same( first, cache.Compile( Glow ) );
		Assert.Null( first.Tree );

		var bad = cache.Compile( "local x = (" );
		Assert.False( bad.Succeeded );
		Assert.Same( bad, cache.Compile( "local x = (" ) );
	}

	[Fact]
	public void TheCacheLetsGoOfTheLeastRecentlyUsed()
	{
		var cache = new ScriptCache();
		cache.Compile( "local kept = 1" );
		for ( var i = 0; i < ScriptCache.MostKept; i++ )
		{
			cache.Compile( "local kept = 1" );
			cache.Compile( $"local x{i} = {i}" );
		}

		Assert.Equal( ScriptCache.MostKept, cache.Count );
		Assert.True( cache.Has( "local kept = 1" ) );
		Assert.False( cache.Has( "local x0 = 0" ) );
	}

	[Fact]
	public void TheCacheKeysOnTheLibrarysNames()
	{
		var cache = new ScriptCache();
		const string source = "local s = Shapes.Mine( \"Love\" )";
		Assert.False( cache.Compile( source ).Succeeded );
		var heart = DrawnShape.From( HersheyFont.Strokes( "L" ), "Love" );
		Assert.True( cache.Compile( source, ScriptKind.Effect, new LookLibrary( new[] { heart } ) ).Succeeded );
	}

	[Fact]
	public void EachPlayerCompilesAtMostTheirBudget()
	{
		var budget = new CompileBudget();
		var (one, two) = (Guid.NewGuid(), Guid.NewGuid());
		var taken = 0;
		for ( var i = 0; i < 100; i++ )
			taken += budget.TryTake( one, 10.0 ) ? 1 : 0;

		Assert.Equal( (int)CompileBudget.Burst, taken );
		Assert.True( budget.TryTake( two, 10.0 ) );
		Assert.False( budget.TryTake( one, 10.1 ) );
		Assert.True( budget.TryTake( one, 10.0 + 1 / CompileBudget.PerSecond ) );
	}

	[Fact]
	public void AVisualEditRewritesTheLiteralInPlace()
	{
		var effect = Saved( "local glow = Sprite()\n-- the colour\nglow.Color = Color( 1, 0.5, 0 )\nglow.Amount = 10" );
		effect.Layers[ 0 ].Amount = 25;
		effect.Layers[ 0 ].Color = "#00ff00";
		var result = ScriptSync.Reconcile( Formatter.Format( effect.Script ), effect );
		Assert.True( result.Succeeded, result.Problem );
		Assert.True( result.Source.Contains( "glow.Amount = 25" ), result.Source );
		Assert.True( result.Source.Contains( "glow.Color = Color( 0, 1, 0 )" ), result.Source );
		Assert.Contains( "-- the colour", result.Source );
		Assert.True( ScriptSync.Same( effect, ScriptKit.BuildEffect( result.Source ) ) );
	}

	[Fact]
	public void NothingChangedLeavesTheScriptAlone()
	{
		var effect = Saved( Glow );
		var build = new ScriptMachine( ScriptKit.Compile( effect.Script ) ).Build();
		Assert.Equal( effect.Script, ScriptSync.Reconcile( effect.Script, build, effect ).Source );
	}

	[Fact]
	public void AFieldTheScriptWorksOutIsRefusedWithItsLine()
	{
		var effect = Saved( "local glow = Sprite()\nfor k = 0, 1 do\n\tglow.Size = 3 + k\nend" );
		effect.Layers[ 0 ].Size = 9;
		var result = ScriptSync.Reconcile( Formatter.Format( effect.Script ), effect );
		Assert.False( result.Succeeded );
		Assert.Equal( 3, result.Line );
	}

	[Fact]
	public void RemovingALayerRemovesItsLinesAndMovingOneIsRefused()
	{
		var source = "local a = Sprite()\na.Size = 3\n\nlocal b = Light()\nb.Size = 50";
		var effect = Saved( source );
		var build = new ScriptMachine( ScriptKit.Compile( source ) ).Build();

		var moved = Saved( source );
		moved.Layers.Reverse();
		Assert.False( ScriptSync.Reconcile( source, build, moved ).Succeeded );

		effect.Layers.RemoveAt( 0 );
		var result = ScriptSync.Reconcile( source, build, effect );
		Assert.True( result.Succeeded, result.Problem );
		Assert.DoesNotContain( "a.Size", result.Source );
		Assert.True( ScriptSync.Same( effect, ScriptKit.BuildEffect( result.Source ) ) );
	}

	[Fact]
	public void AdoptGivesTheEffectWhatTheScriptBuiltKeepingWhatIsOutsideIt()
	{
		var effect = Saved( Glow );
		effect.KeyMode = "Toggle";
		effect.Layers[ 0 ].Size = 30;
		ScriptSync.Adopt( effect, ScriptKit.BuildEffect( Glow ) );
		Assert.Equal( 2f, effect.Layers[ 0 ].Size );
		Assert.Equal( "Toggle", effect.KeyMode );
		Assert.Equal( "e1", effect.Id );
	}

	private const string MagicCircle = """
		Effect.Name = "Magic circle"
		Effect.Joint = Joint.Ground

		-- Runes kept out all the time, spread evenly round a circle lying on the floor
		local runes = Sprite()
		runes.ComesFrom = ComesFrom.Shape
		runes.Formation = Shapes.Circle
		runes.Facing = Facing.Flat
		runes.Spread = 28
		runes.Offset = Vector3( 0, 0, 0.5 )
		runes.LooksLike = Picture.Sparkle
		runes.Align = Align.Ground
		runes.Color = Color.Parse( "#7ad8ff" )
		runes.Size = 4
		runes.Amount = 0
		runes.Keep = 16
		runes.Follow = true

		function runes.Particle( p )
			p.Rotation = t * 40 + p.Index * 22.5
			p.Alpha = 0.6 + 0.4 * Math.Sin( t * 3 + p.Index )
		end

		-- The ring itself, one sprite as wide as the circle, flat under the runes
		local ring = Sprite()
		ring.LooksLike = Shapes.Ring
		ring.Align = Align.Ground
		ring.Color = Color.Parse( "#7ad8ff" )
		ring.Size = 60
		ring.Amount = 0
		ring.Keep = 1
		ring.Follow = true

		local glow = Light()
		glow.Offset = Vector3( 0, 0, 4 )
		glow.Color = Color.Parse( "#7ad8ff" )
		glow.Size = 80
		glow.Amount = 0.6
		""";

	[Fact]
	public void TheMagicCircleLiesFlatOnTheGroundUnderTheFeet()
	{
		var shown = EffectScripts.WithScriptsBuilt( PlayerLook.Parse( Wearing( Saved( MagicCircle ) ).Serialize() ), Build, out _ );
		var effect = shown.Effects[ 0 ];
		Assert.Equal( nameof( Effect.Joint.Ground ), effect.Anchor.Joint );
		Assert.Equal( LayerFacing.Flat, effect.Layers[ 0 ].FacingIs );
		Assert.Equal( LayerAlign.Ground, effect.Layers[ 0 ].AlignIs );
		Assert.Equal( 16f, effect.Layers[ 0 ].Keep );
		Assert.Equal( LayerAlign.Ground, effect.Layers[ 1 ].AlignIs );
	}

	public static IEnumerable<object[]> Stress() => StressEffectTests.Names().Where( x => StressScripts.Named( (string)x[ 0 ] ).Kind == ScriptKind.Effect );

	[Theory]
	[MemberData( nameof( Stress ) )]
	public void EveryStressEffectRidesALookAndIsBuiltAgainOnAClient( string name )
	{
		var effect = Saved( StressScripts.Named( name ).Script );
		var read = PlayerLook.Parse( Wearing( effect ).Serialize() );
		var shown = EffectScripts.WithScriptsBuilt( read, Build, out var pending );
		Assert.False( pending );
		Assert.NotNull( shown.Effects[ 0 ].Script );
		Assert.Equal( Enumerable.Range( 0, shown.Effects[ 0 ].Layers.Count ), shown.Effects[ 0 ].Layers.Select( x => x.ScriptLayer ) );
	}
}
