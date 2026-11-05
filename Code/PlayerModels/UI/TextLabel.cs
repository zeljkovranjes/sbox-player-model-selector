using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// Text shown exactly as it is: never read as markup and never looked up as a translation (a label starting with
/// <c>#</c> otherwise is), for text players write (scripts, names, errors).
/// </summary>
public sealed class TextLabel : Label
{
	public TextLabel()
	{
		Tokenize = false;
		IsRich = false;
	}
}
