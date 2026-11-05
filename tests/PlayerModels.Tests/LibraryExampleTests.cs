using System.Collections.Generic;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>Every library entry has a doc and an example, and every example runs and gives what it says.</summary>
public class LibraryExampleTests
{
	public static IEnumerable<object[]> Entries()
	{
		for ( var i = 0; i < ScriptLibrary.All.Count; i++ )
			yield return new object[] { i, ScriptLibrary.All[ i ].Name };
	}

	[Theory]
	[MemberData( nameof( Entries ) )]
	public void ExampleRuns( int index, string name )
	{
		var entry = ScriptLibrary.All[ index ];
		Assert.False( string.IsNullOrWhiteSpace( entry.Doc ), name + " has no doc" );
		if ( entry.Kind is EntryKind.Namespace )
			return;

		Assert.False( string.IsNullOrWhiteSpace( entry.Example ), name + " has no example" );
		ExampleRunner.Run( entry.Example, entry.ExampleKind );
	}
}
