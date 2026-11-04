using System.Collections.Generic;
using System.Text.Json;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>Shared helpers: compile-or-fail, build-or-fail, and comparing effects as the look stores them.</summary>
internal static class ScriptKit
{
	public static ScriptProgram Compile( string source, ScriptKind kind = ScriptKind.Effect, IPlayerLibrary library = null )
	{
		var result = ScriptCompiler.Compile( source, kind, library );
		Assert.True( result.Succeeded, "Compile failed:\n" + string.Join( "\n", result.Errors ) + "\n---\n" + source );
		return result.Program;
	}

	public static (ScriptMachine Machine, ScriptBuild Build) Build( string source, ScriptKind kind = ScriptKind.Effect, IPlayerLibrary library = null )
	{
		var machine = new ScriptMachine( Compile( source, kind, library ) );
		var build = machine.Build( library );
		Assert.True( build.Succeeded, "Build failed: " + build.Error + "\n---\n" + source );
		return (machine, build);
	}

	public static Effect BuildEffect( string source, IPlayerLibrary library = null ) => Build( source, ScriptKind.Effect, library ).Build.Effect;

	/// <summary>The first compile error's message (it must fail).</summary>
	public static string Error( string source, ScriptKind kind = ScriptKind.Effect, IPlayerLibrary library = null )
	{
		var result = ScriptCompiler.Compile( source, kind, library );
		Assert.False( result.Succeeded, "Expected a compile error:\n" + source );
		return result.Errors[ 0 ].ToString();
	}

	/// <summary>An effect as a look keeps it, without what stays outside its script (its id, where it was forked from).</summary>
	public static string Data( Effect effect )
	{
		var copy = effect.Sanitized();
		copy.Id = "";
		copy.ForkedFrom = null;
		return JsonSerializer.Serialize( copy );
	}

	public static void SameEffect( Effect expected, Effect actual ) => Assert.Equal( Data( expected ), Data( actual ) );

	public static IEnumerable<object[]> Presets()
	{
		foreach ( var preset in EffectPresets.All )
			yield return new object[] { preset.Name };
	}

	public static Effect Preset( string name ) => EffectPresets.All[ IndexOf( name ) ];

	private static int IndexOf( string name )
	{
		for ( var i = 0; i < EffectPresets.All.Count; i++ )
		{
			if ( EffectPresets.All[ i ].Name == name )
				return i;
		}

		return -1;
	}
}

/// <summary>Runs small sticker scripts and reads their variables.</summary>
internal static class Eval
{
	/// <summary>The value of <paramref name="expression"/>, as the reference writes values.</summary>
	public static string Of( string expression ) => Script( "local result = " + expression ).Variable( "result" );

	/// <summary>Runs <paramref name="source"/> once as a sticker script (with the examples' inputs), giving its machine.</summary>
	public static ScriptMachine Script( string source )
	{
		var machine = new ScriptMachine( ScriptKit.Compile( source, ScriptKind.Sticker ) );
		Assert.True( machine.RunElement( ExampleRunner.Inputs() ), machine.Fault.Describe( machine.Program )?.ToString() );
		return machine;
	}

	/// <summary>Runs <paramref name="source"/>, which must fault: its fault.</summary>
	public static ScriptFault Fault( string source )
	{
		var machine = new ScriptMachine( ScriptKit.Compile( source, ScriptKind.Sticker ) );
		Assert.False( machine.RunElement( ExampleRunner.Inputs() ), "Expected a fault" );
		return machine.Fault;
	}
}
