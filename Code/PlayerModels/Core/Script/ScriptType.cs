using System.Collections.Generic;
using System.Text;

namespace PlayerModels.Core.Script;

/// <summary>The kinds of value a script has.</summary>
public enum TypeKind
{
	Error, Void, Number, Bool, String, Color, Vector3, Key, Shape, Picture, Layer, Effect, EmblemLayer, Enum, List, Function,

	// A weapon design script's: the weapon, its skin, a sticker and a prop on it
	Weapon, WeaponSkin, WeaponSticker, WeaponProp,

	// Only ever parameters, read field by field: a particle's (p) and a shot's
	Particle, Shot,
}

/// <summary>
/// A script value's static type, checked when the script compiles (like C#): numbers, bools, text, colours, vectors,
/// keys, shapes, pictures, layers, effects, emblem layers, the library's named choices (<see cref="ScriptEnum"/>),
/// lists of one type (<c>{Color}</c>) and functions (<c>(number) -> number</c>). Each type is a fixed number of value
/// slots (<see cref="Width"/>), so the machine never boxes or allocates to hold one.
/// </summary>
public sealed class ScriptType
{
	public TypeKind Kind { get; }
	public ScriptType Element { get; }
	public IReadOnlyList<ScriptType> Parameters { get; }
	public ScriptType Return { get; }
	public ScriptEnum Enum { get; }

	/// <summary>How many value slots it takes: a colour 4 (r, g, b, a), a vector 3, a list 2 (where, how many), most 1.</summary>
	public int Width { get; }

	private ScriptType( TypeKind kind, int width, ScriptType element = null, IReadOnlyList<ScriptType> parameters = null, ScriptType returns = null, ScriptEnum enumeration = null )
	{
		Kind = kind;
		Width = width;
		Element = element;
		Parameters = parameters;
		Return = returns;
		Enum = enumeration;
	}

	[Sandbox.SkipHotload]
	public static readonly ScriptType Error = new( TypeKind.Error, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Void = new( TypeKind.Void, 0 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Number = new( TypeKind.Number, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Bool = new( TypeKind.Bool, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType String = new( TypeKind.String, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Color = new( TypeKind.Color, 4 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Vector3 = new( TypeKind.Vector3, 3 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Key = new( TypeKind.Key, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Shape = new( TypeKind.Shape, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Picture = new( TypeKind.Picture, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Layer = new( TypeKind.Layer, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Effect = new( TypeKind.Effect, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType EmblemLayer = new( TypeKind.EmblemLayer, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Weapon = new( TypeKind.Weapon, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType WeaponSkin = new( TypeKind.WeaponSkin, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType WeaponSticker = new( TypeKind.WeaponSticker, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType WeaponProp = new( TypeKind.WeaponProp, 1 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Particle = new( TypeKind.Particle, 0 );
	[Sandbox.SkipHotload]
	public static readonly ScriptType Shot = new( TypeKind.Shot, 0 );

	/// <summary>Any type, for a library parameter that takes one (List.Create's value); checked by its call.</summary>
	[Sandbox.SkipHotload]
	public static readonly ScriptType Any = new( TypeKind.Error, 1 );

	/// <summary>A list of <paramref name="element"/>.</summary>
	public static ScriptType ListOf( ScriptType element ) => new( TypeKind.List, 2, element );

	/// <summary>A function taking <paramref name="parameters"/> and giving <paramref name="returns"/> (void for nothing).</summary>
	public static ScriptType FunctionOf( IReadOnlyList<ScriptType> parameters, ScriptType returns ) => new( TypeKind.Function, 1, null, parameters, returns );

	/// <summary>The type of <paramref name="enumeration"/>'s choices.</summary>
	internal static ScriptType EnumOf( ScriptEnum enumeration ) => new( TypeKind.Enum, 1, null, null, null, enumeration );

	/// <summary>The types a script names directly (in <c>local x: Color</c>), by name.</summary>
	[Sandbox.SkipHotload]
	public static readonly IReadOnlyDictionary<string, ScriptType> Named = new Dictionary<string, ScriptType>
	{
		["number"] = Number, ["bool"] = Bool, ["string"] = String, ["Color"] = Color, ["Vector3"] = Vector3, ["Key"] = Key,
		["Shape"] = Shape, ["Picture"] = Picture, ["Layer"] = Layer, ["Effect"] = Effect, ["EmblemLayer"] = EmblemLayer,
		["Weapon"] = Weapon, ["WeaponSkin"] = WeaponSkin, ["WeaponSticker"] = WeaponSticker, ["WeaponProp"] = WeaponProp,
	};

	/// <summary>Whether it is a value that can be kept in a variable (parameters only types and nothing aren't).</summary>
	public bool IsStorable => Kind is not (TypeKind.Void or TypeKind.Particle or TypeKind.Shot);

	/// <summary>Whether it is exactly <paramref name="other"/>.</summary>
	public bool Same( ScriptType other )
	{
		if ( other is null || Kind != other.Kind )
			return false;

		switch ( Kind )
		{
			case TypeKind.List: return Element.Same( other.Element );
			case TypeKind.Enum: return Enum == other.Enum;
			case TypeKind.Function:
				if ( Parameters.Count != other.Parameters.Count || !Return.Same( other.Return ) )
					return false;

				for ( var i = 0; i < Parameters.Count; i++ )
				{
					if ( !Parameters[ i ].Same( other.Parameters[ i ] ) )
						return false;
				}

				return true;
			default: return true;
		}
	}

	/// <summary>
	/// Whether a value of this type can be given where <paramref name="target"/> is wanted: the same type, a shape as a
	/// picture, or anything after an error (so one mistake isn't reported twice).
	/// </summary>
	public bool FitsInto( ScriptType target )
	{
		return Same( target ) || Kind == TypeKind.Error || target.Kind == TypeKind.Error || Kind == TypeKind.Shape && target.Kind == TypeKind.Picture;
	}

	/// <summary>How a script writes it: <c>number</c>, <c>{Color}</c>, <c>(number) -> bool</c>, <c>Joint</c>.</summary>
	public override string ToString()
	{
		switch ( Kind )
		{
			case TypeKind.Error: return this == Any ? "any" : "unknown";
			case TypeKind.Void: return "nothing";
			case TypeKind.List: return "{" + Element + "}";
			case TypeKind.Enum: return Enum.Name;
			case TypeKind.Function:
			{
				var text = new StringBuilder( "(" );
				for ( var i = 0; i < Parameters.Count; i++ )
					text.Append( i > 0 ? ", " : "" ).Append( Parameters[ i ] );

				return text.Append( ") -> " ).Append( Return ).ToString();
			}

			default:
				foreach ( var pair in Named )
				{
					if ( pair.Value == this )
						return pair.Key;
				}

				return Kind.ToString();
		}
	}
}

/// <summary>
/// A set of named choices a script picks from, like a C# enum: <c>Joint.Head</c>, <c>Rhythm.Pulse</c>. Each choice is
/// its index; the names are the data's own (see <see cref="EffectLayer"/>), so a choice is saved by the same name the
/// visual editor saves.
/// </summary>
public sealed class ScriptEnum
{
	public string Name { get; }
	public IReadOnlyList<string> Members { get; }
	public string Doc { get; }
	public ScriptType Type { get; }

	public ScriptEnum( string name, IReadOnlyList<string> members, string doc )
	{
		Name = name;
		Members = members;
		Doc = doc;
		Type = ScriptType.EnumOf( this );
	}

	/// <summary>The index of <paramref name="member"/>, or -1.</summary>
	public int IndexOf( string member )
	{
		for ( var i = 0; i < Members.Count; i++ )
		{
			if ( Members[ i ] == member )
				return i;
		}

		return -1;
	}
}
