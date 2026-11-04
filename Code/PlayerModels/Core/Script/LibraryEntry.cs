using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PlayerModels.Core.Script;

/// <summary>
/// What a script is for: an effect (built once, then its functions run), a shape (built once into points), a sticker (run
/// per element, each frame), or a weapon's skin (run per band along the weapon, each frame, into its colours).
/// </summary>
public enum ScriptKind { Effect, Shape, Sticker, Skin }

/// <summary>
/// Where in a script a library name can be used: while an effect or a shape is built (its top level, once), while an
/// effect runs (its event and particle functions, each frame or event), in a sticker's per-element run, or in a weapon
/// skin's per-band run.
/// </summary>
[Flags]
public enum ScriptPlace
{
	None = 0,
	EffectBuild = 1,
	EffectRun = 2,
	ShapeBuild = 4,
	StickerRun = 8,
	SkinRun = 16,
	Build = EffectBuild | ShapeBuild,
	Run = EffectRun | StickerRun | SkinRun,
	Element = StickerRun | SkinRun,
	Effect = EffectBuild | EffectRun,
	All = EffectBuild | EffectRun | ShapeBuild | StickerRun | SkinRun,
}

/// <summary>What a library entry is.</summary>
public enum EntryKind { Keyword, Type, Namespace, Function, Constant, Property, Method, Input, Output, Event, Enum, Key, Attribute }

/// <summary>How a property of a value is read or written by the machine.</summary>
public enum PropertyAccess { None, Component, ListCount, Intrinsic, Input, Output, Builder, Reserved }

/// <summary>A parameter of a library function or event: its name, type, range and what it is for.</summary>
public sealed record LibraryParameter
{
	public string Name { get; init; }
	public ScriptType Type { get; init; }
	public string Doc { get; init; }

	/// <summary>The value given when it is left out; null when it can't be.</summary>
	public double[] Default { get; init; }

	/// <summary>The range it is meant for, written for people ("0 to 1"); null for any.</summary>
	public string Range { get; init; }

	/// <summary>For an event's parameter, the input it reads.</summary>
	public ScriptInput Input { get; init; }
}

/// <summary>
/// One name of the language's fixed library: a function, constant, property, input, output, event, keyword, type or
/// named choice, with its signature, a one-line description, a runnable example and its category. This one table is
/// what the compiler checks names against (anything not in it fails to compile), and what completion, hover, signature
/// help and the reference docs read, so they never disagree.
/// </summary>
public sealed class LibraryEntry
{
	/// <summary>How it is written: <c>Math.Floor</c>, <c>time</c>, <c>OnLand</c>, <c>if</c>; a member as <c>Layer.Size</c>.</summary>
	public string Name { get; init; }

	/// <summary>Its last part (<c>Floor</c>, <c>Size</c>), what completion offers after a dot.</summary>
	public string Member => Name.LastIndexOf( '.' ) is var dot and >= 0 ? Name[ (dot + 1).. ] : Name;

	/// <summary>The namespace it is in (<c>Math</c>) or the type it is a member of (<c>Layer</c>); null for a global.</summary>
	public string Owner => Name.LastIndexOf( '.' ) is var dot and >= 0 ? Name[ ..dot ] : null;

	public EntryKind Kind { get; init; }
	public string Category { get; init; }

	/// <summary>Its value's type, or what a function gives back (void for nothing).</summary>
	public ScriptType Type { get; init; }

	public IReadOnlyList<LibraryParameter> Parameters { get; init; } = Array.Empty<LibraryParameter>();

	/// <summary>How many parameters must be given; the rest have defaults.</summary>
	public int Required { get; init; }

	public string Doc { get; init; }

	/// <summary>
	/// A short runnable example: lines like <c>local f = Math.Floor( 2.7 ) --&gt; 2</c>, the value after <c>--&gt;</c> being
	/// what the local holds once it has run (checked by the doc tests).
	/// </summary>
	public string Example { get; init; }

	/// <summary>The kind of script the example is (and where it runs).</summary>
	public ScriptKind ExampleKind { get; init; } = ScriptKind.Sticker;

	/// <summary>Where it can be read or called; where a property can be written.</summary>
	public ScriptPlace Where { get; init; } = ScriptPlace.All;
	public ScriptPlace WriteWhere { get; init; } = ScriptPlace.None;

	/// <summary>The machine operation behind a function or method.</summary>
	public Intrinsic Op { get; init; }

	/// <summary>How a property is reached, and the slot, component or property it is.</summary>
	public PropertyAccess Access { get; init; }
	public int Slot { get; init; }

	/// <summary>A constant's value, slot by slot.</summary>
	public double[] Value { get; init; }

	/// <summary>The range a property's value is held to when it is written; NaN for none.</summary>
	public double Min { get; init; } = double.NaN;
	public double Max { get; init; } = double.NaN;

	/// <summary>The choices of a named-choice type.</summary>
	public ScriptEnum Enum { get; init; }

	/// <summary>The event a handler name is for.</summary>
	public ScriptEvent Event { get; init; }

	/// <summary>The most times a second an event is delivered for one player, on every client.</summary>
	public int RatePerSecond { get; init; }

	/// <summary>Whether it can be written.</summary>
	public bool IsWritable => WriteWhere != ScriptPlace.None;

	/// <summary>Its signature for people: <c>Math.Clamp( x: number, min: number, max: number ): number</c>.</summary>
	public string Signature()
	{
		var text = new StringBuilder();
		switch ( Kind )
		{
			case EntryKind.Function or EntryKind.Method or EntryKind.Event:
			case EntryKind.Attribute when Owner is null:
			{
				text.Append( Kind == EntryKind.Event ? "function " : "" ).Append( Name ).Append( Parameters.Count > 0 ? "( " : "(" );
				for ( var i = 0; i < Parameters.Count; i++ )
				{
					var parameter = Parameters[ i ];
					text.Append( i > 0 ? ", " : "" ).Append( i >= Required ? "[" : "" ).Append( parameter.Name ).Append( ": " )
						.Append( parameter.Type ).Append( i >= Required ? "]" : "" );
				}

				text.Append( Parameters.Count > 0 ? " )" : ")" );
				if ( Type is not null && Type.Kind != TypeKind.Void )
					text.Append( ": " ).Append( Type );

				return text.ToString();
			}

			case EntryKind.Constant or EntryKind.Property or EntryKind.Input or EntryKind.Output or EntryKind.Attribute:
				text.Append( Name ).Append( ": " ).Append( Type );
				if ( !double.IsNaN( Min ) )
					text.Append( "  (" ).Append( Number( Min ) ).Append( " to " ).Append( Number( Max ) ).Append( ')' );

				if ( Kind == EntryKind.Property && !IsWritable )
					text.Append( "  (read only)" );

				return text.ToString();
			default:
				return Name;
		}
	}

	private static string Number( double value ) => value.ToString( "0.###", CultureInfo.InvariantCulture );
}
