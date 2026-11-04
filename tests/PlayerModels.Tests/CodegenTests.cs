using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

public class CodegenTests
{
	[Theory]
	[MemberData( nameof( ScriptKit.Presets ), MemberType = typeof( ScriptKit ) )]
	public void PresetsRoundTripThroughCode( string name )
	{
		var preset = ScriptKit.Preset( name );
		var script = ScriptCodegen.Effect( preset );
		ScriptKit.SameEffect( preset, ScriptKit.BuildEffect( script ) );
	}

	[Theory]
	[MemberData( nameof( ScriptKit.Presets ), MemberType = typeof( ScriptKit ) )]
	public void CodegenIsCanonical( string name )
	{
		var script = ScriptCodegen.Effect( ScriptKit.Preset( name ) );
		Assert.Equal( script, Formatter.Format( script ) );
	}
}
