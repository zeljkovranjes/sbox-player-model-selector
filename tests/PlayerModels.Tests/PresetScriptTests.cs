using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
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

	// Each preset as the C# builders made it, before the scripts became the presets (written with UPDATE_SNAPSHOTS=1)
	private static string SnapshotFolder( [CallerFilePath] string here = "" ) => Path.Combine( Path.GetDirectoryName( here ), "PresetSnapshots" );

	private static string SnapshotOf( Effect effect )
	{
		var copy = effect.Sanitized();
		copy.Script = null;
		return ScriptKit.Data( copy );
	}

	[Theory]
	[MemberData( nameof( Scripts ) )]
	public void BuildsTheEffectTheCodedPresetMade( string name )
	{
		var path = Path.Combine( SnapshotFolder(), name.ToLowerInvariant().Replace( ' ', '-' ) + ".json" );
		var made = SnapshotOf( ScriptKit.BuildEffect( ScriptOf( name ) ) );
		if ( Environment.GetEnvironmentVariable( "UPDATE_SNAPSHOTS" ) == "1" )
		{
			Directory.CreateDirectory( SnapshotFolder() );
			File.WriteAllText( path, made );
			return;
		}

		Assert.True( File.Exists( path ), $"No snapshot for {name}: run the tests with UPDATE_SNAPSHOTS=1" );
		Assert.Equal( File.ReadAllText( path ), made );
	}

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

	[Theory]
	[MemberData( nameof( Scripts ) )]
	public void EveryPresetCarriesItsScript( string name )
	{
		var preset = ScriptKit.Preset( name );
		Assert.Equal( Minifier.Minify( ScriptOf( name ) ), preset.Script );
		Assert.True( ScriptLimits.Fits( preset.Script ) );
	}

	[Fact]
	public void AnyEightPresetsFitALooksScripts()
	{
		var sizes = new List<int>();
		foreach ( var preset in EffectPresets.All )
			sizes.Add( ScriptLimits.Utf8Bytes( preset.Script ) );

		sizes.Sort();
		sizes.Reverse();
		var biggest = 0;
		for ( var i = 0; i < Effect.MaxWorn && i < sizes.Count; i++ )
			biggest += sizes[ i ];

		Assert.True( biggest <= ScriptLimits.MaxLookBytes, $"The 8 biggest presets' scripts come to {biggest} bytes" );
	}

	[Fact]
	public void PresetsWornAreBuiltTheSameOnEveryMachine()
	{
		var look = new PlayerLook();
		foreach ( var preset in EffectPresets.All )
		{
			if ( look.Effects.Count < Effect.MaxWorn )
				look.Adopt( EffectPresets.Copy( preset, look.Effects ) );
		}

		var read = PlayerLook.Parse( look.Serialize() );
		var shown = EffectScripts.WithScriptsBuilt( read, x => EffectScripts.Build( x, new ScriptCache(), new LookLibrary( read.DrawnShapes ) ), out _ );
		for ( var i = 0; i < read.Effects.Count; i++ )
		{
			Assert.NotNull( shown.Effects[ i ].Script );
			ScriptKit.SameEffect( read.Effects[ i ], shown.Effects[ i ] );
		}
	}
}
