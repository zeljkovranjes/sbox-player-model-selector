using System;
using System.Diagnostics;
using System.Text;
using PlayerModels.Core.Script;
using Xunit;
using Xunit.Abstractions;

namespace PlayerModels.Tests;

/// <summary>
/// Huge or hostile scripts are an attack, not a feature: refused fast (under 1 ms) allocating little beyond the refusal,
/// and the largest script within the caps still compiles in under 2 ms. Timings are the best of several warm runs.
/// </summary>
[Collection( Measurements.Name )]
public class AttackTests
{
	private const double RefuseMs = 1;
	private const double CompileMs = 2;
	private const int Runs = 20;
	private readonly ITestOutputHelper _output;

	public AttackTests( ITestOutputHelper output ) => _output = output;

	// The best time of several runs, and what one run allocated
	private (double Ms, long Bytes, CompileResult Result) Measure( string source )
	{
		ScriptCompiler.Compile( source );
		var best = double.MaxValue;
		long bytes = 0;
		CompileResult result = null;
		for ( var i = 0; i < Runs; i++ )
		{
			var before = GC.GetAllocatedBytesForCurrentThread();
			var clock = Stopwatch.StartNew();
			result = ScriptCompiler.Compile( source );
			clock.Stop();
			bytes = GC.GetAllocatedBytesForCurrentThread() - before;
			best = Math.Min( best, clock.Elapsed.TotalMilliseconds );
		}

		return (best, bytes, result);
	}

	private void Refused( string what, string source, string message, long mostBytes )
	{
		var (ms, bytes, result) = Measure( source );
		_output.WriteLine( $"{what}: {source.Length:N0} chars refused in {ms:0.0000} ms, {bytes:N0} bytes allocated: {result.Errors[ 0 ].Message}" );
		Assert.False( result.Succeeded );
		Assert.Contains( message, result.Errors[ 0 ].Message );
		Assert.True( ms < RefuseMs, $"{what} took {ms} ms" );
		Assert.True( bytes <= mostBytes, $"{what} allocated {bytes} bytes" );
	}

	private static string Repeat( string text, int times ) => new StringBuilder( text.Length * times ).Insert( 0, text, times ).ToString();

	[Fact]
	public void AMillionLineScriptIsRefusedOnItsLength() => Refused( "1,000,000 lines", Repeat( "local a = 1\n", 1_000_000 ), "Script is too long", 1024 );

	[Fact]
	public void TenMegabytesOfGarbageIsRefusedOnItsLength()
	{
		var random = new Random( 7 );
		var garbage = new char[ 10 * 1024 * 1024 ];
		for ( var i = 0; i < garbage.Length; i++ )
			garbage[ i ] = (char)random.Next( 0, 0xD800 );

		Refused( "10 MB garbage", new string( garbage ), "Script is too long", 1024 );
	}

	[Fact]
	public void DeepBracketsAreRefusedAtTheDepthCap() => Refused( "deep brackets", "local x = " + Repeat( "(", 30_000 ) + "1", "too deep", 16 * 1024 );

	[Fact]
	public void DeepBlocksAreRefusedAtTheDepthCap() => Refused( "deep blocks", Repeat( "if true then ", 4_000 ), "nests too deep", 32 * 1024 );

	[Fact]
	public void AGiantStringIsRefusedAtTheStringCap() => Refused( "giant string", "local s = \"" + Repeat( "a", 60_000 ) + "\"", "text is too long", 8 * 1024 );

	[Fact]
	public void AGiantNumberIsRefusedAtTheNumberCap() => Refused( "giant number", "local n = " + Repeat( "9", 60_000 ), "too long", 8 * 1024 );

	[Fact]
	public void ReadingAPaletteOverAndOverStopsAtTheListCap()
	{
		// Each read copies the palette into the script's lists: without a cap a short loop made the build take ~45 s
		var colours = string.Join( ", ", System.Linq.Enumerable.Repeat( "Color.Red", 32 ) );
		var program = ScriptKit.Compile( $"local glow = Sprite()\nglow.Palette = {{ {colours} }}\nfor k = 1, 40000 do\n\tlocal p = glow.Palette\nend" );
		var clock = Stopwatch.StartNew();
		var build = new ScriptMachine( program ).Build();
		clock.Stop();
		_output.WriteLine( $"palette read 40,000 times: stopped in {clock.Elapsed.TotalMilliseconds:0.0} ms: {build.Error}" );
		Assert.False( build.Succeeded );
		Assert.Contains( "lists are too big", build.Error.Message );
		Assert.True( clock.Elapsed.TotalMilliseconds < 50, $"took {clock.Elapsed.TotalMilliseconds} ms" );
	}

	[Fact]
	public void MakingListAfterListStopsAtTheListCapQuickly()
	{
		// Every list made used to copy all the lists before it: 40,000 one-item lists took ~145 ms and 1 GB
		var program = ScriptKit.Compile( "local glow = Sprite()\nfor k = 1, 40000 do\n\tlocal l = List.Create( 1, 0 )\nend" );
		new ScriptMachine( program ).Build();
		var before = GC.GetAllocatedBytesForCurrentThread();
		var clock = Stopwatch.StartNew();
		var build = new ScriptMachine( program ).Build();
		clock.Stop();
		var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
		_output.WriteLine( $"40,000 lists made: stopped in {clock.Elapsed.TotalMilliseconds:0.0} ms, {bytes:N0} bytes allocated: {build.Error}" );
		Assert.Contains( "lists are too big", build.Error.Message );
		Assert.True( clock.Elapsed.TotalMilliseconds < 20, $"took {clock.Elapsed.TotalMilliseconds} ms" );
		Assert.True( bytes < 2_000_000, $"allocated {bytes} bytes" );
	}

	[Fact]
	public void TheLargestScriptWithinTheCapsCompilesFast()
	{
		var source = MaximalScript();
		var (ms, bytes, result) = Measure( source );
		_output.WriteLine( $"maximal script: {source.Length:N0} chars ({result.StoredBytes:N0} bytes stored) compiled in {ms:0.000} ms, {bytes:N0} bytes allocated" );
		Assert.True( result.Succeeded, string.Join( "\n", result.Errors ) );
		Assert.InRange( result.StoredBytes, ScriptLimits.MaxScriptBytes - 512, ScriptLimits.MaxScriptBytes );
		Assert.True( ms < CompileMs, $"took {ms} ms" );
	}

	// An effect script just under 16 KB stored: a layer, then arithmetic on a few variables, statement after statement
	private static string MaximalScript()
	{
		var text = new StringBuilder( "local glow = Sprite()\nlocal x = 0\nlocal y = 1\n" );
		for ( var i = 0; Minifier.Minify( text.ToString() ).Length < ScriptLimits.MaxScriptBytes - 120; i++ )
			text.Append( $"x = x * 0.5 + Math.Sin( y + {i % 97} ) * {i % 13}\ny = Math.Max( x, {i % 7} )\n" );

		return text.Append( "glow.Size = Math.Clamp( x, 1, 4 )\n" ).ToString();
	}
}
