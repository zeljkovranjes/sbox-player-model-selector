using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

public class FormatterTests
{
	private const string Messy = "-- Rain\nlocal   step=Math.Floor(time/100)   -- the clock\n\n\ncolor=palette[(step+i)%palette.Count]\nif i==0 then alpha=0.5 elseif i<3 then alpha= -1 else\n-- rest\nalpha=1 end";

	private const string Formatted = "-- Rain\nlocal step = Math.Floor( time / 100 ) -- the clock\n\ncolor = palette[ (step + i) % palette.Count ]\nif i == 0 then\n\talpha = 0.5\nelseif i < 3 then\n\talpha = -1\nelse\n\t-- rest\n\talpha = 1\nend\n";

	[Fact]
	public void FormatsCanonically() => Assert.Equal( Formatted, Formatter.Format( Messy ) );

	[Fact]
	public void MinifiesKeepingCommentsAndParagraphs() =>
		Assert.Equal( "-- Rain\nlocal step=Math.Floor(time/100)-- the clock\n;color=palette[(step+i)%palette.Count]\nif i==0 then\nalpha=0.5\nelseif i<3 then\nalpha=-1\nelse\n-- rest\nalpha=1\nend", Minifier.Minify( Messy ) );
}
