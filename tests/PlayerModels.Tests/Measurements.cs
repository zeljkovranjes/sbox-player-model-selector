using Xunit;

namespace PlayerModels.Tests;

/// <summary>
/// Tests that time or count allocations run alone, after the others: another test's garbage collection mid-measurement
/// would show up in theirs.
/// </summary>
[CollectionDefinition( Name, DisableParallelization = true )]
public class Measurements
{
	public const string Name = "Measurements";
}
