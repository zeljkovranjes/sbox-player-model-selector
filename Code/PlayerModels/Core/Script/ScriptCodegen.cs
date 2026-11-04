using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlayerModels.Core.Script;

/// <summary>
/// Writes what the visual editor made as a script, in the canonical style (what <see cref="Formatter"/> writes): the
/// effect's own fields, the drawn shapes its layers use (as their points, a sticker's colours after), then one block per
/// layer, each field only when it isn't the default. Building the script gives back the same effect, field for field.
/// Its id and where it was forked from stay outside the script, with the effect.
/// </summary>
public static class ScriptCodegen
{
	// How many points one Points( { ... } ) call holds, so a line stays readable and under the line cap
	private const int PointsPerLine = 16;

	/// <summary>
	/// <paramref name="effect"/> as a script; the drawn shapes it names are its own (<see cref="Effect.Shapes"/>) or, in a
	/// look, <paramref name="shapes"/>.
	/// </summary>
	public static string Effect( Effect effect, IReadOnlyList<DrawnShape> shapes = null )
	{
		var text = new StringBuilder();
		var defaults = new Effect { Anchor = new EffectAnchor() };
		for ( var property = PropertyId.EffectName; property <= PropertyId.EffectSpeed; property++ )
		{
			var value = ScriptLiteral.Of( property, effect );
			if ( value is not null && value != ScriptLiteral.Of( property, defaults ) )
				text.Append( "Effect." ).Append( ScriptProperties.Of( property ).Name ).Append( " = " ).Append( value ).Append( '\n' );
		}

		var names = new HashSet<string>();
		var drawn = new Dictionary<string, string>();
		foreach ( var layer in effect.Layers )
		{
			foreach ( var reference in layer.Pictures is null ? new[] { layer.Formation, layer.Picture } : new[] { layer.Formation, layer.Picture }.Concat( layer.Pictures ) )
			{
				if ( !DrawnShape.IsReference( reference ) || drawn.ContainsKey( reference ) || Find( reference, effect.Shapes, shapes ) is not { } shape )
					continue;

				var name = Unique( names, Identifier( shape.Name, "shape" ) );
				drawn[ reference ] = name;
				Paragraph( text );
				WriteShape( text, name, shape );
			}
		}

		foreach ( var layer in effect.Layers )
		{
			var name = Unique( names, LayerName( layer ) );
			Paragraph( text );
			WriteLayer( text, name, layer, x => drawn.TryGetValue( x, out var variable ) ? variable : null );
		}

		return text.ToString();
	}

	/// <summary>A drawn shape (a shape script's own) as a shape script: its points, then its sticker colours.</summary>
	public static string Shape( DrawnShape shape )
	{
		var text = new StringBuilder();
		WriteShape( text, "shape", shape, isOwn: true );
		return text.ToString();
	}

	/// <summary>One layer as a block of a script, held in <paramref name="name"/>.</summary>
	public static string Layer( string name, EffectLayer layer, Func<string, string> shapeName = null )
	{
		var text = new StringBuilder();
		WriteLayer( text, name, layer, shapeName );
		return text.ToString();
	}

	private static void WriteLayer( StringBuilder text, string name, EffectLayer layer, Func<string, string> shapeName )
	{
		text.Append( "local " ).Append( name ).Append( " = " ).Append( layer.KindIs ).Append( "()\n" );
		var defaults = new EffectLayer { Kind = layer.Kind }.Sanitized();
		foreach ( var property in ScriptProperties.All )
		{
			if ( property.Owner != TypeKind.Layer || property.ReadOnly || property.Id == PropertyId.LayerParticle )
				continue;

			var value = ScriptLiteral.Of( property.Id, layer, shapeName );
			if ( value is not null && value != ScriptLiteral.Of( property.Id, defaults, shapeName ) )
				text.Append( name ).Append( '.' ).Append( property.Name ).Append( " = " ).Append( value ).Append( '\n' );
		}
	}

	private static void WriteShape( StringBuilder text, string name, DrawnShape shape, bool isOwn = false )
	{
		if ( !isOwn )
			text.Append( "local " ).Append( name ).Append( " = Shapes.New( " ).Append( ScriptLiteral.Text( shape.Name ?? "" ) ).Append( " )\n" );

		foreach ( var stroke in shape.Decode() )
		{
			for ( var start = 0; start < stroke.Count; start += PointsPerLine )
			{
				text.Append( name ).Append( ".Points( { " );
				for ( var i = start; i < Math.Min( start + PointsPerLine, stroke.Count ); i++ )
				{
					text.Append( i > start ? ", " : "" ).Append( ScriptLiteral.Coordinate( stroke.Points[ i * 2 ] ) ).Append( ", " )
						.Append( ScriptLiteral.Coordinate( stroke.Points[ i * 2 + 1 ] ) );
				}

				text.Append( " } )\n" );
			}

			text.Append( name ).Append( stroke.Closed ? ".EndOutline()\n" : ".EndLine()\n" );
		}

		if ( shape.FadeArc != 0f )
		{
			text.Append( name ).Append( ".Fade( " ).Append( ScriptLiteral.Number( shape.FadeFrom ) ).Append( ", " )
				.Append( ScriptLiteral.Number( shape.FadeFrom + shape.FadeArc ) ).Append( " )\n" );
		}

		if ( shape.Sticker is not { } sticker )
			return;

		var plain = new Sticker();
		text.Append( name ).Append( ".FillColor = " ).Append( ScriptLiteral.Colour( sticker.Fill ) ).Append( '\n' );
		Field( text, name, "OutlineColor", ScriptLiteral.Colour( sticker.Outline ), ScriptLiteral.Colour( plain.Outline ) );
		Field( text, name, "OutlineWidth", ScriptLiteral.Number( sticker.OutlineWidth ), ScriptLiteral.Number( plain.OutlineWidth ) );
		Field( text, name, "Animation", sticker.Animation is null ? null : "Animation." + sticker.Animation, null );
		Field( text, name, "Palette", sticker.Palette is null ? null : ScriptLiteral.Palette( sticker.Palette ), null );
		Field( text, name, "AnimationSpeed", ScriptLiteral.Number( sticker.Speed ), ScriptLiteral.Number( plain.Speed ) );
	}

	private static void Field( StringBuilder text, string name, string field, string value, string otherwise )
	{
		if ( value is not null && value != otherwise )
			text.Append( name ).Append( '.' ).Append( field ).Append( " = " ).Append( value ).Append( '\n' );
	}

	private static void Paragraph( StringBuilder text )
	{
		if ( text.Length > 0 )
			text.Append( '\n' );
	}

	private static DrawnShape Find( string reference, IReadOnlyList<DrawnShape> own, IReadOnlyList<DrawnShape> look )
	{
		foreach ( var list in new[] { own, look } )
		{
			foreach ( var shape in list ?? Array.Empty<DrawnShape>() )
			{
				if ( shape.Reference == reference )
					return shape;
			}
		}

		return null;
	}

	// A layer's name from what it is: its picture for sprites ("flame", "star5"), else its kind ("light", "trail")
	private static string LayerName( EffectLayer layer )
	{
		if ( layer.KindIs != LayerKind.Sprite )
			return Identifier( layer.KindIs.ToString(), "layer" );

		return layer.DrawnPicture is null ? Identifier( layer.Picture ?? nameof( LayerPicture.Glow ), "sprite" ) : "sprite";
	}

	/// <summary>
	/// <paramref name="text"/> as a script name: its letters and digits in lowerCamel case, never a keyword or a library
	/// name; <paramref name="otherwise"/> when nothing of it is left.
	/// </summary>
	public static string Identifier( string text, string otherwise )
	{
		var name = new StringBuilder();
		var upper = false;
		foreach ( var c in text ?? "" )
		{
			if ( char.IsAsciiLetterOrDigit( c ) )
			{
				name.Append( name.Length == 0 ? char.ToLowerInvariant( c ) : upper ? char.ToUpperInvariant( c ) : c );
				upper = false;
			}
			else
				upper = name.Length > 0;
		}

		var identifier = name.Length == 0 || char.IsAsciiDigit( name[ 0 ] ) ? otherwise : name.ToString();
		if ( identifier.Length > Lexer.MaxNameLength )
			identifier = identifier[ ..Lexer.MaxNameLength ];

		return ScriptLibrary.Keyword( identifier ) is not null || ScriptLibrary.Global( identifier ) is not null ? identifier + "Layer" : identifier;
	}

	private static string Unique( HashSet<string> names, string name )
	{
		var unique = name;
		for ( var i = 2; !names.Add( unique ); i++ )
			unique = name + i;

		return unique;
	}
}
