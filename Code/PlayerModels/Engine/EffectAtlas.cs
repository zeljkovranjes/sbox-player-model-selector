using System;
using System.Collections.Generic;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The pictures effects' sprites are drawn with, the curated ones and the standard shapes (see
/// <see cref="StandardShapes.Picture"/>): each drawn on this client from its lines (see <see cref="EffectShape"/>)
/// into a slot of its own the first time a layer needs it, so nothing is shipped, published or downloaded, and kept for
/// every effect of every player. A slot is a small texture white all over with the shape in its alpha, for each layer
/// to tint. Sprites draw bindless and batch by blending alone, not by texture, so every effect's sprites still batch
/// together; a texture made at runtime can't be cut into frames for particles, which is why a slot isn't a piece of
/// one big texture.
/// </summary>
internal static class EffectAtlas
{
	// A sprite covers a few hundred pixels of the screen at most
	private const int SlotSize = 64;

	private sealed record Slot( Texture Texture, Sprite Sprite );

	private static readonly Dictionary<string, Slot> Slots = new();

	/// <summary>The sprite of <paramref name="layer"/>'s picture: a curated one, or a standard shape.</summary>
	internal static Sprite SpriteOf( EffectLayer layer ) => SlotOf( layer ).Sprite;

	/// <summary>The texture of <paramref name="layer"/>'s picture, for a tile on the page.</summary>
	internal static Texture TextureOf( EffectLayer layer ) => SlotOf( layer ).Texture;

	/// <summary>
	/// The sprite of the standard shape <paramref name="shape"/>: what a drawn shape (see <see cref="ShapeTextures"/>)
	/// shows as while it isn't drawn.
	/// </summary>
	internal static Sprite SpriteOf( StandardShape shape ) => SlotOf( shape, null ).Sprite;

	/// <inheritdoc cref="SpriteOf(StandardShape)"/>
	internal static Texture TextureOf( StandardShape shape ) => SlotOf( shape, null ).Texture;

	/// <summary>
	/// The texture of the picture or standard shape named <paramref name="name"/> (a sticker's); a star for a name that is
	/// neither.
	/// </summary>
	internal static Texture TextureOf( string name ) => SlotNamed( name ).Texture;

	/// <summary>The sprite of the picture or standard shape named <paramref name="name"/> (one of a layer's pictures); a star for a name that is neither.</summary>
	internal static Sprite SpriteOf( string name ) => SlotNamed( name ).Sprite;

	private static Slot SlotNamed( string name )
	{
		if ( Enum.TryParse<LayerPicture>( name, out var picture ) && Enum.IsDefined( picture ) )
			return SlotOf( null, picture );

		return SlotOf( Enum.TryParse<StandardShape>( name, out var shape ) && Enum.IsDefined( shape ) ? shape : StandardShape.Star5, null );
	}

	// A slash's reach and thickness are drawn in steps this fine, so its pictures stay few however a layer sets them
	private const float SlashArcStep = 15f;
	private const float SlashWidthStep = 0.05f;

	private static Slot SlotOf( EffectLayer layer )
	{
		if ( layer.PictureShape is null && layer.PictureIs == LayerPicture.Slash )
			return SlashSlot( layer.SlashShape.Arc, layer.SlashShape.Width );

		return SlotOf( layer.PictureShape, layer.PictureIs );
	}

	// A slash of the reach and thickness given, each drawn once
	private static Slot SlashSlot( float arc, float width )
	{
		var steppedArc = MathF.Round( arc / SlashArcStep ) * SlashArcStep;
		var steppedWidth = MathF.Round( width / SlashWidthStep ) * SlashWidthStep;
		var name = $"slash_{steppedArc:0}_{steppedWidth * 100f:0}";
		if ( Slots.TryGetValue( name, out var slot ) && slot.Texture.IsValid() )
			return slot;

		var texture = Draw( EffectShape.Slash( steppedArc, steppedWidth ), name );
		slot = new Slot( texture, Sprite.FromTexture( texture ) );
		Slots[ name ] = slot;
		return slot;
	}

	private static Slot SlotOf( StandardShape? shape, LayerPicture? picture )
	{
		var name = shape?.ToString() ?? picture.GetValueOrDefault().ToString();
		if ( Slots.TryGetValue( name, out var slot ) && slot.Texture.IsValid() )
			return slot;

		var texture = Draw( shape is { } standard ? StandardShapes.Picture( standard ) : EffectShape.Of( picture.GetValueOrDefault() ), name );
		slot = new Slot( texture, Sprite.FromTexture( texture ) );
		Slots[ name ] = slot;
		return slot;
	}

	private static Texture Draw( EffectShape shape, string name )
	{
		var coverage = shape.Rasterize( SlotSize );
		var pixels = new byte[ coverage.Length * 4 ];
		for ( var i = 0; i < coverage.Length; i++ )
		{
			pixels[ i * 4 ] = pixels[ i * 4 + 1 ] = pixels[ i * 4 + 2 ] = 255;
			pixels[ i * 4 + 3 ] = (byte)(coverage[ i ] * 255f + 0.5f);
		}

		return Texture.Create( SlotSize, SlotSize ).WithName( $"effect_{name.ToLowerInvariant()}" ).WithData( pixels ).Finish();
	}
}
