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
}
