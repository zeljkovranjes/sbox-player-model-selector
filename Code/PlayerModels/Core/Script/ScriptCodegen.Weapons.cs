using System;
using System.Collections.Generic;
using System.Text;

namespace PlayerModels.Core.Script;

public static partial class ScriptCodegen
{
	// A weapon's and its skin's fields, in the order a design script writes them
	[Sandbox.SkipHotload]
	private static readonly PropertyId[] WeaponFields = { PropertyId.WeaponFinish, PropertyId.WeaponTint };

	[Sandbox.SkipHotload]
	private static readonly PropertyId[] SkinFields =
	{
		PropertyId.SkinPattern, PropertyId.SkinPatternColor, PropertyId.SkinPatternColorEnd, PropertyId.SkinPatternScale, PropertyId.SkinPatternTurn,
		PropertyId.SkinPatternScroll, PropertyId.SkinPatternSpin, PropertyId.SkinPatternShape, PropertyId.SkinAnimation, PropertyId.SkinSpeed, PropertyId.SkinBands,
		PropertyId.SkinPalette,
	};

	/// <summary>A sticker's fields after its picture, a prop's after its model, an effect's after what it is, as written.</summary>
	[Sandbox.SkipHotload]
	internal static readonly PropertyId[] StickerFields =
	{
		PropertyId.StickerColor, PropertyId.StickerAnimation, PropertyId.StickerPalette, PropertyId.StickerSpeed, PropertyId.StickerPosition, PropertyId.StickerRotation,
		PropertyId.StickerSize, PropertyId.StickerFinish, PropertyId.StickerScrape, PropertyId.StickerBorder,
	};

	[Sandbox.SkipHotload]
	internal static readonly PropertyId[] PropFields =
	{
		PropertyId.PropPart, PropertyId.PropReplaces, PropertyId.PropPosition, PropertyId.PropRotation, PropertyId.PropScale, PropertyId.PropNudge, PropertyId.PropNudgeRotation,
	};

	[Sandbox.SkipHotload]
	internal static readonly PropertyId[] EffectFields =
	{
		PropertyId.EffectName, PropertyId.EffectOff, PropertyId.EffectHue, PropertyId.EffectSize, PropertyId.EffectAmount, PropertyId.EffectSpeed, PropertyId.EffectWhen,
	};

	/// <summary>The fields of the weapon and its skin, as written.</summary>
	internal static IEnumerable<PropertyId> DesignFields()
	{
		foreach ( var field in WeaponFields )
			yield return field;

		foreach ( var field in SkinFields )
			yield return field;
	}

	/// <summary>
	/// <paramref name="design"/> as a weapon design script, in the canonical style: the weapon's finish and tint, its skin,
	/// then a block for each sticker, prop, hidden part and effect, each field only when it isn't the default. The drawn
	/// shapes its stickers and pattern name are its own (<see cref="WeaponDesign.Shapes"/>) or, in a look,
	/// <paramref name="shapes"/>. An effect that is a stock preset is named as one, with what was changed on it; any other
	/// by its name, from the copy the design carries. Building the script gives back the same design.
	/// </summary>
	public static string Weapon( WeaponDesign design, IReadOnlyList<DrawnShape> shapes = null )
	{
		var text = new StringBuilder();
		var names = new HashSet<string> { "Weapon", "Skin" };
		var drawn = new Dictionary<string, string>();
		var pictures = new List<string>();
		foreach ( var sticker in design.Stickers ?? new List<WeaponSticker>() )
			pictures.Add( sticker.Picture );

		pictures.Add( design.Skin?.PatternShape );
		foreach ( var reference in pictures )
		{
			if ( !DrawnShape.IsReference( reference ) || drawn.ContainsKey( reference ) || Find( reference, design.Shapes, shapes ) is not { } shape )
				continue;

			var name = Unique( names, Identifier( shape.Name, "shape" ) );
			drawn[ reference ] = name;
			Paragraph( text );
			WriteShape( text, name, shape );
		}

		Func<string, string> shapeName = x => drawn.TryGetValue( x, out var variable ) ? variable : null;
		var plain = new WeaponDesign { Skin = new WeaponSkin() };
		var block = new StringBuilder();
		foreach ( var field in WeaponFields )
			Set( block, "Weapon", field, ScriptLiteral.Of( field, design ), ScriptLiteral.Of( field, plain ) );

		Block( text, block );
		foreach ( var field in SkinFields )
			Set( block, "Skin", field, ScriptLiteral.Of( field, design, 0, shapeName ), ScriptLiteral.Of( field, plain, 0, shapeName ) );

		Block( text, block );
		for ( var i = 0; i < (design.Stickers?.Count ?? 0); i++ )
		{
			var sticker = design.Stickers[ i ];
			var name = Unique( names, Identifier( StickerName( sticker.Picture, shapeName ), "sticker" ) );
			block.Append( "local " ).Append( name ).Append( " = Weapon.AddSticker( " ).Append( ScriptLiteral.Of( PropertyId.StickerPicture, design, i, shapeName ) ).Append( " )\n" );
			var stock = new WeaponDesign { Stickers = new List<WeaponSticker> { new() { Picture = sticker.Picture } } };
			foreach ( var field in StickerFields )
				Set( block, name, field, ScriptLiteral.Of( field, design, i, shapeName ), ScriptLiteral.Of( field, stock, 0, shapeName ) );

			Block( text, block );
		}

		for ( var i = 0; i < (design.Props?.Count ?? 0); i++ )
		{
			var prop = design.Props[ i ];
			var name = Unique( names, Identifier( ModelName( prop.Model ), "prop" ) );
			block.Append( "local " ).Append( name ).Append( " = Weapon.AddProp( " ).Append( ScriptLiteral.Text( prop.Model ) ).Append( " )\n" );
			var stock = new WeaponDesign { Props = new List<WeaponProp> { new() { Model = prop.Model } } };
			foreach ( var field in PropFields )
				Set( block, name, field, ScriptLiteral.Of( field, design, i ), ScriptLiteral.Of( field, stock, 0 ) );

			Block( text, block );
		}

		foreach ( var part in design.HiddenParts ?? new List<string>() )
			block.Append( "Weapon.HidePart( " ).Append( ScriptLiteral.Text( part ) ).Append( " )\n" );

		Block( text, block );
		for ( var i = 0; i < (design.Effects?.Count ?? 0); i++ )
		{
			var effect = design.Effects[ i ];
			var name = Unique( names, Identifier( effect.Name, "effect" ) );
			var preset = WeaponScripts.PresetOf( effect );
			var from = preset is null ? $"Effects.Mine( {ScriptLiteral.Text( effect.Name ?? "" )} )" : $"Effects.Preset( {ScriptLiteral.Text( preset.Name )} )";
			block.Append( "local " ).Append( name ).Append( " = Weapon.AddEffect( " ).Append( from ).Append( " )\n" );

			// What was changed on a preset; the player's own is carried as it is
			var stock = new WeaponDesign { Effects = new List<Effect> { (preset ?? effect).Sanitized() ?? effect } };
			foreach ( var field in EffectFields )
				Set( block, name, field, ScriptLiteral.Of( field, design, i ), ScriptLiteral.Of( field, stock, 0 ) );

			Block( text, block );
		}

		return text.ToString();
	}

	/// <summary>One sticker, prop or effect of <paramref name="design"/> as a block of its script, kept in <paramref name="name"/>.</summary>
	public static string WeaponItem( WeaponDesign design, PropertyId kind, int index, string name, Func<string, string> shapeName = null )
	{
		var single = new WeaponDesign
		{
			Stickers = kind == PropertyId.StickerPicture ? new List<WeaponSticker> { design.Stickers[ index ] } : null,
			Props = kind == PropertyId.PropModel ? new List<WeaponProp> { design.Props[ index ] } : null,
			Effects = kind == PropertyId.EffectName ? new List<Effect> { design.Effects[ index ] } : null,
		};

		var text = Weapon( single, null );
		var made = text.IndexOf( " = ", StringComparison.Ordinal );
		var local = text.IndexOf( "local ", StringComparison.Ordinal );
		if ( made < 0 || local < 0 )
			return text;

		var old = text[ (local + 6)..made ];
		return text.Replace( "local " + old + " = ", "local " + name + " = " ).Replace( "\n" + old + ".", "\n" + name + "." );
	}

	private static void Set( StringBuilder text, string owner, PropertyId field, string value, string otherwise )
	{
		if ( value is not null && value != otherwise )
			text.Append( owner ).Append( '.' ).Append( ScriptProperties.Of( field ).Name ).Append( " = " ).Append( value ).Append( '\n' );
	}

	// A block of lines, a blank line before it when it isn't the first
	private static void Block( StringBuilder text, StringBuilder block )
	{
		if ( block.Length == 0 )
			return;

		Paragraph( text );
		text.Append( block );
		block.Clear();
	}

	private static string StickerName( string picture, Func<string, string> shapeName ) =>
		DrawnShape.IsReference( picture ) ? shapeName( picture ) ?? "sticker" : picture ?? "sticker";

	// "models/sbox_props/cola_can/cola_can.vmdl" is "cola can"
	private static string ModelName( string model )
	{
		var start = (model ?? "").LastIndexOfAny( new[] { '/', '\\' } ) + 1;
		var end = (model ?? "").LastIndexOf( '.' );
		return end > start ? model[ start..end ].Replace( '_', ' ' ) : "prop";
	}
}
