using System;
using System.Collections.Generic;
using System.Diagnostics;
using PlayerModels.Core.Script;
using Xunit;
using Xunit.Abstractions;

namespace PlayerModels.Tests;

/// <summary>
/// After a script is built, its runs allocate nothing: element runs, event and frame functions, particle functions,
/// calls, loops, lists made while running, parameters, the host's Emit, and runs that stop at a fault. Measured with
/// GC.GetAllocatedBytesForCurrentThread over many runs; the cost of a run is reported.
/// </summary>
[Collection( Measurements.Name )]
public class AllocationTests
{
	private const int Runs = 10_000;
	private readonly ITestOutputHelper _output;

	public AllocationTests( ITestOutputHelper output ) => _output = output;

	private sealed class CountingHost : IScriptHost
	{
		public int Sprites;
		public void Emit( int layer, int count ) => Sprites += count;
		public void EmitAt( int layer, double x, double y, double z, int count ) => Sprites += count;
	}

	private void NothingAllocated( string what, Func<bool> run, bool faults = false )
	{
		Assert.Equal( !faults, run() );
		System.GC.Collect();
		System.GC.WaitForPendingFinalizers();
		var clock = new Stopwatch();
		var before = GC.GetAllocatedBytesForCurrentThread();
		clock.Start();
		for ( var i = 0; i < Runs; i++ )
			run();

		clock.Stop();
		var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
		_output.WriteLine( $"{what}: {bytes} bytes over {Runs:N0} runs, {clock.Elapsed.TotalMilliseconds * 1_000_000 / Runs:0} ns a run" );
		Assert.Equal( 0, bytes );
	}

	[Fact]
	public void ElementRuns()
	{
		var stack = new ScriptStack();
		foreach ( var (name, script) in new[] { ("Rain", PresetScripts.Rain), ("Cycl", PresetScripts.Cycl), ("Cyln", PresetScripts.Cyln), ("Mix", PresetScripts.Mix), ("Pulse", PresetScripts.Pulse), ("Jiggle", PresetScripts.Jiggle) } )
		{
			var machine = new ScriptMachine( ScriptKit.Compile( script, ScriptKind.Sticker ) );
			machine.SetParameters( new Dictionary<string, string> { ["speed"] = "2", ["amount"] = "0.4" } );
			var io = ExampleRunner.Inputs();
			var i = 0;
			NothingAllocated( name + " element", () =>
			{
				io.Inputs[ (int)ScriptInput.Index ] = i++ % 5;
				io.Inputs[ (int)ScriptInput.Time ] += 7;
				return machine.RunElement( io, stack );
			} );
		}
	}

	[Fact]
	public void FrameEventAndParticleFunctions()
	{
		const string script = """
			local glow = Sprite()
			glow.Palette = { Color.Red, Color.Blue, Color.Yellow }
			local total = 0

			local function wave( x: number ): number
				return Math.Sin( x ) * 0.5 + 0.5
			end

			function OnUpdate( dt )
				glow.Size = 1 + wave( t ) * velocity.Length / 100
				glow.Color = Color.Lerp( Color.Red, Color.Blue, wave( t * 2 ) )
				glow.Offset = Vector3( 0, 0, wave( t ) * 4 )
				glow.Off = crouching
				local steps = { 1, dt, t }
				for k = 0, steps.Count - 1 do
					total = total + steps[ k ]
				end
			end

			function OnJump()
				glow.Emit( 10 )
				glow.EmitAt( velocity, 3 )
			end

			function glow.Particle( p )
				p.Color = glow.Palette[ p.Index % glow.Palette.Count ]
				p.Size = if p.Life > 0.5 then 2 else 1
			end
			""";

		var (machine, _) = ScriptKit.Build( script );
		var stack = new ScriptStack();
		var host = new CountingHost();
		var io = ExampleRunner.Inputs();
		NothingAllocated( "OnUpdate", () => machine.RunEvent( ScriptEvent.Update, io, host, stack ) );
		NothingAllocated( "OnJump with Emit", () => machine.RunEvent( ScriptEvent.Jump, io, host, stack ) );
		NothingAllocated( "Particle", () => machine.RunParticle( 0, io, host, stack ) );
	}

	[Fact]
	public void ChangingListItems()
	{
		var (machine, _) = ScriptKit.Build( "local glow = Sprite()\nlocal path = List.Create( 32, Vector3.Zero )\nlocal head = 0\nfunction OnUpdate( dt )\n\thead = (head + 1) % path.Count\n\tpath[ head ] = velocity * t\n\tlocal fresh = { 1, 2 }\n\tfresh[ 0 ] = dt\nend" );
		var stack = new ScriptStack();
		var io = ExampleRunner.Inputs();
		NothingAllocated( "list items changed", () => machine.RunEvent( ScriptEvent.Update, io, null, stack ) );
	}

	[Fact]
	public void RunsThatFault()
	{
		var (machine, _) = ScriptKit.Build( "local glow = Sprite()\nfunction OnUpdate( dt )\n\twhile true do\n\tend\nend\nfunction OnJump()\n\tlocal l = { 1 }\n\tlocal x = l[ 5 ]\nend" );
		var stack = new ScriptStack();
		var io = new ScriptIO();
		NothingAllocated( "budget fault", () => machine.RunEvent( ScriptEvent.Update, io, null, stack, 500 ), faults: true );
		NothingAllocated( "index fault", () => machine.RunEvent( ScriptEvent.Jump, io, null, stack ), faults: true );
	}

	[Fact]
	public void RecursionAndFunctionValues()
	{
		var machine = new ScriptMachine( ScriptKit.Compile( "local function fib( n: number ): number\n\tif n < 2 then\n\t\treturn n\n\tend\n\treturn fib( n - 1 ) + fib( n - 2 )\nend\nlocal f: (number) -> number = fib\nsize = f( 10 )", ScriptKind.Sticker ) );
		var stack = new ScriptStack();
		var io = ExampleRunner.Inputs();
		NothingAllocated( "recursion (fib 10)", () => machine.RunElement( io, stack ) );
		Assert.Equal( 48, io.Get( ScriptOutput.Size ) );
	}
}
