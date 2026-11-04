using System;
using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>
/// Every library function and method: its normal case is its reference example (see <see cref="LibraryExampleTests"/>);
/// here its invalid arguments (too few, too many, the wrong type) fail to compile, and its edge cases give safe values.
/// </summary>
public class LibraryFunctionTests
{
	public static IEnumerable<object[]> Callables()
	{
		foreach ( var entry in ScriptLibrary.All )
		{
			if ( entry.Kind is EntryKind.Function or EntryKind.Method )
				yield return new object[] { entry.Name };
		}
	}

	private static LibraryEntry Find( string name )
	{
		foreach ( var entry in ScriptLibrary.All )
		{
			if ( entry.Name == name && entry.Kind is EntryKind.Function or EntryKind.Method )
				return entry;
		}

		throw new ArgumentException( name );
	}

	private static string ValueOf( ScriptType type ) => type.Kind switch
	{
		TypeKind.Number => "1",
		TypeKind.Bool => "true",
		TypeKind.String => "\"COOL\"",
		TypeKind.Color => "Color.Red",
		TypeKind.Vector3 => "Vector3.Up",
		TypeKind.List => "{ 1, 2 }",
		TypeKind.Shape => "Shapes.Heart",
		TypeKind.Picture => "Picture.Glow",
		TypeKind.Effect => "Effects.Preset( \"Hearts\" )",
		TypeKind.Key => "\"g\"",
		_ => "1",
	};

	// A call to entry with arguments, in a script where it can be called
	private static (string Source, ScriptKind Kind) CallOf( LibraryEntry entry, IReadOnlyList<string> arguments )
	{
		var call = $"{entry.Member}( {string.Join( ", ", arguments )} )";
		if ( entry.Kind == EntryKind.Method )
		{
			return entry.Owner switch
			{
				"Layer" => ($"local layer = Sprite()\nfunction OnJump()\n\tlayer.{call}\nend", ScriptKind.Effect),
				"Effect" => ($"Effect.{call}", ScriptKind.Effect),
				_ => ($"local s = Shapes.New( \"x\" )\nlocal glow = Sprite()\n{(entry.Type.Kind == TypeKind.Void ? "" : "local r = ")}s.{call}", ScriptKind.Effect),
			};
		}

		var kind = (entry.Where & ScriptPlace.StickerRun) != 0 ? ScriptKind.Sticker : ScriptKind.Effect;
		var statement = entry.Type.Kind == TypeKind.Void ? entry.Name[ ..^entry.Member.Length ] + call : $"local r = {entry.Name[ ..^entry.Member.Length ]}{call}";
		return (kind == ScriptKind.Effect ? statement + "\nlocal glow = Sprite()" : statement, kind);
	}

	private static string Errors( string source, ScriptKind kind ) =>
		string.Join( "\n", ScriptCompiler.Compile( source, kind, ExampleRunner.Library ).Errors );

	private static List<string> Valid( LibraryEntry entry, int count )
	{
		var arguments = new List<string>();
		for ( var i = 0; i < count; i++ )
			arguments.Add( i < entry.Parameters.Count ? ValueOf( entry.Parameters[ i ].Type ) : "1" );

		return arguments;
	}

	[Theory]
	[MemberData( nameof( Callables ) )]
	public void CompilesWithValidArguments( string name )
	{
		var entry = Find( name );
		var (source, kind) = CallOf( entry, Valid( entry, entry.Parameters.Count ) );
		if ( entry.Op is Intrinsic.ShapesMine or Intrinsic.StickersMine or Intrinsic.EmblemsMine or Intrinsic.EffectsMine or Intrinsic.EffectsPreset or Intrinsic.ColorParse )
			source = source.Replace( "\"COOL\"", "\"" + Mine( entry.Op ) + "\"" );

		Assert.Equal( "", Errors( source, kind ) );
	}

	private static string Mine( Intrinsic op ) => op switch
	{
		Intrinsic.StickersMine => "Gold skull",
		Intrinsic.EmblemsMine => "Clan",
		Intrinsic.EffectsMine => "Fire hands",
		Intrinsic.EffectsPreset => "Hearts",
		Intrinsic.ColorParse => "#ff0000",
		_ => "COOL",
	};

	[Theory]
	[MemberData( nameof( Callables ) )]
	public void RefusesTooFewArguments( string name )
	{
		var entry = Find( name );
		if ( entry.Required == 0 )
			return;

		var (source, kind) = CallOf( entry, Valid( entry, entry.Required - 1 ) );
		Assert.Contains( "takes", Errors( source, kind ) );
	}

	[Theory]
	[MemberData( nameof( Callables ) )]
	public void RefusesTooManyArguments( string name )
	{
		var entry = Find( name );
		var (source, kind) = CallOf( entry, Valid( entry, entry.Parameters.Count + 1 ) );
		Assert.Contains( "takes", Errors( source, kind ) );
	}

	[Theory]
	[MemberData( nameof( Callables ) )]
	public void RefusesAnArgumentOfTheWrongType( string name )
	{
		var entry = Find( name );
		if ( entry.Parameters.Count == 0 )
			return;

		var arguments = Valid( entry, entry.Parameters.Count );
		arguments[ 0 ] = entry.Parameters[ 0 ].Type.Kind == TypeKind.Number ? "true" : "1";
		var (source, kind) = CallOf( entry, arguments );
		Assert.Contains( "should be", Errors( source, kind ) );
	}

	[Theory]
	[InlineData( "Math.Abs( -0 )", "0" )]
	[InlineData( "Math.Sign( 0 )", "0" )]
	[InlineData( "Math.Floor( -0.5 )", "-1" )]
	[InlineData( "Math.Ceiling( -0.5 )", "0" )]
	[InlineData( "Math.Round( -2.5 )", "-2" )]
	[InlineData( "Math.Round( 3.5 )", "4" )]
	[InlineData( "Math.Frac( -0.25 )", "0.75" )]
	[InlineData( "Math.Min( 2, 2 )", "2" )]
	[InlineData( "Math.Max( -1, -2 )", "-1" )]
	[InlineData( "Math.Clamp( 5, 10, 0 )", "10" )]
	[InlineData( "Math.Lerp( 0, 10, 2 )", "10" )]
	[InlineData( "Math.Lerp( 0, 10, -1 )", "0" )]
	[InlineData( "Math.Sqrt( -4 )", "0" )]
	[InlineData( "Math.Pow( -8, 1 / 3 )", "0" )]
	[InlineData( "Math.Pow( 10, 400 )", "1000000000000000" )]
	[InlineData( "Math.Pow( 0, -1 )", "1000000000000000" )]
	[InlineData( "Math.Atan2( 0, 0 )", "0" )]
	[InlineData( "Math.Exp( 0 )", "1" )]
	[InlineData( "Math.Exp( 1000 )", "1000000000000000" )]
	[InlineData( "Math.Exp( -1000 )", "0" )]
	[InlineData( "Math.SmoothStep( 0, 1, -3 )", "0" )]
	[InlineData( "Math.SmoothStep( 0, 1, 3 )", "1" )]
	[InlineData( "Math.SmoothStep( 1, 0, 0.25 )", "0.8438" )]
	[InlineData( "Math.SmoothStep( 2, 2, 1.9 )", "0" )]
	[InlineData( "Math.SmoothStep( 2, 2, 2 )", "1" )]
	[InlineData( "Math.Noise( 0, 0 ) >= 0 and Math.Noise( 0, 0 ) <= 1", "true" )]
	[InlineData( "Math.Noise( 1e15 * 1e15, -1e15 * 1e15 ) >= 0", "true" )]
	[InlineData( "Math.Random( 1e15 * 1e15 * 1e15 ) < 1", "true" )]
	[InlineData( "Math.Random( 3 ) == Math.Random( 3 )", "true" )]
	[InlineData( "Math.Random( 3 ) == Math.Random( 4 )", "false" )]
	[InlineData( "Color.Parse( \"#f80\" )", "#ff8800" )]
	[InlineData( "Color.Parse( \"#FF880080\" )", "#ff880080" )]
	[InlineData( "Color.FromHsv( -120, 1, 1 )", "#0000ff" )]
	[InlineData( "Color.FromHsv( 720, 2, 2 )", "#ff0000" )]
	[InlineData( "Color.FromHsv( 0, 0, 0.5 )", "#808080" )]
	[InlineData( "Color.Lerp( Color.Black, Color.White, 2 )", "#ffffff" )]
	[InlineData( "Color( 2, -1, 0.5 )", "#ff0080" )]
	[InlineData( "Vector3.Zero.Normal", "Vector3( 0, 0, 0 )" )]
	[InlineData( "Vector3.Cross( Vector3.Up, Vector3.Up )", "Vector3( 0, 0, 0 )" )]
	[InlineData( "Vector3.Distance( Vector3.One, Vector3.One )", "0" )]
	[InlineData( "Vector3.Lerp( Vector3.Zero, Vector3.One, -5 )", "Vector3( 0, 0, 0 )" )]
	[InlineData( "Vector3( 1e15, 1e15, 1e15 ).Length", "1732050807568880" )]
	public void EdgeCasesGiveSafeValues( string expression, string expected ) => Assert.Equal( expected, Eval.Of( expression ) );

	[Fact]
	public void TrigonometryStaysFinite()
	{
		foreach ( var call in new[] { "Math.Tan( Math.PI / 2 )", "Math.Sin( 1e15 )", "Math.Cos( -1e15 )", "Math.Tan( 1e15 )" } )
			Assert.True( double.IsFinite( double.Parse( Eval.Of( call ), System.Globalization.CultureInfo.InvariantCulture ) ), call );
	}

	[Fact]
	public void TextThatIsntAColourReadsWhite() => Assert.Equal( "#ffffff", Eval.Script( "local s = \"nope\"\nlocal c = Color.Parse( s )" ).Variable( "c" ) );

	private sealed class Recorder : IScriptHost
	{
		public readonly List<(int Layer, int Count, double X)> Emitted = new();
		public void Emit( int layer, int count ) => Emitted.Add( (layer, count, double.NaN) );
		public void EmitAt( int layer, double x, double y, double z, int count ) => Emitted.Add( (layer, count, x) );
	}

	[Fact]
	public void EmitCountsAreHeldToABurst()
	{
		var (machine, _) = ScriptKit.Build( "local a = Sprite()\nlocal b = Sprite()\nfunction OnJump()\n\ta.Emit( 1000 )\n\tb.Emit( -5 )\n\tb.EmitAt( Vector3( 7, 0, 0 ), 2.9 )\nend" );
		var host = new Recorder();
		Assert.True( machine.RunEvent( ScriptEvent.Jump, new ScriptIO(), host ) );
		Assert.Equal( new[] { (0, (int)EffectLayer.MaxBurst, double.NaN), (1, 0, double.NaN), (1, 2, 7.0) }, host.Emitted );
	}
}
