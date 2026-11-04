using System.Collections.Generic;
using System.Text;

namespace PlayerModels.Core.Script;

public static partial class ScriptCodegen
{
	/// <summary>
	/// A sticker of layers (see <see cref="DrawnShape.Emblem"/>) as a shape script: whether it is mirrored and what it is
	/// printed on, the drawn shapes its layers draw (as their points), then one block per layer, each field only when it
	/// isn't the default. Building the script gives back the same sticker, layer for layer; its name and its own script
	/// (see <see cref="DrawnShape.Script"/>) stay outside it, with the sticker. Lines a layer was drawn with keep their
	/// points; how wide they were and eraser strokes inside them aren't written.
	/// </summary>
	public static string Sticker( DrawnShape sticker )
	{
		var text = new StringBuilder();
		WriteEmblem( text, "shape", sticker, new HashSet<string> { "shape" } );
		return text.ToString();
	}

	// A sticker of layers drawn into the shape held in owner: its mirror and finish, its drawn shapes, then its layers
	private static void WriteEmblem( StringBuilder text, string owner, DrawnShape sticker, HashSet<string> names )
	{
		if ( sticker?.Mirror == true )
			text.Append( owner ).Append( ".Mirror = true\n" );

		if ( sticker?.FinishIs is { } finish && finish != StickerFinish.Paper )
			text.Append( owner ).Append( ".Finish = StickerFinish." ).Append( finish ).Append( '\n' );

		var drawn = new Dictionary<string, string>();
		foreach ( var part in sticker?.Parts ?? new List<DrawnShape>() )
		{
			if ( drawn.ContainsKey( part.Reference ) )
				continue;

			var name = Unique( names, Identifier( part.Name, "drawn" ) );
			drawn[ part.Reference ] = name;
			Paragraph( text );
			WriteShape( text, name, part, names: names );
		}

		var plain = new EmblemLayer();
		foreach ( var layer in sticker?.Emblem ?? new List<EmblemLayer>() )
		{
			var shape = drawn.TryGetValue( layer.Shape, out var variable ) ? variable : "Shapes." + layer.Shape;
			var name = Unique( names, Identifier( drawn.ContainsKey( layer.Shape ) ? variable + "Layer" : layer.Shape, "layer" ) );
			Paragraph( text );
			text.Append( "local " ).Append( name ).Append( " = " ).Append( owner ).Append( ".AddLayer( " ).Append( shape ).Append( " )\n" );
			Field( text, name, "Position", Vector( layer.X, layer.Y, 0f ), Vector( plain.X, plain.Y, 0f ) );
			Field( text, name, "Rotation", ScriptLiteral.Number( layer.Rotation ), ScriptLiteral.Number( plain.Rotation ) );
			Field( text, name, "Scale", Vector( layer.ScaleX, layer.ScaleY, 1f ), Vector( plain.ScaleX, plain.ScaleY, 1f ) );
			Field( text, name, "FlipX", layer.FlipX ? "true" : null, null );
			Field( text, name, "FlipY", layer.FlipY ? "true" : null, null );
			Field( text, name, "Color", ScriptLiteral.Colour( layer.Color ), ScriptLiteral.Colour( plain.Color ) );
			Field( text, name, "Opacity", ScriptLiteral.Number( layer.Opacity ), ScriptLiteral.Number( plain.Opacity ) );
			Field( text, name, "OutlineColor", ScriptLiteral.Colour( layer.OutlineColor ), ScriptLiteral.Colour( plain.OutlineColor ) );
			Field( text, name, "OutlineWidth", ScriptLiteral.Number( layer.OutlineWidth ), ScriptLiteral.Number( plain.OutlineWidth ) );
			Field( text, name, "Animation", layer.Animation is null ? null : "Animation." + layer.Animation, null );
			Field( text, name, "Palette", layer.Palette is null ? null : ScriptLiteral.Palette( layer.Palette ), null );
			Field( text, name, "AnimationSpeed", ScriptLiteral.Number( layer.AnimationSpeed ), ScriptLiteral.Number( plain.AnimationSpeed ) );
			Field( text, name, "Hidden", layer.Hidden ? "true" : null, null );
			Field( text, name, "Eraser", layer.Eraser ? "true" : null, null );
			Field( text, name, "Side", layer.Side != EmblemLayer.BothLooks ? layer.Side.ToString( System.Globalization.CultureInfo.InvariantCulture ) : null, null );
		}
	}

	private static string Vector( float x, float y, float z ) =>
		$"Vector3( {ScriptLiteral.Number( x )}, {ScriptLiteral.Number( y )}, {ScriptLiteral.Number( z )} )";
}
