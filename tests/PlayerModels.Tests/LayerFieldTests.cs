using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>
/// The layer fields and kinds the online stress effects needed: each is set from a script, held to its bounds by the
/// sanitizer every look goes through, and given back by codegen.
/// </summary>
public class LayerFieldTests
{
	// An effect with one layer made by the script's lines, built, then rebuilt from its codegen
	private static EffectLayer RoundTrip( string lines )
	{
		var effect = ScriptKit.BuildEffect( lines );
		ScriptKit.SameEffect( effect, ScriptKit.BuildEffect( ScriptCodegen.Effect( effect ) ) );
		return effect.Layers[ 0 ];
	}

	[Theory]
	[InlineData( "12", 12f )]
	[InlineData( "12.4", 12f )]
	[InlineData( "1000", EffectLayer.MaxKeep )]
	[InlineData( "-3", 0f )]
	public void KeepIsAWholeNumberOfSpritesHeldToItsCap( string keep, float expected ) =>
		Assert.Equal( expected, RoundTrip( $"local orbs = Sprite()\norbs.Keep = {keep}" ).Keep );

	[Fact]
	public void KeptSpritesCountWhateverTheAmountSlider()
	{
		var layer = new EffectLayer { Amount = 10, Lifetime = 2, Keep = 30 }.Sanitized();
		Assert.Equal( 50f, layer.LiveSprites() );
		Assert.Equal( 35f, layer.LiveSprites( 0.25f ) );
		Assert.Equal( 0f, new EffectLayer { Kind = nameof( LayerKind.Light ), Keep = 30 }.Sanitized().Keep );
	}

	[Theory]
	[InlineData( "", 16f )]
	[InlineData( "bolt.Keep = 1", EffectLayer.MinBeamPoints )]
	[InlineData( "bolt.Keep = 500", EffectLayer.MaxBeamPoints )]
	[InlineData( "bolt.Keep = 24", 24f )]
	public void ABeamIsDrawnThroughItsPoints( string keep, float points )
	{
		var layer = RoundTrip( $"local bolt = Beam()\nbolt.ComesFrom = ComesFrom.Line\nbolt.ToJoint = Joint.RightHand\n{keep}" );
		Assert.Equal( (LayerKind.Beam, points), (layer.KindIs, layer.Keep) );
		Assert.Equal( points, layer.LiveSprites( 0.25f ) );
	}

	[Fact]
	public void ABeamsPointsRunItsParticleFunction()
	{
		var (machine, build) = ScriptKit.Build( "local bolt = Beam()\nfunction bolt.Particle( p )\n\tp.Offset = Vector3( 0, 0, Math.Sin( p.Index / (p.Count - 1) * Math.PI ) * 4 )\nend" );
		var io = ExampleRunner.Inputs();
		Assert.True( machine.HasParticleFunction( 0 ) && machine.RunParticle( 0, io ) );
		Assert.Equal( "local beam = Beam()\n", ScriptCodegen.Effect( build.Effect ) );
	}

	[Theory]
	[InlineData( "Align.Camera", LayerAlign.Camera, null )]
	[InlineData( "Align.Swing", LayerAlign.Swing, "Swing" )]
	[InlineData( "Align.Ground", LayerAlign.Ground, "Ground" )]
	[InlineData( "Align.Facing", LayerAlign.Facing, "Facing" )]
	[InlineData( "Align.Outward", LayerAlign.Outward, "Outward" )]
	public void SpritesFaceTheWayTheirLayerIsAligned( string align, LayerAlign expected, string saved )
	{
		var layer = RoundTrip( $"local prints = Sprite()\nprints.Align = {align}" );
		Assert.Equal( (expected, saved), (layer.AlignIs, layer.Align) );
	}

	[Fact]
	public void AnAlignmentThisVersionDoesntKnowDropsTheLayer() =>
		Assert.Null( new EffectLayer { Align = "Sideways" }.Sanitized() );

	[Theory]
	[InlineData( 12f, 0.4f, 12f, 48f )]
	[InlineData( 500f, 0.4f, EffectLayer.MaxGhostRate, 80f )]
	[InlineData( 30f, 5f, EffectLayer.MaxGhostRate, 80f )]
	public void GhostsAreHeldToTheirRateAndCountAgainstTheBudget( float amount, float lifetime, float rate, float sprites )
	{
		var layer = RoundTrip( $"local afterimage = Ghost()\nafterimage.Amount = {amount}\nafterimage.Lifetime = {lifetime}" );
		Assert.Equal( (LayerKind.Ghost, rate), (layer.KindIs, layer.Amount) );
		Assert.Equal( sprites, layer.LiveSprites(), 3 );
	}

	[Fact]
	public void EachSpriteTakesOneOfItsLayersPictures()
	{
		var effect = ScriptKit.BuildEffect( "local rain = Sprite()\nrain.Pictures = { Picture.Spark, Shapes.Heart, Shapes.Text( \"0\" ), Shapes.Text( \"1\" ) }" );
		var pictures = effect.Layers[ 0 ].Pictures;
		Assert.Equal( new[] { "Spark", "Heart" }, pictures.GetRange( 0, 2 ) );
		Assert.Equal( 2, effect.Shapes.Count );
		Assert.Equal( new[] { effect.Shapes[ 0 ].Reference, effect.Shapes[ 1 ].Reference }, pictures.GetRange( 2, 2 ) );
		Assert.Equal( new[] { effect.Shapes[ 0 ].Id, effect.Shapes[ 1 ].Id }, effect.Layers[ 0 ].DrawnShapeIds() );
		ScriptKit.SameEffect( effect, ScriptKit.BuildEffect( ScriptCodegen.Effect( effect ) ) );
	}

	[Fact]
	public void PicturesAreHeldToTheirCapAndToPicturesThisVersionKnows()
	{
		var many = string.Join( ", ", System.Linq.Enumerable.Repeat( "Picture.Spark", 17 ) );
		var build = new ScriptMachine( ScriptKit.Compile( $"local rain = Sprite()\nrain.Pictures = {{ {many} }}" ) ).Build();
		Assert.Contains( "at most 16 pictures", build.Error.Message );
		Assert.Equal( EffectLayer.MaxPictures, new EffectLayer { Pictures = new( System.Linq.Enumerable.Repeat( "Spark", 40 ) ) }.Sanitized().Pictures.Count );
		Assert.Null( new EffectLayer { Pictures = new() { "Spark", "Banana" } }.Sanitized() );
		Assert.Null( new EffectLayer { Pictures = new() { "@0123456789abcdef" } }.Sanitized() );
	}

	[Fact]
	public void ALookCarriesTheShapesALayersPicturesName()
	{
		var effect = ScriptKit.BuildEffect( "local rain = Sprite()\nrain.Pictures = { Shapes.Text( \"0\" ), Shapes.Text( \"1\" ) }" );
		var look = new PlayerLook { Effects = new() { effect }, DrawnShapes = new( effect.Shapes ) };
		Assert.Equal( 2, look.Standalone( effect ).Shapes.Count );
		var plain = look.WithEffectsAllowed( workshop: true, drawnShapes: false, blocked: _ => false );
		Assert.Null( plain.Effects[ 0 ].Layers[ 0 ].Pictures );
		Assert.Empty( plain.DrawnShapes );
	}

	[Fact]
	public void AParticleFunctionPicksItsFrame()
	{
		var (machine, _) = ScriptKit.Build( "local rain = Sprite()\nrain.Pictures = { Picture.Spark, Picture.Heart }\nfunction rain.Particle( p )\n\tp.Frame = p.Frame + 99\nend" );
		var io = ExampleRunner.Inputs();
		io.Outputs[ (int)ScriptOutput.Frame ] = 1;
		Assert.True( machine.RunParticle( 0, io ) );
		Assert.Equal( (EffectLayer.MaxPictures - 1, ScriptIO.FrameSet), (io.Get( ScriptOutput.Frame ), io.OutputsSet) );
	}

	[Fact]
	public void ALayerCanComeFromTheGroundUnderTheBody()
	{
		var effect = ScriptKit.BuildEffect( "Effect.Joint = Joint.Ground\nlocal circle = Sprite()\nlocal prints = Sprite()\nprints.Joint = Joint.Ground" );
		Assert.Equal( ("Ground", "Ground"), (effect.Anchor.Joint, effect.Layers[ 1 ].Joint) );
		Assert.Equal( Effect.Joint.Ground, effect.JointOf( effect.Layers[ 0 ] ) );
		ScriptKit.SameEffect( effect, ScriptKit.BuildEffect( ScriptCodegen.Effect( effect ) ) );
	}

	[Fact]
	public void AWeaponsSlashLiesAlongTheSwingAndSweepsRound()
	{
		var layer = RoundTrip( "local slash = Sprite()\nslash.LooksLike = Picture.Slash\nslash.Align = Align.Swing\nslash.Sweep = 110\nslash.SlashArc = 160\nslash.SlashThickness = 0.3" );
		Assert.Equal( (LayerAlign.Swing, 110f, 160f, 0.3f), (layer.AlignIs, layer.Sweep, layer.SlashShape.Arc, layer.SlashShape.Width) );
		Assert.Equal( (EffectLayer.SlashArc, EffectLayer.SlashWidth), RoundTrip( "local slash = Sprite()\nslash.LooksLike = Picture.Slash" ).SlashShape );
		Assert.Equal( EffectLayer.MaxSweep, RoundTrip( "local slash = Sprite()\nslash.Sweep = 5000" ).Sweep );
	}

	[Fact]
	public void SparksShowWhereAShotHitAndFollowersLag()
	{
		var sparks = RoundTrip( "local sparks = Sprite()\nsparks.When = When.OnHit\nsparks.AtHit = true" );
		Assert.True( sparks.AtHit );
		Assert.False( RoundTrip( "local sparks = Sprite()\nsparks.AtHit = true" ).AtHit );

		var snow = RoundTrip( "local snow = Sprite()\nsnow.Follow = true\nsnow.Drag = 0.4" );
		Assert.Equal( 0.6f, snow.StaysWithBody, 3 );
		Assert.Equal( 0f, RoundTrip( "local snow = Sprite()\nsnow.Drag = 0.4" ).StaysWithBody );
	}
}
