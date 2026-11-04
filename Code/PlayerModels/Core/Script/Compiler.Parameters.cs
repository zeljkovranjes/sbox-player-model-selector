using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

public sealed partial class Compiler
{
	private readonly List<ScriptParameter> _parameters = new();

	// The named arguments [Parameter] takes
	internal static readonly string[] ParameterArguments = { "Min", "Max", "Step", "Group", "Tip", "Choices", "Order" };

	// [Parameter( ... )] local name = literal: checks the attribute, records the option, and lets a chosen value replace the literal
	private void ParameterLocal( LocalStmt local )
	{
		var attribute = local.Attribute;
		if ( attribute.Name != "Parameter" )
			Error( $"There is no attribute [{attribute.Name}]: the one there is, is [Parameter]", attribute );
		else if ( !F.IsMain || F.BlockDepth > 0 )
			Error( "[Parameter] goes on a local at the top of the script, not inside a function or block", attribute );

		var declared = local.Type is null ? null : TypeOf( local.Type );
		if ( local.Value is null )
		{
			Error( $"A parameter needs its default written out: local {local.Name} = ...", local );
			return;
		}

		var type = Value( local.Value, declared );
		if ( declared is not null )
			Check( type, declared, local.Value );

		type = declared ?? type;
		var parameter = Describe( local, type );
		if ( parameter is not null )
		{
			Emit( OpCode.Parameter, parameter.Index, type.Width );
			_parameters.Add( parameter );
		}

		var symbol = Declare( local.Name, type, local.NameStart, _scopeEnd );
		symbol.IsParameter = parameter is not null;
		Store( symbol );
	}

	private ScriptParameter Describe( LocalStmt local, ScriptType type )
	{
		var value = local.Value;
		if ( !IsLiteral( value ) || value is NameExpr )
		{
			Error( "A parameter's default must be written out (like 2.6, true, \"text\", Color.Parse( \"#ff8800\" ) or { ... }), so the editor can show and change it", value );
			return null;
		}

		if ( _parameters.Exists( x => x.Name == local.Name ) )
		{
			Error( $"There is already a parameter called '{local.Name}'", local.NameStart, local.Name.Length );
			return null;
		}

		if ( _parameters.Count >= ScriptParameter.MaxPerScript )
		{
			Error( $"A script has at most {ScriptParameter.MaxPerScript} parameters", local.Attribute );
			return null;
		}

		var attribute = local.Attribute;
		string title = null, group = null, tip = null;
		double min = double.NaN, max = double.NaN, step = double.NaN, order = _parameters.Count;
		List<string> choices = null;
		foreach ( var argument in attribute.Arguments )
		{
			if ( title is not null || argument is not StringExpr text )
				Error( "[Parameter] takes its title as text, then named options: [Parameter( \"Glow size\", Min = 0, Max = 10 )]", argument );
			else
				title = text.Value;
		}

		var seen = new HashSet<string>();
		foreach ( var (name, start, argument) in attribute.Named )
		{
			if ( !seen.Add( name ) )
			{
				Error( $"{name} is given twice", start, name.Length );
				continue;
			}

			switch ( name )
			{
				case "Min": min = NumberArgument( argument, name ); break;
				case "Max": max = NumberArgument( argument, name ); break;
				case "Step": step = NumberArgument( argument, name ); break;
				case "Order": order = NumberArgument( argument, name ); break;
				case "Group": group = TextArgument( argument, name ); break;
				case "Tip": tip = TextArgument( argument, name ); break;
				case "Choices": choices = ChoicesArgument( argument ); break;
				default:
					Error( $"[Parameter] has no option '{name}'{Suggest( name, ParameterArguments )}", start, name.Length );
					break;
			}
		}

		if ( choices is not null && type.Kind != TypeKind.String )
		{
			Error( "Choices are for text parameters: [Parameter( Choices = { \"Spin\", \"Pulse\" } )] local motion = \"Spin\"", local.Attribute );
			return null;
		}

		var kind = KindOf( type, choices, step, value );
		if ( kind is null )
		{
			Error( $"A parameter can't be {Article( type )}: it can be a number, bool, string, Color, list of Colors, Shape, Picture, Key, a choice (like Joint) or Vector3", value );
			return null;
		}

		var defaultValue = DefaultOf( value, type );
		if ( !CheckRange( kind.Value, type, min, max, step, defaultValue, attribute ) )
			return null;

		if ( type.Kind == TypeKind.Enum )
			choices = new List<string>( type.Enum.Members );
		else if ( choices is not null && !choices.Contains( _strings[ (int)defaultValue[ 0 ] ] ) )
			Error( "The default must be one of the Choices", value );

		var parameter = new ScriptParameter
		{
			Name = local.Name, Title = title ?? ScriptParameter.TitleOf( local.Name ), Kind = kind.Value, Type = type, Min = min, Max = max,
			Step = step, Group = group, Tip = tip, Choices = choices, Order = order, ValueStart = value.Start, ValueEnd = value.End,
			Line = _lines.Position( value.Start ).Line, Index = _parameters.Count,
		};

		return parameter with { Default = parameter.Text( defaultValue, _strings ) };
	}

	private static ParameterKind? KindOf( ScriptType type, List<string> choices, double step, Expr value ) => type.Kind switch
	{
		TypeKind.Number when step >= 1 && step == Math.Floor( step ) => ParameterKind.Integer,
		TypeKind.Number => ParameterKind.Number,
		TypeKind.Bool => ParameterKind.Bool,
		TypeKind.Color => ParameterKind.Color,
		TypeKind.List when type.Element.Kind == TypeKind.Color => ParameterKind.Palette,
		TypeKind.String when choices is not null => ParameterKind.Choice,
		TypeKind.String => ParameterKind.Text,
		TypeKind.Shape => ParameterKind.Shape,
		TypeKind.Picture => ParameterKind.Picture,
		TypeKind.Key => ParameterKind.Key,
		TypeKind.Enum => ParameterKind.Choice,
		TypeKind.Vector3 => ParameterKind.Vector3,
		_ => null,
	};

	// The default's value slots (a palette's colours flattened)
	private double[] DefaultOf( Expr value, ScriptType type )
	{
		if ( TryConstant( value, type, false, out _, out var constant ) )
			return constant;

		var slots = new List<double>();
		if ( value is ListExpr list )
		{
			foreach ( var item in list.Items )
			{
				if ( TryConstant( item, type.Element, false, out _, out var colour ) )
					slots.AddRange( colour );
			}
		}

		return slots.ToArray();
	}

	private bool CheckRange( ParameterKind kind, ScriptType type, double min, double max, double step, double[] value, Node at )
	{
		var ranged = !double.IsNaN( min ) || !double.IsNaN( max ) || !double.IsNaN( step );
		if ( ranged && type.Kind is not (TypeKind.Number or TypeKind.Vector3) )
		{
			Error( "Min, Max and Step are for number and Vector3 parameters", at );
			return false;
		}

		if ( double.IsNaN( min ) != double.IsNaN( max ) )
		{
			Error( "Give both Min and Max, or neither", at );
			return false;
		}

		if ( min > max )
		{
			Error( $"Min ({ScriptValue.NumberText( min )}) is above Max ({ScriptValue.NumberText( max )})", at );
			return false;
		}

		if ( step <= 0 )
		{
			Error( "Step must be above 0", at );
			return false;
		}

		for ( var i = 0; !double.IsNaN( min ) && i < value.Length; i++ )
		{
			if ( value[ i ] < min || value[ i ] > max )
			{
				Error( $"The default ({ScriptValue.NumberText( value[ i ] )}) is outside Min to Max ({ScriptValue.NumberText( min )} to {ScriptValue.NumberText( max )})", at );
				return false;
			}
		}

		return kind != ParameterKind.Integer || value[ 0 ] == Math.Floor( value[ 0 ] ) || Error( "A whole-number parameter (Step 1) needs a whole default", at, false );
	}

	private bool Error( string message, Node at, bool result )
	{
		Error( message, at );
		return result;
	}

	private double NumberArgument( Expr argument, string name )
	{
		if ( TryConstant( argument, ScriptType.Number, false, out var type, out var value ) && type.Kind == TypeKind.Number )
			return value[ 0 ];

		Error( $"{name} must be a number written out", argument );
		return double.NaN;
	}

	private string TextArgument( Expr argument, string name )
	{
		if ( argument is StringExpr text )
			return text.Value;

		Error( $"{name} must be text in quotes", argument );
		return null;
	}

	private List<string> ChoicesArgument( Expr argument )
	{
		var choices = new List<string>();
		if ( argument is ListExpr list && list.Items.Count > 0 )
		{
			foreach ( var item in list.Items )
			{
				if ( item is not StringExpr text )
				{
					Error( "Each choice is text in quotes", item );
					return null;
				}

				choices.Add( text.Value );
				Intern( text.Value );
			}

			return choices;
		}

		Error( "Choices is a list of text: Choices = { \"Spin\", \"Pulse\" }", argument );
		return null;
	}
}
