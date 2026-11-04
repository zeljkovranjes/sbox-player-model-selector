using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PlayerModels.Core.Script;
using Xunit;
using Xunit.Abstractions;

namespace PlayerModels.Tests;

/// <summary>
/// Thousands of random, garbage and mutated scripts: none crashes the compiler or the machine, hangs, throws out of
/// them, or allocates while running; every one that parses survives minify and format round trips.
/// </summary>
[Collection( Measurements.Name )]
public class FuzzTests
{
	private const int Soup = 3000;
	private const int Garbage = 1500;
	private const int MutationsEach = 20;
	private readonly ITestOutputHelper _output;

	public FuzzTests( ITestOutputHelper output ) => _output = output;

	private static readonly string[] Vocabulary =
	{
		"local", "function", "end", "if", "then", "elseif", "else", "for", "do", "while", "return", "break", "and", "or", "not", "true",
		"false", "nil", "x", "y", "glow", "p", "shape", "Effect", "Sprite()", "Light()", "Math", "Color", "Vector3", "Shapes", "Picture",
		"Joint", "Rhythm", ".", ",", "(", ")", "[", "]", "{", "}", "=", "==", "~=", "<", ">", "+", "-", "*", "/", "%", ":", ";", "->",
		"0", "1", "2.5", "1e15", "-1", "\"a\"", "\"#ff0000\"", "\"g\"", "--c\n", "--[[b]]", "\n", "\n\n", "Size", "Color", "Floor",
		"Parse", "Particle", "Emit", "time", "i", "count", "palette", "color", "OnUpdate", "OnJump", "number", "[Parameter]", "Min",
	};

	private sealed class Tally
	{
		public int Inputs, Rejected, Compiled, Built, Runs, Faults, RoundTrips;
		public long WorstTicks;
		public readonly List<string> Problems = new();
	}

	[Fact]
	public void NothingCrashesHangsThrowsOrAllocates()
	{
		var random = new Random( 20261104 );
		var tally = new Tally();
		var stack = new ScriptStack();
		var clock = Stopwatch.StartNew();

		for ( var n = 0; n < Soup; n++ )
		{
			var text = new StringBuilder();
			for ( var t = random.Next( 1, 120 ); t > 0; t-- )
				text.Append( Vocabulary[ random.Next( Vocabulary.Length ) ] ).Append( random.Next( 3 ) == 0 ? "" : " " );

			Try( text.ToString(), n % 2 == 0 ? ScriptKind.Effect : ScriptKind.Sticker, tally, stack );
		}

		for ( var n = 0; n < Garbage; n++ )
		{
			var chars = new char[ n % 100 == 0 ? random.Next( 0, 70_000 ) : random.Next( 0, 500 ) ];
			for ( var i = 0; i < chars.Length; i++ )
				chars[ i ] = (char)(n % 3 == 0 ? random.Next( 0, 0xD800 ) : random.Next( 0, 128 ));

			Try( new string( chars ), (ScriptKind)(n % 3), tally, stack );
		}

		foreach ( var (source, kind) in Corpus() )
		{
			for ( var m = 0; m < MutationsEach; m++ )
				Try( Mutate( source, random ), kind, tally, stack );
		}

		clock.Stop();
		_output.WriteLine( $"Fuzz: {tally.Inputs:N0} scripts in {clock.Elapsed.TotalSeconds:0.0} s; {tally.Rejected:N0} refused, {tally.Compiled:N0} compiled, {tally.Built:N0} built, " +
			$"{tally.Runs:N0} runs ({tally.Faults:N0} stopped by a fault), {tally.RoundTrips:N0} minify/format round trips; slowest {TimeSpan.FromTicks( tally.WorstTicks ).TotalMilliseconds:0.0} ms; " +
			$"{tally.Problems.Count} problems" );

		if ( Environment.GetEnvironmentVariable( "FUZZ_DUMP" ) is { } dump )
			File.WriteAllText( dump, string.Join( "\n\n=====\n", tally.Problems ) );

		Assert.True( tally.Problems.Count == 0, string.Join( "\n\n", tally.Problems.GetRange( 0, Math.Min( 5, tally.Problems.Count ) ) ) );
	}

	private static IEnumerable<(string, ScriptKind)> Corpus()
	{
		foreach ( var (_, script) in PresetScripts.All )
			yield return (script, ScriptKind.Effect);

		foreach ( var script in new[] { PresetScripts.Rain, PresetScripts.Cycl, PresetScripts.Cyln, PresetScripts.Mix, PresetScripts.Pulse, PresetScripts.Flicker, PresetScripts.Jiggle } )
			yield return (script, ScriptKind.Sticker);

		foreach ( var entry in ScriptLibrary.All )
		{
			if ( entry.Example is not null )
				yield return (entry.Example, entry.ExampleKind);
		}

		foreach ( var effect in StressEffects.StressScripts.All )
		{
			foreach ( var script in StressEffects.StressEffectTests.Scripts( effect ) )
				yield return script;
		}
	}

	private static readonly Regex Numbers = new( @"(?<![\w.])\d+(\.\d+)?" );
	private static readonly string[] Extremes = { "0", "-1", "1e15", "-1e15", "0.0001", "99999", "255", "-0.5", "1e-9" };

	private static string Mutate( string source, Random random )
	{
		// Half the time a number becomes an extreme one: the script mostly still compiles, and runs on odd values
		if ( random.Next( 2 ) == 0 )
		{
			var matches = Numbers.Matches( source );
			if ( matches.Count > 0 )
			{
				var match = matches[ random.Next( matches.Count ) ];
				return source[ ..match.Index ] + Extremes[ random.Next( Extremes.Length ) ] + source[ (match.Index + match.Length).. ];
			}
		}

		var text = new StringBuilder( source );
		for ( var edits = random.Next( 1, 4 ); edits > 0 && text.Length > 0; edits-- )
		{
			var at = random.Next( text.Length );
			switch ( random.Next( 5 ) )
			{
				case 0: text.Remove( at, Math.Min( random.Next( 1, 8 ), text.Length - at ) ); break;
				case 1: text.Insert( at, Vocabulary[ random.Next( Vocabulary.Length ) ] ); break;
				case 2: text[ at ] = (char)random.Next( 32, 127 ); break;
				case 3:
				{
					var line = source.Split( '\n' );
					text.Insert( at, line[ random.Next( line.Length ) ] + "\n" );
					break;
				}

				default: text.Insert( at, "\n" ); break;
			}
		}

		return text.ToString();
	}

	private static void Try( string source, ScriptKind kind, Tally tally, ScriptStack stack )
	{
		tally.Inputs++;
		var started = Stopwatch.GetTimestamp();
		try
		{
			Check( source, kind, tally, stack );
		}
		catch ( Exception exception )
		{
			tally.Problems.Add( $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}\n---\n{source}" );
		}

		tally.WorstTicks = Math.Max( tally.WorstTicks, (long)((Stopwatch.GetTimestamp() - started) * (TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency)) );
	}

	private static void Check( string source, ScriptKind kind, Tally tally, ScriptStack stack )
	{
		var result = ScriptCompiler.Compile( source, kind, ExampleRunner.Library );
		if ( result.Tree?.Error is null && result.Tree is not null )
			RoundTrip( source, kind, result, tally );

		if ( !result.Succeeded )
		{
			tally.Rejected++;
			return;
		}

		tally.Compiled++;
		var machine = new ScriptMachine( result.Program );
		var io = ExampleRunner.Inputs();
		if ( kind != ScriptKind.Sticker )
		{
			if ( !machine.Build( ExampleRunner.Library, stack ).Succeeded || kind == ScriptKind.Shape )
				return;

			tally.Built++;
		}

		RunAll( machine, kind, io, stack, tally );
		var before = GC.GetAllocatedBytesForCurrentThread();
		RunAll( machine, kind, io, stack, tally );
		var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
		if ( bytes != 0 )
			tally.Problems.Add( $"Runs allocated {bytes} bytes\n---\n{source}" );
	}

	private static void RunAll( ScriptMachine machine, ScriptKind kind, ScriptIO io, ScriptStack stack, Tally tally )
	{
		if ( kind == ScriptKind.Sticker )
		{
			Count( machine.RunElement( io, stack ), tally );
			return;
		}

		for ( var scriptEvent = ScriptEvent.Spawn; scriptEvent < ScriptEvent.Total; scriptEvent++ )
		{
			if ( machine.Program.Handles( scriptEvent ) )
				Count( machine.RunEvent( scriptEvent, io, null, stack ), tally );
		}

		for ( var layer = 0; layer < machine.LayerCount; layer++ )
		{
			if ( machine.HasParticleFunction( layer ) )
				Count( machine.RunParticle( layer, io, null, stack ), tally );
		}
	}

	private static void Count( bool finished, Tally tally )
	{
		tally.Runs++;
		if ( !finished )
			tally.Faults++;
	}

	// format(minify(s)) == format(s), minify(format(s)) == minify(s), and all three compile to the same program
	private static void RoundTrip( string source, ScriptKind kind, CompileResult result, Tally tally )
	{
		var formatted = Formatter.Format( source );
		var minified = Minifier.Minify( source );
		foreach ( var text in new[] { formatted, minified } )
		{
			var again = ScriptCompiler.Compile( text, kind, ExampleRunner.Library );
			if ( again.Succeeded != result.Succeeded || result.Succeeded && !again.Program.Code.AsSpan().SequenceEqual( result.Program.Code ) )
				tally.Problems.Add( $"Formatted or minified compiles differently\n--- source\n{source}\n--- text\n{text}" );
		}

		if ( Formatter.Format( minified ) != formatted || Minifier.Minify( formatted ) != minified )
			tally.Problems.Add( $"Round trip differs\n--- source\n{source}\n--- formatted\n{formatted}\n--- minified\n{minified}\n--- format(minified)\n{Formatter.Format( minified )}\n--- minify(formatted)\n{Minifier.Minify( formatted )}" );

		tally.RoundTrips++;
	}
}
