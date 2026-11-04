using System;
using System.IO;
using System.Text.RegularExpressions;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>
/// docs/effects-scripting.md: its reference is the one generated from the library (run with UPDATE_DOCS=1 to write it),
/// and every example in it runs and gives what it says.
/// </summary>
public class DocTests
{
	private const string Start = "<!-- reference:start -->";
	private const string End = "<!-- reference:end -->";
	private static readonly Regex Example = new( @"^```lua (effect|shape|sticker)\n(.*?)^```", RegexOptions.Multiline | RegexOptions.Singleline );

	private static string DocPath()
	{
		var directory = AppContext.BaseDirectory;
		while ( directory is not null && !File.Exists( Path.Combine( directory, "docs", "effects-scripting.md" ) ) )
			directory = Path.GetDirectoryName( directory );

		Assert.NotNull( directory );
		return Path.Combine( directory, "docs", "effects-scripting.md" );
	}

	[Fact]
	public void ReferenceIsUpToDate()
	{
		var path = DocPath();
		var doc = File.ReadAllText( path );
		var from = doc.IndexOf( Start, StringComparison.Ordinal ) + Start.Length;
		var to = doc.IndexOf( End, StringComparison.Ordinal );
		var generated = "\n\n" + ScriptReference.Markdown();
		if ( Environment.GetEnvironmentVariable( "UPDATE_DOCS" ) == "1" )
		{
			File.WriteAllText( path, doc[ ..from ] + generated + doc[ to.. ] );
			return;
		}

		Assert.True( doc[ from..to ] == generated, "docs/effects-scripting.md's reference is out of date: run the tests with UPDATE_DOCS=1" );
	}

	[Fact]
	public void EveryExampleRuns()
	{
		var doc = File.ReadAllText( DocPath() );
		var examples = 0;
		var checks = 0;
		foreach ( Match match in Example.Matches( doc ) )
		{
			checks += ExampleRunner.Run( match.Groups[ 2 ].Value, Enum.Parse<ScriptKind>( match.Groups[ 1 ].Value, true ) );
			examples++;
		}

		Assert.True( examples >= 250, $"Only {examples} examples" );
		Console.WriteLine( $"Doc examples: {examples} run, {checks} expectations checked" );
	}
}
