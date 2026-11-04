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
}
