using System.Collections.Generic;
using System.Text.RegularExpressions;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>A player's library for tests: a shape, a sticker, an emblem and an effect of their own.</summary>
internal sealed class TestLibrary : IPlayerLibrary
{
	public IReadOnlyList<LibraryItem> Items { get; } = new[]
	{
		new LibraryItem( "COOL", LibraryItemKind.Shape, "c-cool" ), new LibraryItem( "Gold skull", LibraryItemKind.Sticker, "c-skull" ),
		new LibraryItem( "Clan", LibraryItemKind.Emblem, "c-clan" ), new LibraryItem( "Fire hands", LibraryItemKind.Effect, "c-fire" ),
	};

	public Effect Effect( string name ) => name == "Fire hands" ? ScriptKit.Preset( "Fire hands" ) : null;

	public DrawnShape Shape( string name, LibraryItemKind kind ) => (name, kind) switch
	{
		("COOL", LibraryItemKind.Shape) => DrawnShape.From( HersheyFont.Strokes( "COOL" ), "COOL" ),
		("Gold skull", LibraryItemKind.Sticker) => DrawnShape.From( HersheyFont.Strokes( "SK" ), "Gold skull", new Sticker { Fill = "#ffd700", Outline = "#000000", OutlineWidth = 0.1f } ),
		("Clan", LibraryItemKind.Emblem) => DrawnShape.From( HersheyFont.Strokes( "CLAN" ), "Clan" ),
		_ => null,
	};
}

/// <summary>
/// Runs an example the way the reference promises: it compiles with no errors, runs within the budget (an effect's build
/// then each of its events and particle functions once; a shape's build; a sticker's run for one element), and every
/// <c>local name = ... --&gt; value</c> holds that value once it has run.
/// </summary>
internal static class ExampleRunner
{
	public static readonly TestLibrary Library = new();

	private static readonly Regex Expectation = new( @"^\s*local\s+(\w+)\b.*-->\s*(.+?)\s*$", RegexOptions.Multiline );

	/// <summary>
	/// The inputs examples run with: 1.5 s in, element 2 of 5 (at 0.5, -0.25 on its shape), a quarter through its life,
	/// running sideways on the ground with the effect's place at (100, 200, 50).
	/// </summary>
	public static ScriptIO Inputs()
	{
		var io = new ScriptIO();
		io.Set( ScriptInput.Time, 1500 );
		io.Set( ScriptInput.Seconds, 1.5 );
		io.Set( ScriptInput.Speed, 120 );
		io.Set( ScriptInput.VelocityX, 0, 120, 0 );
		io.Set( ScriptInput.Grounded, 1 );
		io.Set( ScriptInput.Moving, 1 );
		io.Set( ScriptInput.AnchorSpeed, 80 );
		io.Set( ScriptInput.AnchorPositionX, 100, 200, 50 );
		io.Set( ScriptInput.Index, 2 );
		io.Set( ScriptInput.Count, 5 );
		io.Set( ScriptInput.Life, 0.25 );
		io.Set( ScriptInput.Seed, 0.375 );
		io.Set( ScriptInput.OriginX, 0.5, -0.25, 0 );
		io.Set( ScriptInput.Dt, 1 / 60.0 );
		io.Set( ScriptInput.Amount, 300 );
		io.Set( ScriptInput.Foot, (int)Effect.Joint.LeftFoot );
		io.Set( ScriptInput.ShotHit, 1 );
		io.Set( ScriptInput.ShotDistance, 400 );
		return io;
	}

	// An effect needs a layer: an example that makes none is run with one added after it
	private static readonly Regex MakesLayer = new( @"\b(Sprite|Light|Trail|Beam)\(\)|Effect\.Include" );

	/// <summary>Runs <paramref name="code"/> as a script of <paramref name="kind"/> and checks its expectations; gives how many it checked.</summary>
	public static int Run( string code, ScriptKind kind )
	{
		var source = kind == ScriptKind.Effect && !MakesLayer.IsMatch( code ) ? code + "\nlocal exampleLayer = Sprite()" : code;
		var result = ScriptCompiler.Compile( source, kind, Library );
		Assert.True( result.Succeeded, "Example doesn't compile:\n" + string.Join( "\n", result.Errors ) + "\n---\n" + code );
		var machine = new ScriptMachine( result.Program );
		var io = Inputs();
		switch ( kind )
		{
			case ScriptKind.Sticker:
				Assert.True( machine.RunElement( io ), "Example faulted: " + machine.Fault.Describe( result.Program ) + "\n---\n" + code );
				break;
			default:
			{
				var build = machine.Build( Library );
				Assert.True( build.Succeeded, "Example doesn't build: " + build.Error + "\n---\n" + code );
				break;
			}
		}

		var checkedCount = 0;
		foreach ( Match match in Expectation.Matches( code ) )
		{
			Assert.Equal( match.Groups[ 2 ].Value, machine.Variable( match.Groups[ 1 ].Value ) );
			checkedCount++;
		}

		if ( kind == ScriptKind.Effect )
			RunFunctions( machine, io, code );

		return checkedCount;
	}

	private static void RunFunctions( ScriptMachine machine, ScriptIO io, string code )
	{
		for ( var scriptEvent = ScriptEvent.Spawn; scriptEvent < ScriptEvent.Total; scriptEvent++ )
			Assert.True( machine.RunEvent( scriptEvent, io ), $"On{scriptEvent} faulted: " + machine.Fault.Describe( machine.Program ) + "\n---\n" + code );

		for ( var layer = 0; layer < machine.LayerCount; layer++ )
			Assert.True( machine.RunParticle( layer, io ), "Particle function faulted: " + machine.Fault.Describe( machine.Program ) + "\n---\n" + code );
	}
}
