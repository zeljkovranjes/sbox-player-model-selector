using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using PlayerModels.Core.Script;
using Xunit;
using Xunit.Abstractions;

namespace PlayerModels.Tests.StressEffects;

/// <summary>
/// The online stress effects as regression tests: each has its source and description, compiles with no errors, is
/// written canonically with a few parameters, builds the effect (or shape) its snapshot recorded (run with
/// UPDATE_SNAPSHOTS=1 to write them), and runs three seconds of play with every run within its budget.
/// </summary>
public class StressEffectTests
{
	public static IEnumerable<object[]> Names()
	{
		foreach ( var effect in StressScripts.All )
			yield return new object[] { effect.Name };
	}

	internal static IEnumerable<(string Source, ScriptKind Kind)> Scripts( StressEffect effect )
	{
		yield return (effect.Script, effect.Kind);
		if ( effect.Sticker is not null )
			yield return (effect.Sticker, ScriptKind.Sticker);
	}

	[Theory]
	[MemberData( nameof( Names ) )]
	public void HasASourceAndADescription( string name )
	{
		var effect = StressScripts.Named( name );
		Assert.StartsWith( "https://", effect.Source );
		Assert.False( string.IsNullOrWhiteSpace( effect.Description ) );
	}

	[Theory]
	[MemberData( nameof( Names ) )]
	public void CompilesWithNoErrors( string name )
	{
		foreach ( var (source, kind) in Scripts( StressScripts.Named( name ) ) )
			Assert.Equal( "", string.Join( "\n", ScriptCompiler.Compile( source, kind ).Errors ) );
	}

	[Theory]
	[MemberData( nameof( Names ) )]
	public void IsWrittenCanonically( string name )
	{
		foreach ( var (source, _) in Scripts( StressScripts.Named( name ) ) )
			Assert.Equal( source, Formatter.Format( source ).TrimEnd( '\n' ) );
	}

	[Theory]
	[MemberData( nameof( Names ) )]
	public void ExposesParameters( string name )
	{
		var parameters = 0;
		foreach ( var (source, kind) in Scripts( StressScripts.Named( name ) ) )
			parameters += ScriptKit.Compile( source, kind ).Parameters.Count;

		Assert.InRange( parameters, 2, ScriptParameter.MaxPerScript );
	}

	[Theory]
	[MemberData( nameof( Names ) )]
	public void BuildsItsSnapshot( string name )
	{
		var effect = StressScripts.Named( name );
		var made = Snapshot( ScriptKit.Build( effect.Script, effect.Kind ).Build );
		var path = Path.Combine( SnapshotFolder(), effect.FileName + ".json" );
		if ( Environment.GetEnvironmentVariable( "UPDATE_SNAPSHOTS" ) == "1" )
		{
			File.WriteAllText( path, made );
			return;
		}

		Assert.True( File.Exists( path ), $"No snapshot for {name}: run the tests with UPDATE_SNAPSHOTS=1" );
		Assert.Equal( File.ReadAllText( path ), made );
	}

	[Theory]
	[MemberData( nameof( Names ) )]
	public void RunsThreeSecondsOfPlayWithinBudget( string name )
	{
		var run = Start( StressScripts.Named( name ) );
		Assert.True( run.Run(), $"{name} stopped: {run.Fault}" );
		Assert.True( run.Runs > StressRun.Frames, $"{name} only ran {run.Runs} times" );
	}

	/// <summary>A run of the effect, built: its effect script, or its sticker over its shape.</summary>
	internal static StressRun Start( StressEffect effect )
	{
		var (machine, build) = ScriptKit.Build( effect.Script, effect.Kind );
		if ( effect.Sticker is null )
			return new StressRun( machine, build.Effect );

		return new StressRun( new ScriptMachine( ScriptKit.Compile( effect.Sticker, ScriptKind.Sticker ) ), build );
	}

	// What a build made, as indented JSON: the effect as a look keeps it, or the shape and its emblem layers
	private static string Snapshot( ScriptBuild build )
	{
		var options = new JsonSerializerOptions { WriteIndented = true };
		var json = build.Effect is not null
			? ScriptKit.Data( build.Effect )
			: JsonSerializer.Serialize( new { shape = build.Shape, emblem = build.Emblem } );

		return JsonSerializer.Serialize( JsonDocument.Parse( json ).RootElement, options ).ReplaceLineEndings( "\n" ) + "\n";
	}

	private static string SnapshotFolder()
	{
		var directory = AppContext.BaseDirectory;
		while ( directory is not null && !Directory.Exists( Path.Combine( directory, "StressEffects" ) ) )
			directory = Path.GetDirectoryName( directory );

		Assert.NotNull( directory );
		var folder = Path.Combine( directory, "StressEffects", "Snapshots" );
		Directory.CreateDirectory( folder );
		return folder;
	}
}

/// <summary>Three seconds of every stress effect allocate nothing once it is built; the cost of a frame is reported.</summary>
[Collection( Measurements.Name )]
public class StressEffectAllocationTests
{
	private readonly ITestOutputHelper _output;

	public StressEffectAllocationTests( ITestOutputHelper output ) => _output = output;

	[Theory]
	[MemberData( nameof( StressEffectTests.Names ), MemberType = typeof( StressEffectTests ) )]
	public void RunsWithoutAllocating( string name )
	{
		var run = StressEffectTests.Start( StressScripts.Named( name ) );
		Assert.True( run.Run(), run.Fault );
		GC.Collect();
		GC.WaitForPendingFinalizers();
		var clock = Stopwatch.StartNew();
		var before = GC.GetAllocatedBytesForCurrentThread();
		var finished = run.Run();
		var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
		clock.Stop();
		_output.WriteLine( $"{name}: {run.Runs:N0} runs over {StressRun.Frames} frames, {clock.Elapsed.TotalMilliseconds * 1000 / StressRun.Frames:0.0} us a frame, {bytes} bytes" );
		Assert.True( finished, run.Fault );
		Assert.Equal( 0, bytes );
	}
}
