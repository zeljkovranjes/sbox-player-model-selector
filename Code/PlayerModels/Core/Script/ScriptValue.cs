using System;
using System.Globalization;
using System.Text;

namespace PlayerModels.Core.Script;

/// <summary>
/// How values read to people, in the reference's examples and the editor's hover: numbers to at most four decimals,
/// colours as hex, vectors and choices as a script writes them. Only for display (it allocates), never while running.
/// </summary>
public static class ScriptValue
{
	/// <summary>A number as the reference writes it: <c>2</c>, <c>0.5</c>, <c>0.7854</c>.</summary>
	public static string NumberText( double value )
	{
		var rounded = Math.Round( value, 4 );
		return (rounded == 0 ? 0 : rounded).ToString( "0.####", CultureInfo.InvariantCulture );
	}

	/// <summary>A colour as <c>#rrggbb</c>, with alpha (<c>#rrggbbaa</c>) when it isn't solid.</summary>
	public static string ColorText( double r, double g, double b, double a )
	{
		var text = "#" + Hex( r ) + Hex( g ) + Hex( b );
		return Byte( a ) == 255 ? text : text + Hex( a );
	}

	/// <summary>A colour channel, 0 to 1, as the byte it is saved as.</summary>
	public static int Byte( double channel ) => (int)Math.Round( Math.Clamp( double.IsFinite( channel ) ? channel : 0, 0, 1 ) * 255, MidpointRounding.AwayFromZero );

	private static string Hex( double channel ) => Byte( channel ).ToString( "x2", CultureInfo.InvariantCulture );

	/// <summary>
	/// The value of <paramref name="type"/> in <paramref name="slots"/> as text: <c>3</c>, <c>true</c>, <c>"text"</c>,
	/// <c>#ff8000</c>, <c>Vector3( 1, 2, 3 )</c>, <c>Joint.Head</c>, <c>Shapes.Heart</c>, a list in braces.
	/// <paramref name="strings"/> and <paramref name="arena"/> are the program's, for text and lists.
	/// </summary>
	public static string Format( ScriptType type, ReadOnlySpan<double> slots, string[] strings, double[] arena )
	{
		switch ( type.Kind )
		{
			case TypeKind.Number: return NumberText( slots[ 0 ] );
			case TypeKind.Bool: return slots[ 0 ] != 0 ? "true" : "false";
			case TypeKind.String: return Quoted( At( strings, slots[ 0 ] ) );
			case TypeKind.Key: return Quoted( At( Effect.Keys, slots[ 0 ] ) );
			case TypeKind.Color: return ColorText( slots[ 0 ], slots[ 1 ], slots[ 2 ], slots[ 3 ] );
			case TypeKind.Vector3: return $"Vector3( {NumberText( slots[ 0 ] )}, {NumberText( slots[ 1 ] )}, {NumberText( slots[ 2 ] )} )";
			case TypeKind.Enum: return type.Enum.Name + "." + At( type.Enum.Members, slots[ 0 ] );
			case TypeKind.Shape or TypeKind.Picture: return ShapeText( (int)slots[ 0 ] );
			case TypeKind.Layer: return "Layer " + (int)slots[ 0 ];
			case TypeKind.EmblemLayer: return "EmblemLayer " + (int)slots[ 0 ];
			case TypeKind.Effect: return slots[ 0 ] == Handles.CurrentEffect ? "Effect" : "Effect " + (int)slots[ 0 ];
			case TypeKind.Weapon: return "Weapon";
			case TypeKind.WeaponSkin: return "Skin";
			case TypeKind.WeaponSticker: return "WeaponSticker " + (int)slots[ 0 ];
			case TypeKind.WeaponProp: return "WeaponProp " + (int)slots[ 0 ];
			case TypeKind.Function: return "function";
			case TypeKind.List:
			{
				var (start, count, width) = ((int)slots[ 0 ], (int)slots[ 1 ], type.Element.Width);
				if ( count == 0 )
					return "{}";

				var text = new StringBuilder( "{ " );
				for ( var i = 0; i < count && arena is not null && start + (i + 1) * width <= arena.Length; i++ )
					text.Append( i > 0 ? ", " : "" ).Append( Format( type.Element, arena.AsSpan( start + i * width, width ), strings, arena ) );

				return text.Append( " }" ).ToString();
			}

			default: return type.ToString();
		}
	}

	/// <summary>A shape or picture handle as a script writes it.</summary>
	public static string ShapeText( int handle )
	{
		if ( handle >= Handles.PictureBase )
			return "Picture." + At( Enum.GetNames<LayerPicture>(), handle - Handles.PictureBase );

		if ( handle >= Handles.DraftBase )
			return "Shape " + (handle - Handles.DraftBase);

		return "Shapes." + At( Enum.GetNames<StandardShape>(), handle );
	}

	/// <summary>Text as a script writes it, in double quotes with its escapes.</summary>
	public static string Quoted( string text )
	{
		var quoted = new StringBuilder( "\"" );
		foreach ( var c in text ?? "" )
		{
			quoted.Append( c switch
			{
				'"' => "\\\"",
				'\\' => "\\\\",
				'\n' => "\\n",
				'\t' => "\\t",

				// The language writes no other control character (a carriage return, a bell): typed text loses them
				_ when char.IsControl( c ) => "",
				_ => c.ToString(),
			} );
		}

		return quoted.Append( '"' ).ToString();
	}

	private static string At( System.Collections.Generic.IReadOnlyList<string> names, double index )
	{
		var i = (int)index;
		return names is not null && i >= 0 && i < names.Count ? names[ i ] : "?";
	}
}
