using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PlayerModels.Core.Script;

/// <summary>The widget a parameter is shown with, worked out from its type.</summary>
public enum ParameterKind { Number, Integer, Bool, Color, Palette, Text, Shape, Picture, Key, Choice, Vector3 }

/// <summary>
/// An option a script exposes with <c>[Parameter( "Title", Min = .., Max = .., Step = .., Group = .., Tip = ..,
/// Choices = { .. }, Order = .. )]</c> on a top-level local: what the Effects tab shows (its title, widget, range, group,
/// tip) and its default (the local's literal, where the source has it, for "Save as default"). A chosen value is kept
/// as text by the parameter's name (see <see cref="TryRead"/>), clamped to its type and range wherever it is read.
/// </summary>
public sealed record ScriptParameter
{
	/// <summary>The most parameters one script exposes.</summary>
	public const int MaxPerScript = 32;

	public string Name { get; init; }
	public string Title { get; init; }
	public ParameterKind Kind { get; init; }
	public ScriptType Type { get; init; }

	/// <summary>Its default, as a chosen value's text.</summary>
	public string Default { get; init; }

	public double Min { get; init; } = double.NaN;
	public double Max { get; init; } = double.NaN;
	public double Step { get; init; } = double.NaN;
	public string Group { get; init; }
	public string Tip { get; init; }
	public IReadOnlyList<string> Choices { get; init; }
	public double Order { get; init; }

	/// <summary>Where its literal is in the source, and its line.</summary>
	public int ValueStart { get; init; }
	public int ValueEnd { get; init; }
	public int Line { get; init; }

	internal int Index { get; init; }

	/// <summary>"glowSize" as a title: "Glow size".</summary>
	public static string TitleOf( string name )
	{
		var title = new StringBuilder();
		for ( var i = 0; i < name.Length; i++ )
		{
			var c = name[ i ];
			if ( c == '_' )
			{
				title.Append( ' ' );
				continue;
			}

			if ( i > 0 && char.IsUpper( c ) && !char.IsUpper( name[ i - 1 ] ) )
				title.Append( ' ' ).Append( char.ToLowerInvariant( c ) );
			else
				title.Append( i == 0 ? char.ToUpperInvariant( c ) : c );
		}

		return title.ToString().Trim();
	}

	/// <summary>
	/// Reads a chosen value's <paramref name="text"/> into value slots, held to this parameter: a number clamped to its
	/// range (and whole for an integer), a choice one of its choices, a colour <c>#rrggbb[aa]</c>, a palette of up to
	/// <see cref="EffectLayer.MaxPalette"/> colours joined by commas, a vector "x,y,z", a key, a stock shape or picture by
	/// name. False for text that isn't one: the default stays. <paramref name="strings"/> are the machine's (a choice is
	/// one of them; new text is added); a palette's colours come back flattened r, g, b, a.
	/// </summary>
	public bool TryRead( string text, List<string> strings, out double[] value )
	{
		value = null;
		if ( text is null || text.Length > ScriptLimits.MaxStringLength * 4 )
			return false;

		switch ( Kind )
		{
			case ParameterKind.Number or ParameterKind.Integer:
			{
				if ( !double.TryParse( text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number ) )
					return false;

				value = new[] { Held( number ) };
				return true;
			}

			case ParameterKind.Bool when text is "true" or "false":
				value = new[] { text == "true" ? 1.0 : 0.0 };
				return true;
			case ParameterKind.Color when ScriptMath.TryParseColor( text, out var r, out var g, out var b, out var a ):
				value = new[] { r, g, b, a };
				return true;
			case ParameterKind.Palette:
			{
				var colours = text.Split( ',' );
				if ( colours.Length > EffectLayer.MaxPalette )
					return false;

				value = new double[ colours.Length * 4 ];
				for ( var i = 0; i < colours.Length; i++ )
				{
					if ( !ScriptMath.TryParseColor( colours[ i ].Trim(), out value[ i * 4 ], out value[ i * 4 + 1 ], out value[ i * 4 + 2 ], out value[ i * 4 + 3 ] ) )
						return false;
				}

				return true;
			}

			case ParameterKind.Vector3:
			{
				var parts = text.Split( ',' );
				if ( parts.Length != 3 )
					return false;

				value = new double[ 3 ];
				for ( var i = 0; i < 3; i++ )
				{
					if ( !double.TryParse( parts[ i ], NumberStyles.Float, CultureInfo.InvariantCulture, out var part ) )
						return false;

					value[ i ] = Held( part );
				}

				return true;
			}

			case ParameterKind.Key when Effect.KeyNamed( text ) is { } key:
				value = new double[] { Compiler.KeyIndex( key ) };
				return true;
			case ParameterKind.Shape or ParameterKind.Picture:
				return TryShape( text, out value );
			case ParameterKind.Choice:
			{
				var choice = IndexOf( Choices, text );
				if ( choice < 0 )
					return false;

				value = new double[] { Type.Kind == TypeKind.Enum ? choice : IndexOf( strings, text ) };
				return value[ 0 ] >= 0;
			}

			case ParameterKind.Text when strings is not null && text.Length <= ScriptLimits.MaxStringLength:
			{
				var index = IndexOf( strings, text );
				if ( index < 0 )
				{
					strings.Add( text );
					index = strings.Count - 1;
				}

				value = new double[] { index };
				return true;
			}

			default:
				return false;
		}
	}

	private bool TryShape( string text, out double[] value )
	{
		value = null;
		if ( Kind == ParameterKind.Picture && Enum.TryParse<LayerPicture>( text, false, out var picture ) && Enum.IsDefined( picture ) )
		{
			value = new double[] { Handles.PictureBase + (int)picture };
			return true;
		}

		// shortcut: only the stock shapes can be chosen as a value yet; the player's own need the library (4b)
		if ( !Enum.TryParse<StandardShape>( text, false, out var shape ) || !Enum.IsDefined( shape ) )
			return false;

		value = new double[] { (int)shape };
		return true;
	}

	private double Held( double number )
	{
		number = ScriptMath.Safe( number );
		if ( !double.IsNaN( Min ) )
			number = Math.Clamp( number, Min, Max );

		return Kind == ParameterKind.Integer ? Math.Round( number ) : number;
	}

	private static int IndexOf( IReadOnlyList<string> list, string text )
	{
		for ( var i = 0; list is not null && i < list.Count; i++ )
		{
			if ( list[ i ] == text )
				return i;
		}

		return -1;
	}

	/// <summary>
	/// The chosen values of <paramref name="program"/>'s parameters within bounds: each read and written back clamped,
	/// those naming no parameter (or not one's value) dropped. What a look keeps after any client reads it.
	/// </summary>
	public static Dictionary<string, string> Sanitized( ScriptProgram program, IReadOnlyDictionary<string, string> values )
	{
		var kept = new Dictionary<string, string>();
		if ( values is null || program is null )
			return kept;

		var strings = new List<string>( program.Strings );
		foreach ( var parameter in program.Parameters )
		{
			if ( values.TryGetValue( parameter.Name, out var text ) && parameter.TryRead( text, strings, out var value ) )
				kept[ parameter.Name ] = parameter.Text( value, strings );
		}

		return kept;
	}

	/// <summary>Value slots as a chosen value's text (the inverse of <see cref="TryRead"/>).</summary>
	public string Text( double[] value, IReadOnlyList<string> strings )
	{
		switch ( Kind )
		{
			case ParameterKind.Number or ParameterKind.Integer: return value[ 0 ].ToString( "R", CultureInfo.InvariantCulture );
			case ParameterKind.Bool: return value[ 0 ] != 0 ? "true" : "false";
			case ParameterKind.Color: return ScriptValue.ColorText( value[ 0 ], value[ 1 ], value[ 2 ], value[ 3 ] );
			case ParameterKind.Palette:
			{
				var colours = new List<string>();
				for ( var i = 0; i + 3 < value.Length; i += 4 )
					colours.Add( ScriptValue.ColorText( value[ i ], value[ i + 1 ], value[ i + 2 ], value[ i + 3 ] ) );

				return string.Join( ",", colours );
			}

			case ParameterKind.Vector3:
				return string.Join( ",", value[ 0 ].ToString( "R", CultureInfo.InvariantCulture ), value[ 1 ].ToString( "R", CultureInfo.InvariantCulture ),
					value[ 2 ].ToString( "R", CultureInfo.InvariantCulture ) );
			case ParameterKind.Key: return Effect.Keys[ (int)value[ 0 ] ];
			case ParameterKind.Shape or ParameterKind.Picture:
			{
				var handle = (int)value[ 0 ];
				return handle >= Handles.PictureBase ? ((LayerPicture)(handle - Handles.PictureBase)).ToString() : ((StandardShape)handle).ToString();
			}

			case ParameterKind.Choice when Type.Kind == TypeKind.Enum: return Choices[ (int)value[ 0 ] ];
			default: return strings[ (int)value[ 0 ] ];
		}
	}
}
