using System.Collections.Generic;

namespace PlayerModels.Core.Script;

public static partial class ScriptLibrary
{
	private static void AddLanguage( List<LibraryEntry> entries )
	{
		Keyword( entries, "local", "Makes a variable: local name = value (or local name: type = value). At the top of a script it lasts while the script does.", "local size = 2\nlocal doubled = size * 2 --> 4" );
		Keyword( entries, "function", "Makes a function: function name( a: number ): number ... end. Event handlers (OnJump) and layer.Particle are functions too.", "local function twice( x: number ): number\n\treturn x * 2\nend\nlocal four = twice( 2 ) --> 4" );
		Keyword( entries, "end", "Closes a function, if, for or while.", "local n = 0\nif true then\n\tn = 1\nend\nlocal one = n --> 1" );
		Keyword( entries, "if", "Runs a block when a condition is true: if c then ... elseif d then ... else ... end. As a value: if c then a else b.", "local big = if 5 > 3 then \"yes\" else \"no\" --> \"yes\"" );
		Keyword( entries, "then", "Follows an if or elseif condition.", "local n = 0\nif n == 0 then\n\tn = 2\nend\nlocal two = n --> 2" );
		Keyword( entries, "elseif", "Another condition, tried when the ones before were false.", "local n = 5\nlocal size = 0\nif n < 3 then\n\tsize = 1\nelseif n < 10 then\n\tsize = 2\nend\nlocal picked = size --> 2" );
		Keyword( entries, "else", "What runs when no condition was true.", "local n = 5\nlocal small = false\nif n < 3 then\n\tsmall = true\nelse\n\tsmall = false\nend\nlocal answer = small --> false" );
		Keyword( entries, "for", "Counts: for i = from, to[, step] do ... end, both ends included (step 1 unless given).", "local sum = 0\nfor k = 1, 4 do\n\tsum = sum + k\nend\nlocal total = sum --> 10" );
		Keyword( entries, "do", "Starts the body of a for or while loop.", "local n = 0\nwhile n < 3 do\n\tn = n + 1\nend\nlocal three = n --> 3" );
		Keyword( entries, "while", "Repeats a block while a condition is true (stopped by the run's instruction budget).", "local n = 1\nwhile n < 100 do\n\tn = n * 2\nend\nlocal power = n --> 128" );
		Keyword( entries, "return", "Ends a function, giving back a value; it is the last statement of its block.", "local function half( x: number ): number\n\treturn x / 2\nend\nlocal h = half( 3 ) --> 1.5" );
		Keyword( entries, "break", "Leaves the loop it is in.", "local n = 0\nfor k = 0, 100 do\n\tif k == 5 then\n\t\tbreak\n\tend\n\tn = k\nend\nlocal last = n --> 4" );
		Keyword( entries, "and", "True when both sides are (the right side only checked when the left is true).", "local both = true and false --> false" );
		Keyword( entries, "or", "True when either side is (the right side only checked when the left is false).", "local either = true or false --> true" );
		Keyword( entries, "not", "The opposite of a bool.", "local opposite = not true --> false" );
		Keyword( entries, "true", "The bool for yes.", "local yes = true --> true" );
		Keyword( entries, "false", "The bool for no.", "local no = false --> false" );

		Type( entries, "number", "A number (64-bit, like C#'s double). Division by zero gives 0, and nothing is ever NaN or infinite.", "local half = 1 / 2 --> 0.5" );
		Type( entries, "bool", "true or false. Conditions must be bools (no Lua truthiness): if n > 0, not if n.", "local more = 3 > 2 --> true" );
		Type( entries, "string", "Text in quotes, \"like this\" or 'like this' (at most 256 characters). Text never changes while a script runs.", "local name = \"Sparks\" --> \"Sparks\"" );
		Type( entries, "Color", "A colour: red, green, blue, alpha from 0 to 1. + - * / work on each part.", "local dim = Color.White * 0.5 --> #80808080" );
		Type( entries, "Vector3", "Three numbers: a place or a direction. + - * / work like s&box's Vector3.", "local up2 = Vector3.Up * 2 --> Vector3( 0, 0, 2 )" );
		Type( entries, "Key", "A key that can show an effect, written as text: \"g\", \"f1\", \"mouse4\". Any other text is an error.", "Effect.Key = \"g\"", ScriptKind.Effect );
		Type( entries, "Shape", "A standard shape (Shapes.Heart) or one the script drew (Shapes.New).", "local s: Shape = Shapes.Heart --> Shapes.Heart" );
		Type( entries, "Picture", "What a sprite looks like: a curated picture (Picture.Flame) or any shape.", "local look: Picture = Picture.Flame --> Picture.Flame" );
		Type( entries, "Layer", "A layer of the effect, made by Sprite(), Light(), Trail(), Beam() or Ghost().", "local glow: Layer = Sprite()", ScriptKind.Effect );
		Type( entries, "Effect", "An effect: this one (Effect), or one to include (Effects.Preset).", "Effect.Include( Effects.Preset( \"Hearts\" ) )", ScriptKind.Effect );
		Type( entries, "EmblemLayer", "One layer of an emblem, made by shape.AddLayer.", "local badge = Shapes.New( \"Badge\" )\nlocal star: EmblemLayer = badge.AddLayer( Shapes.Star5 )", ScriptKind.Effect );
		Type( entries, "list", "{ a, b, c }: items of one type, numbered from 0, written {T} as a type. An item changes in place with list[ i ] = value (every variable holding the list sees it); a list keeps its size.", "local odd: {number} = { 1, 3, 5 }\nodd[ 1 ] = 4\nlocal second = odd[ 1 ] --> 4" );
		Type( entries, "function", "A function as a value, written (A, B) -> R as a type.", "local function twice( x: number ): number\n\treturn x * 2\nend\nlocal f: (number) -> number = twice\nlocal six = f( 3 ) --> 6" );
	}

	private static void AddParameters( List<LibraryEntry> entries )
	{
		const string parameters = "Parameters";
		var text = ScriptType.String;
		entries.Add( new LibraryEntry
		{
			Name = "Parameter", Kind = EntryKind.Attribute, Category = parameters, ExampleKind = ScriptKind.Effect,
			Doc = "Makes a top-level local an option players adjust in the Effects tab (the widget comes from its type); the script's value is the default.",
			Example = "[Parameter( \"Glow size\", Min = 0.5, Max = 10, Step = 0.1, Group = \"Look\", Tip = \"How big each glow is\" )]\nlocal glowSize = 2.6\n\n[Parameter]\nlocal glowColor = Color.Parse( \"#7af0ff\" )\nlocal size = glowSize --> 2.6",
			Parameters = new[] { P( "title", text, "What the option is called; the variable's name in words (glowSize: \"Glow size\") when left out", null, new[] { 0.0 } ) },
		} );

		var options = new (string Name, ScriptType Type, string Doc, string Example)[]
		{
			("Min", N, "The least a number (or each part of a Vector3) can be set to; give Max too.", "[Parameter( Min = 0, Max = 1 )]\nlocal strength = 0.5"),
			("Max", N, "The most a number (or each part of a Vector3) can be set to; give Min too.", "[Parameter( Min = -10, Max = 10 )]\nlocal height = 2"),
			("Step", N, "How far one notch of its slider moves; a whole Step of 1 or more makes a whole-number stepper.", "[Parameter( Min = 1, Max = 8, Step = 1 )]\nlocal points = 5"),
			("Group", text, "The fold it is shown in, with the others of its group.", "[Parameter( Group = \"Look\" )]\nlocal tint = Color.White"),
			("Tip", text, "What it does, shown when hovering it.", "[Parameter( Tip = \"How fast it spins\" )]\nlocal spin = 1"),
			("Choices", ScriptType.ListOf( text ), "For text: the choices it is picked from, as a dropdown.", "[Parameter( Choices = { \"Spin\", \"Pulse\" } )]\nlocal motion = \"Spin\""),
			("Order", N, "Where it is shown among the others (lowest first); they are in the script's order otherwise.", "[Parameter( Order = 1 )]\nlocal second = true"),
		};

		foreach ( var (name, type, doc, example) in options )
		{
			entries.Add( new LibraryEntry
			{
				Name = "Parameter." + name, Kind = EntryKind.Attribute, Category = parameters, Type = type, Doc = doc, Example = example,
				ExampleKind = ScriptKind.Effect,
			} );
		}
	}

	private static void Keyword( List<LibraryEntry> entries, string name, string doc, string example ) => entries.Add( new LibraryEntry
	{
		Name = name, Kind = EntryKind.Keyword, Category = "Keywords", Doc = doc, Example = example,
	} );

	private static void Type( List<LibraryEntry> entries, string name, string doc, string example, ScriptKind kind = ScriptKind.Sticker ) => entries.Add( new LibraryEntry
	{
		Name = name, Kind = EntryKind.Type, Category = "Types", Doc = doc, Example = example, ExampleKind = kind,
		Type = ScriptType.Named.TryGetValue( name, out var type ) ? type : null,
	} );

	private static void AddChoices( List<LibraryEntry> entries )
	{
		foreach ( var choices in ScriptEnums.All )
		{
			var first = choices.Members[ choices.Members.Count > 1 ? 1 : 0 ];
			entries.Add( new LibraryEntry
			{
				Name = choices.Name, Kind = EntryKind.Enum, Category = "Choices", Doc = choices.Doc, Enum = choices, Type = choices.Type,
				Example = $"local picked = {choices.Name}.{first} --> {choices.Name}.{first}",
			} );

			for ( var i = 0; i < choices.Members.Count; i++ )
			{
				var member = choices.Members[ i ];
				entries.Add( Constant( $"{choices.Name}.{member}", choices.Type, "Choices", choices.Doc, $"local picked = {choices.Name}.{member} --> {choices.Name}.{member}", i ) );
			}
		}

		for ( var i = 0; i < Effect.Keys.Count; i++ )
		{
			var key = Effect.Keys[ i ];
			var what = key.StartsWith( "mouse" ) ? $"Mouse button {key[ 5.. ]}" : key.Length > 1 ? $"The {key.ToUpperInvariant()} key" : $"The {key.ToUpperInvariant()} key";
			entries.Add( new LibraryEntry
			{
				Name = key, Kind = EntryKind.Key, Category = "Keys", Type = ScriptType.Key, Doc = what + ".", Value = new double[] { i },
				Example = $"Effect.Key = \"{key}\"", ExampleKind = ScriptKind.Effect,
			} );
		}
	}
}
