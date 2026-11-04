using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PlayerModels.Core.Script;

/// <summary>
/// The literal a script writes for a value of the data (an <see cref="EffectLayer"/>'s or <see cref="Effect"/>'s field):
/// numbers as the shortest text that reads back the same float, colours as <c>Color.Parse( "#rrggbb" )</c>, choices as
/// <c>Joint.Head</c>, offsets as <c>Vector3( .. )</c>. Codegen writes every field with it, and the visual editor's edits
/// rewrite one with it, so both always agree; null for a field that isn't set.
/// </summary>
public static class ScriptLiteral
{
	/// <summary>A number as the shortest text that reads back as the same float.</summary>
	public static string Number( float value )
	{
		var text = value.ToString( "R", CultureInfo.InvariantCulture );
		return text.Contains( 'E' ) ? ((double)value).ToString( "0.##########", CultureInfo.InvariantCulture ) : text;
	}

	/// <summary>A coordinate of a drawn shape, to four places: enough to read back as the same quantized point.</summary>
	public static string Coordinate( float value ) => value.ToString( "0.####", CultureInfo.InvariantCulture );

	public static string Colour( string hex ) => $"Color.Parse( \"{hex}\" )";

	public static string Text( string text ) => ScriptValue.Quoted( text );

	public static string Bool( bool value ) => value ? "true" : "false";

	public static string Vector( float x, float y, float z ) => $"Vector3( {Number( x )}, {Number( y )}, {Number( z )} )";

	public static string Palette( IReadOnlyList<string> colours )
	{
		var text = new StringBuilder( "{ " );
		for ( var i = 0; i < colours.Count; i++ )
			text.Append( i > 0 ? ", " : "" ).Append( Colour( colours[ i ] ) );

		return text.Append( " }" ).ToString();
	}

	/// <summary>
	/// Layer <paramref name="layer"/>'s <paramref name="property"/> as a literal; a drawn shape it names by
	/// <paramref name="shapeName"/> (the script variable holding it). Null when it isn't set.
	/// </summary>
	public static string Of( PropertyId property, EffectLayer layer, Func<string, string> shapeName ) => property switch
	{
		PropertyId.LayerOff => Bool( layer.Off ),
		PropertyId.LayerJoint => Choice( "Joint", layer.Joint ),
		PropertyId.LayerComesFrom => Choice( "ComesFrom", layer.Shape ?? nameof( LayerShape.Point ) ),
		PropertyId.LayerSpread => Number( layer.Spread ),
		PropertyId.LayerOffset => Vector( layer.Forward, layer.Left, layer.Up ),
		PropertyId.LayerFacing => Choice( "Facing", layer.Facing ?? nameof( LayerFacing.Forward ) ),
		PropertyId.LayerAngle => Number( layer.Angle ),
		PropertyId.LayerToJoint => Choice( "Joint", layer.ToJoint ),
		PropertyId.LayerFormation => Shape( layer.Formation, shapeName ),
		PropertyId.LayerFill => Bool( layer.Fill ),
		PropertyId.LayerLooksLike => Picture( layer.Picture, shapeName ),
		PropertyId.LayerColor => Colour( layer.Color ),
		PropertyId.LayerColorEnd => layer.ColorEnd is null ? null : Colour( layer.ColorEnd ),
		PropertyId.LayerColorMode => Choice( "ColorMode", layer.ColorMode ?? nameof( LayerColorMode.Fixed ) ),
		PropertyId.LayerPalette => layer.Palette is null ? null : Palette( layer.Palette ),
		PropertyId.LayerSize => Number( layer.Size ),
		PropertyId.LayerSizeEnd => layer.SizeEnd is { } end ? Number( end ) : null,
		PropertyId.LayerSpeed => Number( layer.Speed ),
		PropertyId.LayerGravity => Number( layer.Gravity ),
		PropertyId.LayerLifetime => Number( layer.Lifetime ),
		PropertyId.LayerAmount => Number( layer.Amount ),
		PropertyId.LayerFollow => Bool( layer.Follow ),
		PropertyId.LayerSolid => Bool( layer.Solid ),
		PropertyId.LayerRhythm => Choice( "Rhythm", layer.Rhythm ?? nameof( LayerRhythm.Steady ) ),
		PropertyId.LayerRhythmSpeed => Number( layer.RhythmSpeed ),
		PropertyId.LayerRhythmAmount => Number( layer.RhythmAmount ),
		PropertyId.LayerWhen => Choice( "When", layer.When ?? nameof( LayerWhen.Always ) ),
		PropertyId.LayerBurst => Number( layer.Burst ),
		PropertyId.LayerShowFor => Number( layer.ShowFor ),
		PropertyId.LayerFadeWithSpeed => Bool( layer.FadeWithSpeed ),
		PropertyId.LayerKeep => Number( layer.Keep ),
		PropertyId.LayerAlign => Choice( "Align", layer.Align ?? nameof( LayerAlign.Camera ) ),
		PropertyId.LayerPictures => layer.Pictures is null ? null : Pictures( layer.Pictures, shapeName ),
		_ => null,
	};

	/// <summary>The effect's own <paramref name="property"/> as a literal; null when it isn't set.</summary>
	public static string Of( PropertyId property, Effect effect )
	{
		var anchor = effect.Anchor ?? new EffectAnchor();
		return property switch
		{
			PropertyId.EffectName => Text( effect.Name ?? "" ),
			PropertyId.EffectKey => effect.Key is null ? null : Text( effect.Key ),
			PropertyId.EffectOff => Bool( effect.Off ),
			PropertyId.EffectAnchor => Choice( "Anchor", anchor.Kind ?? nameof( AnchorKind.Joint ) ),
			PropertyId.EffectJoint => Choice( "Joint", anchor.Joint ),
			PropertyId.EffectItem => anchor.Item is null ? null : Text( anchor.Item ),
			PropertyId.EffectOffset => Vector( anchor.Forward, anchor.Left, anchor.Up ),
			PropertyId.EffectHue => Number( effect.Hue ),
			PropertyId.EffectSize => Number( effect.Size ),
			PropertyId.EffectAmount => Number( effect.Amount ),
			PropertyId.EffectSpeed => Number( effect.Speed ),
			_ => null,
		};
	}

	// A list of pictures, or null when one of them can't be written (a drawn shape with no name in the script)
	private static string Pictures( IReadOnlyList<string> names, Func<string, string> shapeName )
	{
		var text = new StringBuilder( "{ " );
		for ( var i = 0; i < names.Count; i++ )
		{
			if ( Picture( names[ i ], shapeName ) is not { } picture )
				return null;

			text.Append( i > 0 ? ", " : "" ).Append( picture );
		}

		return text.Append( " }" ).ToString();
	}

	private static string Choice( string choices, string member ) => member is null ? null : choices + "." + member;

	private static string Shape( string name, Func<string, string> shapeName )
	{
		if ( name is null )
			return null;

		return DrawnShape.IsReference( name ) ? shapeName?.Invoke( name ) : "Shapes." + name;
	}

	private static string Picture( string name, Func<string, string> shapeName )
	{
		if ( name is null )
			return "Picture." + nameof( LayerPicture.Glow );

		return Enum.TryParse<LayerPicture>( name, false, out var picture ) && Enum.IsDefined( picture ) ? "Picture." + name : Shape( name, shapeName );
	}
}
