using PlayerModels.Core.Script;
using Sandbox;

namespace PlayerModels.UI;

/// <summary>
/// The code editor's colours: VS Code's default dark theme (Dark Modern around the text, Dark+ for it) or its default
/// light one (Light Modern, Light+), so the code reads as players know it from the most used editor. The window's own
/// colours are in its stylesheet under the same two names; these are the ones the text itself is coloured with.
/// </summary>
public sealed class CodeTheme
{
	public static readonly CodeTheme Dark = new( "dark", "#cccccc", "#569cd6", "#c586c0", "#b5cea8", "#ce9178", "#6a9955", "#dcdcaa", "#4ec9b0", "#9cdcfe",
		"#d4d4d4", "#f14c4c", "#623315" );

	public static readonly CodeTheme Light = new( "light", "#3b3b3b", "#0000ff", "#af00db", "#098658", "#a31515", "#008000", "#795e26", "#267f99", "#001080",
		"#000000", "#e51400", "#e8d9a8" );

	/// <summary>Its name, also the class its window takes.</summary>
	public string Name { get; }

	/// <summary>The colour errors are underlined in, and the background of a find match or the bracket at the caret.</summary>
	public Color Error { get; }
	public Color Match { get; }

	/// <summary>The background of selected text.</summary>
	public Color Selection { get; }

	private readonly Color[] _classes;

	private CodeTheme( string name, string plain, string keyword, string control, string number, string text, string comment, string function, string type,
		string variable, string symbol, string error, string match )
	{
		Name = name;
		_classes = new[] { Parse( plain ), Parse( keyword ), Parse( control ), Parse( number ), Parse( text ), Parse( comment ), Parse( function ), Parse( type ),
			Parse( variable ), Parse( symbol ) };
		Error = Parse( error );
		Match = Parse( match ).WithAlpha( 0.6f );
		Selection = Parse( name == "light" ? "#add6ff" : "#264f78" );
	}

	/// <summary>The colour of a piece of code of <paramref name="kind"/>.</summary>
	public Color ColorOf( CodeClass kind ) => _classes[ (int)kind < _classes.Length ? (int)kind : 0 ];

	/// <summary>The theme the player chose: light, or dark (the default).</summary>
	public static CodeTheme Of( bool light ) => light ? Light : Dark;

	private static Color Parse( string hex ) => Color.Parse( hex ) ?? Color.White;
}
