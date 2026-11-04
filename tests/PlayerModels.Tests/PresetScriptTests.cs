using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

public class PresetScriptTests
{
	public static IEnumerable<object[]> Scripts()
	{
		foreach ( var (name, _) in PresetScripts.All )
			yield return new object[] { name };
	}

	private static string ScriptOf( string name )
	{
		foreach ( var (preset, script) in PresetScripts.All )
		{
			if ( preset == name )
				return script;
		}

		return null;
	}

	[Theory]
	[MemberData( nameof( Scripts ) )]
	public void BuildsTheSameEffectAsTheCodedPreset( string name ) => ScriptKit.SameEffect( ScriptKit.Preset( name ), ScriptKit.BuildEffect( ScriptOf( name ) ) );

	[Theory]
	[MemberData( nameof( Scripts ) )]
	public void IsWrittenCanonically( string name ) => Assert.Equal( ScriptOf( name ), Formatter.Format( ScriptOf( name ) ).TrimEnd( '\n' ) );

	[Theory]
	[MemberData( nameof( Scripts ) )]
	public void ExposesParameters( string name )
	{
		var program = ScriptKit.Compile( ScriptOf( name ) );
		Assert.InRange( program.Parameters.Count, 2, ScriptParameter.MaxPerScript );
	}

	[Fact]
	public void GalleryHasEveryPresetInOrder()
	{
		var gallery = PresetScripts.Gallery();
		Assert.Equal( EffectPresets.All.Count, gallery.Count );
		for ( var i = 0; i < gallery.Count; i++ )
			ScriptKit.SameEffect( EffectPresets.All[ i ], gallery[ i ] );
	}
}
