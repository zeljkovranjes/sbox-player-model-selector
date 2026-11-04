using System.Collections.Generic;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The pictures effects' sprites are drawn with: each drawn on this client from its lines (see <see cref="EffectShape"/>)
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

	/// <summary>The sprite of the curated picture <paramref name="picture"/>.</summary>
	internal static Sprite SpriteOf( LayerPicture picture ) => SlotOf( picture ).Sprite;

	/// <summary>The texture of <paramref name="picture"/>, for a tile on the page.</summary>
	internal static Texture TextureOf( LayerPicture picture ) => SlotOf( picture ).Texture;

	private static Slot SlotOf( LayerPicture picture )
	{
		var name = picture.ToString();
		if ( Slots.TryGetValue( name, out var slot ) && slot.Texture.IsValid() )
			return slot;

		var texture = Draw( EffectShape.Of( picture ), name );
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
