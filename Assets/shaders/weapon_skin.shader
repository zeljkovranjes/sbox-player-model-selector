HEADER
{
	Description = "Player Model Selector: a weapon's skin. Its own surface (colour, bumps, roughness, metalness, shadows, glow), tinted and finished, with a pattern laid over it from every side, colours moving along it and stickers pressed onto it, worked out from the model's own space so no UVs are needed";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth( S_MODE_DEPTH );
	ToolsShadingComplexity( "tools_shading_complexity.shader" );
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"

	// Where the surface is in the model's own space, and which way it faces there: the pattern sticks to the weapon
	float3 vPositionOs : TEXCOORD8;
	float3 vNormalOs : TEXCOORD9;
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		o.vPositionOs = i.vPositionOs.xyz;
		float3 normalOs;
		float4 tangentUOs;
		VS_DecodeObjectSpaceNormalAndTangent( i, normalOs, tangentUOs );
		o.vNormalOs = normalOs;
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/utils/Material.CommonInputs.hlsl"
	#include "common/pixel.hlsl"

	// The weapon's own surface, read the way its own material reads it, so a skinned weapon is the same gun underneath:
	// its colour, bumps, how rough and metal it is, its shadows and its glow, given as that material's own textures
	// (g_tColor, g_tNormal, g_tRma, and its glow's mask). A complex material (most weapons) keeps its bumps in two channels
	// with its roughness in a third, its metalness in its colour's alpha (or as one number) and its shadows in a texture of
	// their own (given as g_tRma); a standard one keeps roughness, metalness and shadows in g_tRma; anything else gives its
	// colour alone. Which: 0 colour alone, 1 complex, 2 standard
	CreateInputTexture2D( TextureSourceGlow, Srgb, 8, "", "_selfillum", "Material,10/80", Default3( 0.0, 0.0, 0.0 ) );
	Texture2D g_tSourceGlow < Channel( RGB, Box( TextureSourceGlow ), Srgb ); OutputFormat( BC7 ); SrgbRead( true ); >;
	float g_flSourceKind < Default( 0.0 ); Range( 0.0, 2.0 ); >;
	float g_flSourceHasNormal < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	float g_flSourceMetalness < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	float g_flMetalnessInColor < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	float g_flSourceRoughnessPower < Default( 1.0 ); Range( 0.0, 8.0 ); >;
	float3 g_vSourceTint < Default3( 1.0, 1.0, 1.0 ); >;
	float3 g_vSourceGlow < Default3( 0.0, 0.0, 0.0 ); >;
	float g_flSourceGlowAlbedo < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	float g_flAlphaTest < Default( 0.0 ); Range( 0.0, 1.0 ); >;

	// The finish, when there is one: its colour (the tint over it), how metal and how rough, in place of the surface's own
	float3 g_vSkinTint < Default3( 1.0, 1.0, 1.0 ); >;
	float g_flFinished < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	float g_flSkinMetalness < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	float g_flSkinRoughness < Default( 0.6 ); Range( 0.0, 1.0 ); >;

	// The model's bounds in its own space: its middle, its size, the long way along it and where it starts and ends
	float3 g_vCenter < Default3( 0.0, 0.0, 0.0 ); >;
	float3 g_vAxis < Default3( 1.0, 0.0, 0.0 ); >;
	float2 g_vAxisRange < Default2( -16.0, 16.0 ); >;

	// The pattern: which (0 none, 1 stripes, 2 camo, 3 checker, 4 gradient, 5 carbon, 6 shapes), its colours, how big
	// one repeat is in units and how far turned (radians); how far it has scrolled and spun is in the moving colours below
	float g_flPattern < Default( 0.0 ); Range( 0.0, 6.0 ); >;
	float3 g_vPatternA < Default3( 0.0, 0.0, 0.0 ); >;
	float3 g_vPatternB < Default3( 1.0, 1.0, 1.0 ); >;
	float g_flPatternSize < Default( 4.0 ); Range( 0.25, 64.0 ); >;
	float g_flPatternTurn < Default( 0.0 ); Range( 0.0, 3.2 ); >;
	CreateInputTexture2D( TexturePatternShape, Linear, 8, "", "_pattern", "Material,10/90", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	Texture2D g_tPatternShape < Channel( RGBA, Box( TexturePatternShape ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	SamplerState g_sPatternShape < Filter( BILINEAR ); AddressU( WRAP ); AddressV( WRAP ); >;

	// The moving colours, worked out each frame by the skin's scripts on the CPU into a small table (see SkinColours), one
	// for each different skin: for each place along the weapon (its band's own texel, or 64 blended along it) row 0 has the
	// colour painted over it and how much it covers, row 1 its glow and sheen, row 2 the shade its own colour is multiplied
	// by, row 3 how far its pattern has scrolled (repeats) and spun (turns). Without the table nothing moves
	CreateInputTexture2D( TextureSkinColours, Linear, 8, "", "_skincolours", "Material,10/95", Default4( 0.0, 0.0, 0.0, 0.0 ) );
	Texture2D g_tSkinColours < Channel( RGBA, Box( TextureSkinColours ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	SamplerState g_sSkinColours < Filter( BILINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	float g_flSkinMoving < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	float g_flSkinBanded < Default( 1.0 ); Range( 0.0, 1.0 ); >;
	float g_flSkinBands < Default( 8.0 ); Range( 1.0, 32.0 ); >;

	// Stickers, up to eight, each pressed onto the surface from one side as CS:GO's are: part of the weapon's own surface,
	// so lit as it is, following its shape, and never on anything else (a hand holding it). Each has where it is (the model's
	// space to the sticker's: x into it, y across, z up, its box -0.5 to 0.5 each way), how it looks (its finish: 0 paper,
	// 1 holo, 2 foil, 3 glitter, 4 gold, 5 lenticular; how scraped, 0 to 1; where its wear is; a die-cut border), its
	// colour, and its pictures (bindless: a lenticular one's second look too)
	float g_flStickerCount < Default( 0.0 ); Range( 0.0, 8.0 ); >;
	SamplerState g_sSticker < Filter( ANISO ); AddressU( CLAMP ); AddressV( CLAMP ); >;
	float4 g_vStickerX0 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerY0 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerZ0 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerLook0 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerTint0 < Default4( 1.0, 1.0, 1.0, 1.0 ); >;
	float4 g_vStickerPictures0 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerX1 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerY1 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerZ1 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerLook1 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerTint1 < Default4( 1.0, 1.0, 1.0, 1.0 ); >;
	float4 g_vStickerPictures1 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerX2 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerY2 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerZ2 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerLook2 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerTint2 < Default4( 1.0, 1.0, 1.0, 1.0 ); >;
	float4 g_vStickerPictures2 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerX3 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerY3 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerZ3 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerLook3 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerTint3 < Default4( 1.0, 1.0, 1.0, 1.0 ); >;
	float4 g_vStickerPictures3 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerX4 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerY4 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerZ4 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerLook4 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerTint4 < Default4( 1.0, 1.0, 1.0, 1.0 ); >;
	float4 g_vStickerPictures4 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerX5 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerY5 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerZ5 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerLook5 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerTint5 < Default4( 1.0, 1.0, 1.0, 1.0 ); >;
	float4 g_vStickerPictures5 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerX6 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerY6 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerZ6 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerLook6 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerTint6 < Default4( 1.0, 1.0, 1.0, 1.0 ); >;
	float4 g_vStickerPictures6 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerX7 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerY7 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerZ7 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerLook7 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vStickerTint7 < Default4( 1.0, 1.0, 1.0, 1.0 ); >;
	float4 g_vStickerPictures7 < Default4( 0.0, 0.0, 0.0, 0.0 ); >;

	float Mod( float a, float b ) { return a - b * floor( a / b ); }

	float Hash( float3 p )
	{
		p = frac( p * 0.3183099 + 0.1 );
		p *= 17.0;
		return frac( p.x * p.y * p.z * ( p.x + p.y + p.z ) );
	}

	// Smooth value noise in 3D, 0 to 1
	float Noise( float3 p )
	{
		float3 cell = floor( p );
		float3 f = frac( p );
		f = f * f * ( 3.0 - 2.0 * f );
		return lerp( lerp( lerp( Hash( cell ), Hash( cell + float3( 1, 0, 0 ) ), f.x ), lerp( Hash( cell + float3( 0, 1, 0 ) ), Hash( cell + float3( 1, 1, 0 ) ), f.x ), f.y ),
			lerp( lerp( Hash( cell + float3( 0, 0, 1 ) ), Hash( cell + float3( 1, 0, 1 ) ), f.x ), lerp( Hash( cell + float3( 0, 1, 1 ) ), Hash( cell + float3( 1, 1, 1 ) ), f.x ), f.y ), f.z );
	}

	// One side's pattern, 0 (its first colour) to 1 (its second), at a point on that side in repeats
	float PatternOnSide( float2 uv, float pattern )
	{
		if ( pattern < 1.5 )
			return step( 0.5, frac( uv.x ) );

		if ( pattern < 3.5 )
			return Mod( floor( uv.x ) + floor( uv.y ), 2.0 );

		if ( pattern < 5.5 )
		{
			// Carbon fibre: a twill of short bands, each shaded across its width like woven strands
			float2 cell = floor( uv * 4.0 );
			float across = Mod( cell.x + cell.y, 2.0 ) > 0.5 ? frac( uv.x * 4.0 ) : frac( uv.y * 4.0 );
			return 0.25 + 0.75 * sin( across * 3.14159 );
		}

		return g_tPatternShape.Sample( g_sPatternShape, frac( uv ) * float2( 1.0, -1.0 ) ).a;
	}

	// The pattern at a point, 0 to 1: a gradient along the weapon, camo through it, the rest from every side
	float PatternAt( float3 position, float3 normal, float along, float scroll, float spin )
	{
		float pattern = g_flPattern;
		if ( pattern > 3.5 && pattern < 4.5 )
			return saturate( frac( along - scroll * 0.25 ) );

		float3 p = ( position - g_vCenter ) / g_flPatternSize;
		if ( pattern > 1.5 && pattern < 2.5 )
		{
			float3 drift = g_vAxis * scroll;
			float n = Noise( p * 1.3 + drift ) * 0.65 + Noise( p * 2.9 + drift + 7.0 ) * 0.35;
			return smoothstep( 0.47, 0.53, n );
		}

		float angle = g_flPatternTurn + spin * 6.28318;
		float2x2 turn = float2x2( cos( angle ), -sin( angle ), sin( angle ), cos( angle ) );
		float3 weights = pow( abs( normal ), 4.0 );
		weights /= max( dot( weights, 1.0 ), 0.0001 );

		float value = PatternOnSide( mul( turn, p.yz ) + float2( scroll, 0.0 ), pattern ) * weights.x;
		value += PatternOnSide( mul( turn, p.xz ) + float2( scroll, 0.0 ), pattern ) * weights.y;
		value += PatternOnSide( mul( turn, p.xy ) + float2( scroll, 0.0 ), pattern ) * weights.z;
		return value;
	}

	float3 Hue( float hue )
	{
		float3 rgb = saturate( abs( frac( hue + float3( 0.0, 2.0 / 3.0, 1.0 / 3.0 ) ) * 6.0 - 3.0 ) - 1.0 );
		return rgb;
	}

	// A skin's glow is as bright as the weapon's own surface: this much where it is black, all of it where its colour is a
	// third as bright as white or more
	static const float GlowFloor = 0.35;
	static const float GlowDetail = 3.0;

	// The moving colours at a point along the weapon (see g_tSkinColours): a band's own texel, or blended between the 64
	void SkinAt( float along, out float4 paint, out float4 glow, out float4 shade, out float4 travel )
	{
		if ( g_flSkinBanded > 0.5 )
		{
			int texel = (int)min( floor( along * g_flSkinBands ), g_flSkinBands - 1.0 );
			paint = g_tSkinColours.Load( int3( texel, 0, 0 ) );
			glow = g_tSkinColours.Load( int3( texel, 1, 0 ) );
			shade = g_tSkinColours.Load( int3( texel, 2, 0 ) );
			travel = g_tSkinColours.Load( int3( texel, 3, 0 ) );
			return;
		}

		paint = g_tSkinColours.SampleLevel( g_sSkinColours, float2( along, 0.125 ), 0 );
		glow = g_tSkinColours.SampleLevel( g_sSkinColours, float2( along, 0.375 ), 0 );
		shade = g_tSkinColours.SampleLevel( g_sSkinColours, float2( along, 0.625 ), 0 );
		travel = g_tSkinColours.SampleLevel( g_sSkinColours, float2( along, 0.875 ), 0 );
	}

	// How much longer the way along a surface is than its straight projection, where the surface has turned so its normal
	// leans sideways by this much (the sine of the turn): the angle over its sine, 1 on a flat face
	float Unrolled( float lean )
	{
		float s = min( abs( lean ), 0.999 );
		return s < 0.001 ? 1.0 : asin( s ) / s;
	}

	// A sticker over the surface where it is pressed on: its colour (its finish's, by the angle it is seen from) over the
	// surface's, as rough and metal as its finish, scraped away in patches as worn as it is, with a white border cut round it
	void Sticker( inout float3 albedo, inout float metal, inout float rough, inout float3 emission, float3 positionOs, float3 normalOs,
		float3 toCamera, float3 normalWs, float3 tangentWs, float4 rowX, float4 rowY, float4 rowZ, float4 look, float4 tint, float4 pictures )
	{
		if ( pictures.x < 0.5 )
			return;

		float3 p = float3( dot( rowX.xyz, positionOs ) + rowX.w, dot( rowY.xyz, positionOs ) + rowY.w, dot( rowZ.xyz, positionOs ) + rowZ.w );
		if ( abs( p.x ) > 0.5 || abs( p.y ) > 0.5 || abs( p.z ) > 0.5 )
			return;

		// Only on the faces turned towards where it is pressed from, fading out as they turn away, so it never smears along
		// a curve or the side of a rail; and it fades towards the faces of its slab rather than stopping in a hard line
		float pressedOn = smoothstep( 0.5, 0.75, -dot( normalOs, normalize( rowX.xyz ) ) );
		if ( pressedOn <= 0.0 )
			return;

		pressedOn *= 1.0 - smoothstep( 0.35, 0.5, abs( p.x ) );

		// Unrolled over a curve: the picture keeps its size along the surface, as a real sticker wraps a barrel, rather than
		// stretching where the surface turns away (exact on a cylinder or a ball); clipped at its own edges
		float2 turn = float2( dot( normalOs, normalize( rowY.xyz ) ), dot( normalOs, normalize( rowZ.xyz ) ) );
		float2 uv = float2( 0.5 - p.y * Unrolled( turn.x ), 0.5 - p.z * Unrolled( turn.y ) );
		if ( any( uv < 0.0 ) || any( uv > 1.0 ) )
			return;
		float finish = look.x;
		float facing = dot( toCamera, normalWs );
		float across = dot( toCamera, tangentWs );
		Texture2D first = Bindless::GetTexture2D( (int)pictures.x );
		float4 colour = first.Sample( g_sSticker, uv );
		if ( finish > 4.5 && pictures.y > 0.5 )
			colour = lerp( colour, Bindless::GetTexture2D( (int)pictures.y ).Sample( g_sSticker, uv ), smoothstep( -0.12, 0.12, across ) );

		// The pictures are drawn in sRGB; the surface is lit in linear light
		colour.rgb = pow( colour.rgb, 2.2 );

		float alpha = colour.a;
		float3 rgb = colour.rgb * tint.rgb;
		if ( look.w > 0.5 )
		{
			// A die-cut border: white where the picture reaches within a little of a point, but not over it
			float reach = 0.035;
			float around = 0.0;
			around = max( around, first.Sample( g_sSticker, uv + float2( reach, 0.0 ) ).a );
			around = max( around, first.Sample( g_sSticker, uv - float2( reach, 0.0 ) ).a );
			around = max( around, first.Sample( g_sSticker, uv + float2( 0.0, reach ) ).a );
			around = max( around, first.Sample( g_sSticker, uv - float2( 0.0, reach ) ).a );
			around = max( around, first.Sample( g_sSticker, uv + float2( reach, reach ) * 0.7071 ).a );
			around = max( around, first.Sample( g_sSticker, uv - float2( reach, reach ) * 0.7071 ).a );
			around = max( around, first.Sample( g_sSticker, uv + float2( reach, -reach ) * 0.7071 ).a );
			around = max( around, first.Sample( g_sSticker, uv - float2( reach, -reach ) * 0.7071 ).a );
			float edge = smoothstep( 0.35, 0.65, around );
			rgb = lerp( float3( 0.92, 0.92, 0.9 ), rgb, alpha );
			alpha = max( alpha, edge );
		}

		// Worn: patches scraped away, smallest first as it wears, the same patches on every machine
		if ( look.y > 0.0 )
		{
			float worn = Noise( positionOs * 0.8 + look.z ) * 0.55 + Noise( positionOs * 2.6 + look.z * 1.7 ) * 0.3 + Noise( positionOs * 9.0 + look.z * 3.1 ) * 0.15;
			alpha *= smoothstep( look.y * 0.85 - 0.03, look.y * 0.85 + 0.03, worn );
		}

		alpha *= pressedOn;
		if ( alpha <= 0.001 )
			return;

		float stickerMetal = 0.0;
		float stickerRough = 0.85;
		float3 glow = 0.0;
		if ( finish > 0.5 && finish < 1.5 )
		{
			// Holo: a rainbow film over the print, shifting as it turns
			float3 film = Hue( frac( facing * 1.7 + across * 1.3 + ( uv.x + uv.y ) * 0.6 ) );
			rgb = lerp( rgb, rgb * 0.5 + film * 0.8, 0.45 );
			stickerMetal = 0.55;
			stickerRough = 0.18;
			glow = film * 0.06;
		}
		else if ( finish > 1.5 && finish < 2.5 )
		{
			// Foil: bright metal under the print
			stickerMetal = 0.9;
			stickerRough = 0.15;
		}
		else if ( finish > 2.5 && finish < 3.5 )
		{
			// Glitter: flecks that catch the light one by one as it turns
			float2 cell = floor( uv * 96.0 );
			float twinkle = Hash( float3( cell, floor( facing * 18.0 + across * 18.0 ) ) );
			float fleck = step( 0.92, twinkle );
			stickerMetal = 0.6;
			stickerRough = 0.3;
			glow = ( rgb + 0.4 ) * fleck * 0.9;
		}
		else if ( finish > 3.5 && finish < 4.5 )
		{
			// Gold foil
			rgb *= float3( 1.0, 0.78, 0.36 ) * 1.2;
			stickerMetal = 1.0;
			stickerRough = 0.2;
		}
		else if ( finish > 4.5 )
		{
			stickerMetal = 0.15;
			stickerRough = 0.3;
		}

		// Covering what is under it, the weapon's own glow too (on the physgun it washed a sticker out), lit by that glow
		// in its own colour as if the weapon shone through it, so it reads on a glowing part however dark around it
		float under = dot( emission, float3( 0.3, 0.59, 0.11 ) );
		albedo = lerp( albedo, rgb, alpha );
		metal = lerp( metal, stickerMetal, alpha );
		rough = lerp( rough, stickerRough, alpha );
		emission = lerp( emission, glow + rgb * under, alpha );
	}

	// A complex material's bumps, kept as a point on a folded square (Source 2's hemi-octahedral encoding)
	float3 HemiOctahedronNormal( float2 folded )
	{
		float2 turned = float2( folded.x + folded.y - 1.00392163, folded.x - folded.y );
		return normalize( float3( turned, 1.0 - abs( turned.x ) - abs( turned.y ) ) );
	}

	// The weapon's own surface at a point: its colour (alpha for cut-outs), bumps, roughness, metalness, shadows and glow
	void Surface( PixelInput i, inout Material m, out float4 colour, out float3 glow )
	{
		float2 uv = i.vTextureCoords.xy;
		colour = g_tColor.Sample( TextureFiltering, uv );
		float4 normalTexel = g_tNormal.Sample( TextureFiltering, uv );
		float4 rma = g_tRma.Sample( TextureFiltering, uv );
		bool complex = g_flSourceKind > 0.5 && g_flSourceKind < 1.5;
		bool standard = g_flSourceKind > 1.5;

		if ( g_flSourceHasNormal > 0.5 )
		{
			float3 normalTs = complex ? HemiOctahedronNormal( normalTexel.rg ) : DecodeNormal( normalTexel.rgb );
			m.Normal = TransformNormal( normalTs, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		}

		m.Roughness = complex && g_flSourceHasNormal > 0.5 ? pow( saturate( normalTexel.b ), g_flSourceRoughnessPower ) : standard ? rma.r : 0.6;
		m.Metalness = complex ? ( g_flMetalnessInColor > 0.5 ? colour.a : g_flSourceMetalness ) : standard ? rma.g : 0.0;
		m.AmbientOcclusion = complex ? rma.r : standard ? rma.b : 1.0;

		glow = g_tSourceGlow.Sample( TextureFiltering, uv ).rgb * g_vSourceGlow;
		glow = lerp( glow, glow * colour.rgb * g_vSourceTint, g_flSourceGlowAlbedo );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );
		float4 colour;
		float3 sourceGlow;
		Surface( i, m, colour, sourceGlow );
		if ( g_flAlphaTest > 0.5 )
			clip( colour.a - 0.5 );

		if ( g_flFinished > 0.5 )
		{
			m.Metalness = g_flSkinMetalness;
			m.Roughness = g_flSkinRoughness;
		}

		m.Opacity = 1.0;

		float3 albedo = colour.rgb * g_vSourceTint * g_vSkinTint;
		float shade = dot( colour.rgb, float3( 0.3, 0.59, 0.11 ) );
		float along = saturate( ( dot( i.vPositionOs, g_vAxis ) - g_vAxisRange.x ) / max( g_vAxisRange.y - g_vAxisRange.x, 0.001 ) );

		float4 paint = 0.0;
		float4 skinGlow = 0.0;
		float4 skinShade = 1.0;
		float4 travel = 0.0;
		if ( g_flSkinMoving > 0.5 )
			SkinAt( along, paint, skinGlow, skinShade, travel );

		// A pattern takes the place of the weapon's own colour, keeping a little of its light and dark
		if ( g_flPattern > 0.5 )
		{
			float value = PatternAt( i.vPositionOs, normalize( i.vNormalOs ), along, travel.x, travel.y );
			float3 painted = lerp( g_vPatternA, g_vPatternB, value ) * g_vSkinTint;
			albedo = painted * lerp( 1.0, saturate( shade * 2.2 ), 0.35 );
		}

		// The moving colours: the shade over its own colour, the paint over that keeping its light and dark, its glow (as
		// bright as its own surface is there, so the gun's detail still reads through it), and a sheen polishing it
		float3 emission = 0.0;
		if ( g_flSkinMoving > 0.5 )
		{
			float light = max( dot( albedo, float3( 0.3, 0.59, 0.11 ) ), 0.35 );
			albedo = lerp( albedo * skinShade.rgb, paint.rgb * saturate( light * 1.8 ), paint.a );
			emission = skinGlow.rgb * ( GlowFloor + ( 1.0 - GlowFloor ) * saturate( shade * GlowDetail ) );
			m.Roughness = lerp( m.Roughness, 0.1, skinGlow.a );
		}

		emission += sourceGlow;
		if ( g_flStickerCount > 0.5 )
		{
			float metal = m.Metalness;
			float rough = m.Roughness;
			float3 normalOs = normalize( i.vNormalOs );
			float3 toCamera = -normalize( i.vPositionWithOffsetWs.xyz );
			float3 normalWs = normalize( i.vNormalWs );
			float3 tangentWs = normalize( i.vTangentUWs );
			if ( g_flStickerCount > 0.5 ) Sticker( albedo, metal, rough, emission, i.vPositionOs, normalOs, toCamera, normalWs, tangentWs, g_vStickerX0, g_vStickerY0, g_vStickerZ0, g_vStickerLook0, g_vStickerTint0, g_vStickerPictures0 );
			if ( g_flStickerCount > 1.5 ) Sticker( albedo, metal, rough, emission, i.vPositionOs, normalOs, toCamera, normalWs, tangentWs, g_vStickerX1, g_vStickerY1, g_vStickerZ1, g_vStickerLook1, g_vStickerTint1, g_vStickerPictures1 );
			if ( g_flStickerCount > 2.5 ) Sticker( albedo, metal, rough, emission, i.vPositionOs, normalOs, toCamera, normalWs, tangentWs, g_vStickerX2, g_vStickerY2, g_vStickerZ2, g_vStickerLook2, g_vStickerTint2, g_vStickerPictures2 );
			if ( g_flStickerCount > 3.5 ) Sticker( albedo, metal, rough, emission, i.vPositionOs, normalOs, toCamera, normalWs, tangentWs, g_vStickerX3, g_vStickerY3, g_vStickerZ3, g_vStickerLook3, g_vStickerTint3, g_vStickerPictures3 );
			if ( g_flStickerCount > 4.5 ) Sticker( albedo, metal, rough, emission, i.vPositionOs, normalOs, toCamera, normalWs, tangentWs, g_vStickerX4, g_vStickerY4, g_vStickerZ4, g_vStickerLook4, g_vStickerTint4, g_vStickerPictures4 );
			if ( g_flStickerCount > 5.5 ) Sticker( albedo, metal, rough, emission, i.vPositionOs, normalOs, toCamera, normalWs, tangentWs, g_vStickerX5, g_vStickerY5, g_vStickerZ5, g_vStickerLook5, g_vStickerTint5, g_vStickerPictures5 );
			if ( g_flStickerCount > 6.5 ) Sticker( albedo, metal, rough, emission, i.vPositionOs, normalOs, toCamera, normalWs, tangentWs, g_vStickerX6, g_vStickerY6, g_vStickerZ6, g_vStickerLook6, g_vStickerTint6, g_vStickerPictures6 );
			if ( g_flStickerCount > 7.5 ) Sticker( albedo, metal, rough, emission, i.vPositionOs, normalOs, toCamera, normalWs, tangentWs, g_vStickerX7, g_vStickerY7, g_vStickerZ7, g_vStickerLook7, g_vStickerTint7, g_vStickerPictures7 );
			m.Metalness = metal;
			m.Roughness = rough;
		}

		m.Albedo = albedo;
		m.Emission = emission;
		return ShadingModelStandard::Shade( i, m );
	}
}
