using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// The language's whole fixed library, as one table of <see cref="LibraryEntry"/>: the only names a script can reach
/// (no engine, files, network or other players), and the single source for the compiler, completion, hover, signature
/// help and the reference docs. Built once; never changed after.
/// </summary>
public static partial class ScriptLibrary
{
	/// <summary>Every entry, in the reference's order.</summary>
	public static IReadOnlyList<LibraryEntry> All { get; }

	/// <summary>The categories, in the reference's order.</summary>
	public static readonly string[] Categories =
	{
		"Keywords", "Types", "Parameters", "Math", "Color", "Vector3", "Lists", "Effect", "Layer", "Particle", "Shape", "Emblem",
		"Inputs", "Sticker", "Events", "Shot", "Choices", "Keys",
	};

	/// <summary>The reserved script variables of a sticker: its palette (a list), highlight and rest colours.</summary>
	internal const int PaletteSlot = 0;
	internal const int HighlightSlot = 2;
	internal const int RestSlot = 6;
	internal const int ReservedSlots = 10;

	private static readonly Dictionary<string, LibraryEntry> Globals = new();
	private static readonly Dictionary<string, Dictionary<string, LibraryEntry>> Namespaces = new();
	private static readonly Dictionary<TypeKind, Dictionary<string, LibraryEntry>> Members = new();
	private static readonly Dictionary<string, LibraryEntry> Events = new();
	private static readonly Dictionary<string, LibraryEntry> Keywords = new();
	private static readonly Dictionary<string, LibraryEntry> Types = new();
	private static readonly Dictionary<string, LibraryEntry> Keys = new();
	private static readonly Dictionary<string, LibraryEntry> Attributes = new();

	static ScriptLibrary()
	{
		var entries = new List<LibraryEntry>();
		AddLanguage( entries );
		AddParameters( entries );
		AddMath( entries );
		AddColor( entries );
		AddVector( entries );
		AddEffects( entries );
		AddShapes( entries );
		AddInputs( entries );
		AddEvents( entries );
		AddChoices( entries );
		All = entries;

		foreach ( var entry in entries )
			Index( entry );
	}

	private static void Index( LibraryEntry entry )
	{
		switch ( entry.Kind )
		{
			case EntryKind.Keyword: Keywords[ entry.Name ] = entry; break;
			case EntryKind.Type: Types[ entry.Name ] = entry; break;
			case EntryKind.Event: Events[ entry.Name ] = entry; break;
			case EntryKind.Key: Keys[ entry.Name ] = entry; break;
			case EntryKind.Attribute: Attributes[ entry.Name ] = entry; break;
			case EntryKind.Namespace or EntryKind.Enum:
				Namespaces.TryAdd( entry.Name, new Dictionary<string, LibraryEntry>() );
				Globals.TryAdd( "namespace:" + entry.Name, entry );
				break;
			case EntryKind.Property or EntryKind.Method:
			{
				var owner = OwnerKind( entry.Owner );
				if ( !Members.TryGetValue( owner, out var members ) )
					Members[ owner ] = members = new Dictionary<string, LibraryEntry>();

				members[ entry.Member ] = entry;
				break;
			}

			default:
				if ( entry.Owner is null )
				{
					Globals[ entry.Name ] = entry;
					break;
				}

				if ( !Namespaces.TryGetValue( entry.Owner, out var space ) )
					Namespaces[ entry.Owner ] = space = new Dictionary<string, LibraryEntry>();

				space[ entry.Member ] = entry;
				break;
		}
	}

	private static TypeKind OwnerKind( string owner ) => owner switch
	{
		"List" => TypeKind.List,
		_ => Enum.Parse<TypeKind>( owner ),
	};

	/// <summary>A global name: a function (<c>Sprite</c>, <c>Color</c>), an input, an output or a context value; null for none.</summary>
	public static LibraryEntry Global( string name ) => name is not null && Globals.TryGetValue( name, out var entry ) ? entry : null;

	/// <summary>The namespace or choice set called <paramref name="name"/> (<c>Math</c>, <c>Joint</c>); null for none.</summary>
	public static LibraryEntry Namespace( string name ) => Global( "namespace:" + name );

	/// <summary>A member of namespace <paramref name="space"/>: <c>Math.Floor</c>, <c>Joint.Head</c>; null for none.</summary>
	public static LibraryEntry InNamespace( string space, string name ) =>
		Namespaces.TryGetValue( space, out var members ) && members.TryGetValue( name, out var entry ) ? entry : null;

	/// <summary>Every member of namespace <paramref name="space"/>.</summary>
	public static IEnumerable<LibraryEntry> NamespaceMembers( string space ) =>
		Namespaces.TryGetValue( space, out var members ) ? members.Values : Array.Empty<LibraryEntry>();

	/// <summary>A property or method of values of <paramref name="type"/>; null for none.</summary>
	public static LibraryEntry Member( TypeKind type, string name ) =>
		Members.TryGetValue( type, out var members ) && members.TryGetValue( name, out var entry ) ? entry : null;

	/// <summary>Every property and method of values of <paramref name="type"/>.</summary>
	public static IEnumerable<LibraryEntry> MembersOf( TypeKind type ) =>
		Members.TryGetValue( type, out var members ) ? members.Values : Array.Empty<LibraryEntry>();

	/// <summary>The handler <paramref name="name"/> (<c>OnLand</c>); null for none.</summary>
	public static LibraryEntry Event( string name ) => name is not null && Events.TryGetValue( name, out var entry ) ? entry : null;

	/// <summary>Every event handler.</summary>
	public static IEnumerable<LibraryEntry> AllEvents => Events.Values;

	/// <summary>The keyword <paramref name="name"/>; null for none.</summary>
	public static LibraryEntry Keyword( string name ) => name is not null && Keywords.TryGetValue( name, out var entry ) ? entry : null;

	/// <summary>The attribute (<c>Parameter</c>) or one of its named options (<c>Parameter.Min</c>); null for none.</summary>
	public static LibraryEntry Attribute( string name ) => name is not null && Attributes.TryGetValue( name, out var entry ) ? entry : null;

	/// <summary>The type named <paramref name="name"/> in an annotation; null for none.</summary>
	public static LibraryEntry Type( string name ) => name is not null && Types.TryGetValue( name, out var entry ) ? entry : null;

	/// <summary>The bindable key <paramref name="name"/>, whatever its case; null for none.</summary>
	public static LibraryEntry Key( string name ) => Effect.KeyNamed( name ) is { } key ? Keys[ key ] : null;

	/// <summary>The names a script can use from the library everywhere at its top: globals and namespaces.</summary>
	public static IEnumerable<LibraryEntry> TopLevel()
	{
		foreach ( var pair in Globals )
			yield return pair.Value;
	}

	private static LibraryParameter P( string name, ScriptType type, string doc, string range = null, double[] otherwise = null ) =>
		new() { Name = name, Type = type, Doc = doc, Range = range, Default = otherwise };

	private static LibraryEntry Function( string name, Intrinsic op, ScriptType returns, string category, string doc, string example,
		ScriptPlace where, params LibraryParameter[] parameters )
	{
		var required = 0;
		while ( required < parameters.Length && parameters[ required ].Default is null )
			required++;

		return new LibraryEntry
		{
			Name = name, Kind = EntryKind.Function, Op = op, Type = returns, Category = category, Doc = doc, Example = example,
			Where = where, Parameters = parameters, Required = required,
			ExampleKind = (where & ScriptPlace.StickerRun) != 0 ? ScriptKind.Sticker : ScriptKind.Effect,
		};
	}

	// A function called on a value of the type the name starts with: layer.Emit( 10 )
	private static LibraryEntry Method( string name, Intrinsic op, ScriptType returns, string category, string doc, string example,
		ScriptPlace where, params LibraryParameter[] parameters )
	{
		var function = Function( name, op, returns, category, doc, example, where, parameters );
		return new LibraryEntry
		{
			Name = name, Kind = EntryKind.Method, Op = op, Type = returns, Category = category, Doc = doc, Example = example,
			Where = where, Parameters = parameters, Required = function.Required, ExampleKind = function.ExampleKind,
		};
	}

	private static LibraryEntry Constant( string name, ScriptType type, string category, string doc, string example, params double[] value ) => new()
	{
		Name = name, Kind = EntryKind.Constant, Type = type, Category = category, Doc = doc, Example = example, Value = value,
	};

	private static LibraryEntry Space( string name, string category, string doc, string example = null ) => new()
	{
		Name = name, Kind = EntryKind.Namespace, Category = category, Doc = doc, Example = example,
	};
}
