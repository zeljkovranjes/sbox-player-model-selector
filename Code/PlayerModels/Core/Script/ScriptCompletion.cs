using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>What a completion offers.</summary>
public enum CompletionKind { Keyword, Variable, Function, Method, Property, Constant, Input, Output, Event, Namespace, Choice, Type, Key, Shape, Effect, Attribute }

/// <summary>
/// One completion: what is shown (<see cref="Label"/>), what it is, its signature and one-line doc, the text it
/// inserts, the group it is listed under (<c>Stock</c>, <c>My stickers</c>), and an icon for the editor to draw (a key's
/// glyph <c>key:g</c>, a colour swatch <c>color:#ff0000</c>, a shape's thumbnail <c>shape:Heart</c> or a library item's
/// content id).
/// </summary>
public sealed record CompletionItem( string Label, CompletionKind Kind, string Detail, string Doc, string Insert, string Group = null, string Icon = null );

/// <summary>
/// Autocomplete for the code editor, from the same library table the compiler checks against: members after a dot
/// (<c>Math.</c>, <c>p.</c>, a layer's name, <c>Shapes.</c>), the player's own shapes, stickers, emblems and effects
/// inside <c>Mine( "</c>, keys inside a key's text, event handlers after <c>function</c>, [Parameter] and its options,
/// and otherwise the keywords, the names declared so far and the library names that work where the caret is. Filtered
/// by what is typed (its start first, then anywhere in the name).
/// </summary>
public static class ScriptCompletion
{
	/// <summary>The completions at <paramref name="caret"/> in <paramref name="source"/>.</summary>
	public static List<CompletionItem> At( string source, int caret, ScriptKind kind = ScriptKind.Effect, IPlayerLibrary library = null )
	{
		var context = ScriptContext.Of( source, caret, kind, library );
		var items = new List<CompletionItem>();
		if ( context.InComment )
			return items;

		if ( context.StringStart >= 0 )
			InString( context, items );
		else if ( context.Before.Count > 0 && context.Before[ ^1 ].Kind == TokenKind.Dot )
			AfterDot( context, items );
		else if ( context.Before.Count > 0 && context.Before[ ^1 ].Kind == TokenKind.Function )
			AfterFunction( context, items );
		else if ( InAttribute( context ) is { } attribute )
			AttributeOptions( attribute, items );
		else
			Anywhere( context, items );

		return Filtered( items, context.StringStart >= 0 ? context.StringSoFar : context.Word );
	}

	private static void InString( ScriptContext context, List<CompletionItem> items )
	{
		var before = context.Before;
		var n = before.Count;
		if ( n >= 1 && before[ ^1 ].Kind == TokenKind.LeftParen )
		{
			var callee = context.ChainBefore( n - 1 );
			var name = string.Join( ".", callee );
			var kind = name switch
			{
				"Shapes.Mine" => LibraryItemKind.Shape,
				"Stickers.Mine" => LibraryItemKind.Sticker,
				"Emblems.Mine" => LibraryItemKind.Emblem,
				"Effects.Mine" => LibraryItemKind.Effect,
				_ => (LibraryItemKind)(-1),
			};

			if ( kind >= 0 )
			{
				LibraryItems( context.Library, kind, items );
				return;
			}

			// A weapon design's: the parts of the weapon held, the stock props
			if ( name is "Weapon.HidePart" or "Weapon.AddProp" )
			{
				LibraryItems( context.Library, name == "Weapon.HidePart" ? LibraryItemKind.Part : LibraryItemKind.Prop, items );
				return;
			}

			if ( name == "Effects.Preset" )
			{
				foreach ( var preset in EffectPresets.All )
					items.Add( new CompletionItem( preset.Name, CompletionKind.Effect, "Preset", "A stock effect.", preset.Name, "Presets" ) );

				return;
			}
		}

		// A prop's part or model: prop.Part = "..", prop.Model = ".."
		if ( n >= 3 && before[ ^1 ].Kind == TokenKind.Assign && before[ ^3 ].Kind == TokenKind.Dot && context.Text( before[ ^2 ] ) is "Part" or "Model"
			&& context.Resolve( context.ChainBefore( n - 3 ), context.Caret ).Type?.Kind == TypeKind.WeaponProp )
		{
			LibraryItems( context.Library, context.Text( before[ ^2 ] ) == "Part" ? LibraryItemKind.Part : LibraryItemKind.Prop, items );
			return;
		}

		// A key: Effect.Key = "..", or a comparison with a key (OnKey's key == "..")
		var isKey = n >= 3 && before[ ^1 ].Kind == TokenKind.Assign && context.Text( before[ ^2 ] ) == "Key" && before[ ^3 ].Kind == TokenKind.Dot
			|| n >= 2 && before[ ^1 ].Kind is TokenKind.Equal or TokenKind.NotEqual && before[ ^2 ].Kind == TokenKind.Name
			&& context.Resolve( new[] { context.Text( before[ ^2 ] ) }, context.Caret ).Type?.Kind == TypeKind.Key;

		if ( !isKey )
			return;

		foreach ( var key in Effect.Keys )
		{
			var entry = ScriptLibrary.Key( key );
			items.Add( new CompletionItem( key, CompletionKind.Key, entry.Doc, entry.Doc, key, "Keys", "key:" + key ) );
		}
	}

	private static void LibraryItems( IPlayerLibrary library, LibraryItemKind kind, List<CompletionItem> items )
	{
		foreach ( var item in library?.Items ?? Array.Empty<LibraryItem>() )
		{
			if ( item.Kind != kind )
				continue;

			if ( kind is LibraryItemKind.Part or LibraryItemKind.Prop )
			{
				var (detail, doc, group) = kind == LibraryItemKind.Part
					? ("Part", "A part of the weapon you hold: it can be hidden, or a prop can take its place or go on it.", "This weapon's parts")
					: ("Prop", "A stock prop, by its model's path.", "Props");
				items.Add( new CompletionItem( item.Name, CompletionKind.Constant, detail, doc, item.Name, group ) );
				continue;
			}

			var mine = kind switch { LibraryItemKind.Shape => "My shapes", LibraryItemKind.Sticker => "My stickers", LibraryItemKind.Emblem => "My emblems", _ => "My effects" };
			items.Add( new CompletionItem( item.Name, kind == LibraryItemKind.Effect ? CompletionKind.Effect : CompletionKind.Shape, kind.ToString(), $"One of your own {kind.ToString().ToLowerInvariant()}s.",
				item.Name, mine, item.ContentId ) );
		}
	}

	private static void AfterDot( ScriptContext context, List<CompletionItem> items )
	{
		var chain = context.ChainBefore( context.Before.Count - 1 );
		var (space, type, _) = context.Resolve( chain, context.Caret );
		if ( space is not null )
		{
			foreach ( var entry in ScriptLibrary.NamespaceMembers( space ) )
				items.Add( Item( entry, space == "Shapes" && entry.Kind == EntryKind.Constant ? "Stock" : null ) );

			return;
		}

		if ( type is null )
			return;

		foreach ( var entry in ScriptLibrary.MembersOf( type.Kind ) )
		{
			if ( (entry.Where & context.Place) != 0 || entry.Where == ScriptPlace.None )
				items.Add( Item( entry ) );
		}
	}

	private static void AfterFunction( ScriptContext context, List<CompletionItem> items )
	{
		if ( context.Kind == ScriptKind.Effect )
		{
			foreach ( var entry in ScriptLibrary.AllEvents )
			{
				var parameters = new List<string>();
				foreach ( var parameter in entry.Parameters )
					parameters.Add( parameter.Name );

				var insert = parameters.Count > 0 ? $"{entry.Name}( {string.Join( ", ", parameters )} )" : entry.Name + "()";
				items.Add( new CompletionItem( entry.Name, CompletionKind.Event, entry.Signature(), entry.Doc, insert, "Events" ) );
			}
		}

		foreach ( var symbol in context.Visible( context.Caret ) )
		{
			if ( symbol.Type?.Kind == TypeKind.Layer )
				items.Add( new CompletionItem( symbol.Name, CompletionKind.Variable, "Layer", "Write its particle function: function " + symbol.Name + ".Particle( p )", symbol.Name + ".Particle( p )", "Layers" ) );
		}
	}

	// The attribute being written at the caret: "[Parameter(" with its options typed so far, or "[" alone (null for neither)
	private static string InAttribute( ScriptContext context )
	{
		var before = context.Before;
		for ( var k = before.Count - 1; k >= 0; k-- )
		{
			var kind = before[ k ].Kind;
			if ( kind == TokenKind.LeftBracket && before[ k ].NewlineBefore )
				return k + 2 < before.Count && before[ k + 2 ].Kind == TokenKind.LeftParen ? "options" : k == before.Count - 1 ? "name" : null;

			if ( kind is TokenKind.RightBracket or TokenKind.Local )
				return null;
		}

		return null;
	}

	private static void AttributeOptions( string where, List<CompletionItem> items )
	{
		if ( where == "name" )
		{
			items.Add( Item( ScriptLibrary.Attribute( "Parameter" ) ) );
			return;
		}

		foreach ( var option in Compiler.ParameterArguments )
		{
			var entry = ScriptLibrary.Attribute( "Parameter." + option );
			items.Add( new CompletionItem( option, CompletionKind.Attribute, $"{option}: {entry.Type}", entry.Doc, option + " = ", "Parameter" ) );
		}
	}

	private static void Anywhere( ScriptContext context, List<CompletionItem> items )
	{
		foreach ( var entry in ScriptLibrary.All )
		{
			if ( entry.Kind == EntryKind.Keyword )
				items.Add( Item( entry, "Keywords" ) );
		}

		foreach ( var symbol in context.Visible( context.WordStart ) )
			items.Add( new CompletionItem( symbol.Name, symbol.Kind == "function" ? CompletionKind.Function : CompletionKind.Variable, $"{symbol.Name}: {symbol.Type}", $"Your {symbol.Kind}.", symbol.Name, "Yours" ) );

		var place = context.Place;
		foreach ( var entry in ScriptLibrary.TopLevel() )
		{
			if ( entry.Kind is EntryKind.Namespace or EntryKind.Enum || (entry.Where & place) != 0 )
				items.Add( Item( entry ) );
		}
	}

	private static CompletionItem Item( LibraryEntry entry, string group = null )
	{
		var kind = entry.Kind switch
		{
			EntryKind.Keyword => CompletionKind.Keyword,
			EntryKind.Function => CompletionKind.Function,
			EntryKind.Method => CompletionKind.Method,
			EntryKind.Property => CompletionKind.Property,
			EntryKind.Constant when entry.Type?.Kind == TypeKind.Enum => CompletionKind.Choice,
			EntryKind.Constant when entry.Type?.Kind == TypeKind.Shape => CompletionKind.Shape,
			EntryKind.Constant => CompletionKind.Constant,
			EntryKind.Input => CompletionKind.Input,
			EntryKind.Output => CompletionKind.Output,
			EntryKind.Event => CompletionKind.Event,
			EntryKind.Attribute => CompletionKind.Attribute,
			EntryKind.Type => CompletionKind.Type,
			_ => CompletionKind.Namespace,
		};

		return new CompletionItem( entry.Member, kind, entry.Signature(), entry.Doc, entry.Member, group ?? entry.Category, Icon( entry ) );
	}

	/// <summary>The icon an entry is drawn with in a list or a tooltip: a colour swatch, a shape's thumbnail, a key's glyph; null for none.</summary>
	public static string Icon( LibraryEntry entry )
	{
		if ( entry.Kind == EntryKind.Constant && entry.Type?.Kind == TypeKind.Color )
			return "color:" + ScriptValue.ColorText( entry.Value[ 0 ], entry.Value[ 1 ], entry.Value[ 2 ], entry.Value[ 3 ] );

		if ( entry.Kind == EntryKind.Constant && entry.Type?.Kind is TypeKind.Shape or TypeKind.Picture )
			return "shape:" + entry.Member;

		return entry.Kind == EntryKind.Key ? "key:" + entry.Name : null;
	}

	// What is typed so far: names starting with it first (case aside), then those holding it; each once
	private static List<CompletionItem> Filtered( List<CompletionItem> items, string typed )
	{
		typed ??= "";
		var starting = new List<CompletionItem>();
		var holding = new List<CompletionItem>();
		var seen = new HashSet<string>();
		foreach ( var item in items )
		{
			if ( !seen.Add( item.Group + "\n" + item.Label ) )
				continue;

			if ( item.Label.StartsWith( typed, StringComparison.OrdinalIgnoreCase ) )
				starting.Add( item );
			else if ( typed.Length > 1 && item.Label.Contains( typed, StringComparison.OrdinalIgnoreCase ) )
				holding.Add( item );
		}

		starting.Sort( ( a, b ) => string.Compare( a.Label, b.Label, StringComparison.OrdinalIgnoreCase ) );
		holding.Sort( ( a, b ) => string.Compare( a.Label, b.Label, StringComparison.OrdinalIgnoreCase ) );
		starting.AddRange( holding );
		return starting;
	}
}
