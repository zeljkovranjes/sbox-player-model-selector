using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PlayerModels.Core.Script;

/// <summary>
/// The language reference, written from the library table (the same entries the compiler, completion and hover use),
/// so it can't fall behind: every keyword, type, function, property, input, output, event, named choice and key, each
/// with its signature, one-line doc, parameters and an example (examples are fenced <c>lua effect</c>, <c>lua shape</c>
/// or <c>lua sticker</c>, for the kind of script they run as).
/// </summary>
public static class ScriptReference
{
	// An effect needs a layer: an example that makes none (a sum, a colour) runs with one added after it
	private static readonly Regex MakesLayer = new( @"\b(Sprite|Light|Trail|Beam|Ghost)\(\)|Effect\.Include" );

	/// <summary>
	/// <paramref name="example"/> as it runs, in the playground and the tests alike: an effect example that makes no layer
	/// has one added after it, so it builds and shows; anything else as it is.
	/// </summary>
	public static string Runnable( string example, ScriptKind kind ) =>
		kind == ScriptKind.Effect && !MakesLayer.IsMatch( example ) ? example + "\nlocal exampleLayer = Sprite()" : example;

	/// <summary>The fence a runnable example of <paramref name="kind"/> starts with.</summary>
	public static string Fence( ScriptKind kind ) => "```lua " + kind.ToString().ToLowerInvariant();

	/// <summary>The reference, as Markdown.</summary>
	public static string Markdown()
	{
		var text = new StringBuilder();
		foreach ( var category in ScriptLibrary.Categories )
		{
			text.Append( "### " ).Append( category ).Append( "\n\n" );
			switch ( category )
			{
				case "Choices": Choices( text ); break;
				case "Keys": Keys( text ); break;
				default:
					foreach ( var entry in ScriptLibrary.All )
					{
						if ( entry.Category == category && !IsListedTogether( entry ) )
							Entry( text, entry );
					}

					if ( category == "Shape" )
						Names( text, "Stock shapes", "Shapes.", Enum.GetNames<StandardShape>() );

					if ( category == "Layer" )
						Names( text, "Pictures", "Picture.", Enum.GetNames<LayerPicture>() );

					break;
			}
		}

		return text.ToString();
	}

	// Stock shapes and pictures are listed by name together, not one entry each
	private static bool IsListedTogether( LibraryEntry entry ) =>
		entry.Kind == EntryKind.Constant && entry.Type?.Kind is TypeKind.Shape or TypeKind.Picture && entry.Owner is "Shapes" or "Picture";

	private static void Entry( StringBuilder text, LibraryEntry entry )
	{
		text.Append( "#### `" ).Append( entry.Signature() ).Append( "`\n\n" ).Append( entry.Doc ).Append( '\n' );
		if ( entry.Kind == EntryKind.Event )
			text.Append( '\n' ).Append( entry.RatePerSecond > 0 ? $"At most {entry.RatePerSecond} a second for each player." : "Every frame." ).Append( '\n' );

		if ( entry.Kind is EntryKind.Function or EntryKind.Method or EntryKind.Event or EntryKind.Attribute && entry.Parameters.Count > 0 )
		{
			text.Append( "\n| Parameter | Type | Range | What it is |\n|---|---|---|---|\n" );
			foreach ( var parameter in entry.Parameters )
			{
				var optional = parameter.Default is null ? "" : $" (optional{(parameter.Type.Kind == TypeKind.Number ? ", " + ScriptValue.NumberText( parameter.Default[ 0 ] ) : "")})";
				text.Append( $"| `{parameter.Name}` | {parameter.Type} | {parameter.Range ?? "any"} | {parameter.Doc}{optional} |\n" );
			}
		}

		if ( entry.Kind == EntryKind.Property )
		{
			var where = entry.IsWritable ? WhereText( entry.WriteWhere ) : "read only";
			text.Append( "\nSet: " ).Append( where ).Append( ".\n" );
		}

		if ( entry.Example is not null )
			text.Append( '\n' ).Append( Fence( entry.ExampleKind ) ).Append( '\n' ).Append( entry.Example ).Append( "\n```\n" );

		text.Append( '\n' );
	}

	private static string WhereText( ScriptPlace place ) => place switch
	{
		ScriptPlace.EffectBuild => "while the effect is built (at the top of the script)",
		ScriptPlace.Effect => "while it is built and while it runs (in its functions)",
		ScriptPlace.Build => "while the shape or effect is built",
		ScriptPlace.EffectRun => "while it runs (in its functions)",
		ScriptPlace.StickerRun => "in a sticker script",
		ScriptPlace.SkinRun => "in a weapon skin script",
		ScriptPlace.Element => "in a sticker or weapon skin script",
		_ => "anywhere",
	};

	private static void Names( StringBuilder text, string title, string prefix, IEnumerable<string> names )
	{
		text.Append( "#### " ).Append( title ).Append( "\n\n" );
		var first = true;
		foreach ( var name in names )
		{
			text.Append( first ? "" : ", " ).Append( '`' ).Append( prefix ).Append( name ).Append( '`' );
			first = false;
		}

		text.Append( "\n\n" );
	}

	private static void Choices( StringBuilder text )
	{
		foreach ( var entry in ScriptLibrary.All )
		{
			if ( entry.Kind != EntryKind.Enum )
				continue;

			text.Append( "#### `" ).Append( entry.Name ).Append( "`\n\n" ).Append( entry.Doc ).Append( "\n\n" );
			var first = true;
			foreach ( var member in entry.Enum.Members )
			{
				text.Append( first ? "" : ", " ).Append( '`' ).Append( entry.Name ).Append( '.' ).Append( member ).Append( '`' );
				first = false;
			}

			text.Append( "\n\n" ).Append( Fence( entry.ExampleKind ) ).Append( '\n' ).Append( entry.Example ).Append( "\n```\n\n" );
		}
	}

	private static void Keys( StringBuilder text )
	{
		text.Append( "The keys an effect can be shown with, written as text (any other text is a compile error):\n\n" );
		var first = true;
		foreach ( var key in Effect.Keys )
		{
			text.Append( first ? "" : ", " ).Append( "`\"" ).Append( key ).Append( "\"`" );
			first = false;
		}

		text.Append( "\n\n" ).Append( Fence( ScriptKind.Effect ) ).Append( "\nlocal glow = Sprite()\nEffect.Key = \"g\"\n```\n" );
	}
}
