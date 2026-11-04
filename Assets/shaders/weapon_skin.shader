HEADER
{
	Description = "Player Model Selector: a weapon's skin. Its own colour and bumps, tinted and finished, with a pattern laid over it from every side and colours moving along it, worked out from the model's own space so no UVs are needed";
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

	// The finish: its colour (the tint over it), how metal and how rough
	float3 g_vSkinTint < Default3( 1.0, 1.0, 1.0 ); >;
	float g_flSkinMetalness < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	float g_flSkinRoughness < Default( 0.6 ); Range( 0.0, 1.0 ); >;
	float g_flAlphaTest < Default( 0.0 ); Range( 0.0, 1.0 ); >;

	// The model's bounds in its own space: its middle, its size, the long way along it and where it starts and ends
	float3 g_vCenter < Default3( 0.0, 0.0, 0.0 ); >;
	float3 g_vAxis < Default3( 1.0, 0.0, 0.0 ); >;
	float2 g_vAxisRange < Default2( -16.0, 16.0 ); >;

	// The pattern: which (0 none, 1 stripes, 2 camo, 3 checker, 4 gradient, 5 carbon, 6 shapes), its colours, how big
	// one repeat is in units, how far turned (radians), and how fast it scrolls (repeats a second) and spins (turns a second)
	float g_flPattern < Default( 0.0 ); Range( 0.0, 6.0 ); >;
	float3 g_vPatternA < Default3( 0.0, 0.0, 0.0 ); >;
	float3 g_vPatternB < Default3( 1.0, 1.0, 1.0 ); >;
	float g_flPatternSize < Default( 4.0 ); Range( 0.25, 64.0 ); >;
	float g_flPatternTurn < Default( 0.0 ); Range( 0.0, 3.2 ); >;
	float g_flPatternScroll < Default( 0.0 ); Range( -4.0, 4.0 ); >;
	float g_flPatternSpin < Default( 0.0 ); Range( -2.0, 2.0 ); >;
	Texture2D g_tPatternShape < Attribute( "PatternShape" ); SrgbRead( false ); >;
	SamplerState g_sPatternShape < Filter( BILINEAR ); AddressU( WRAP ); AddressV( WRAP ); >;

	// The moving colour: which (0 none, 1 rainbow, 2 rain, 3 cycl, 4 cyln, 5 pulse, 6 glow, 7 shimmer, 8 palette), its
	// colours (up to eight), how fast (1 the clan tags' pace) and in how many bands along the weapon
	float g_flMotion < Default( 0.0 ); Range( 0.0, 8.0 ); >;
	float g_flPaletteCount < Default( 1.0 ); Range( 1.0, 8.0 ); >;
	float3 g_vPalette0 < Default3( 1.0, 0.2, 0.2 ); >;
	float3 g_vPalette1 < Default3( 0.0, 1.0, 0.0 ); >;
	float3 g_vPalette2 < Default3( 1.0, 1.0, 0.0 ); >;
	float3 g_vPalette3 < Default3( 0.0, 0.0, 1.0 ); >;
	float3 g_vPalette4 < Default3( 0.0, 1.0, 1.0 ); >;
	float3 g_vPalette5 < Default3( 1.0, 1.0, 1.0 ); >;
	float3 g_vPalette6 < Default3( 1.0, 1.0, 1.0 ); >;
	float3 g_vPalette7 < Default3( 1.0, 1.0, 1.0 ); >;
	float g_flSpeed < Default( 1.0 ); Range( 0.0, 10.0 ); >;
	float g_flBands < Default( 8.0 ); Range( 2.0, 32.0 ); >;

	float Mod( float a, float b ) { return a - b * floor( a / b ); }

	float3 PaletteAt( float index )
	{
		int i = (int)Mod( index, g_flPaletteCount );
		if ( i == 0 ) return g_vPalette0;
		if ( i == 1 ) return g_vPalette1;
		if ( i == 2 ) return g_vPalette2;
		if ( i == 3 ) return g_vPalette3;
		if ( i == 4 ) return g_vPalette4;
		if ( i == 5 ) return g_vPalette5;
		if ( i == 6 ) return g_vPalette6;
		return g_vPalette7;
	}

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
	float PatternAt( float3 position, float3 normal, float along )
	{
		float pattern = g_flPattern;
		float time = g_flTime;
		if ( pattern > 3.5 && pattern < 4.5 )
			return saturate( frac( along - time * g_flPatternScroll * 0.25 ) );

		float3 p = ( position - g_vCenter ) / g_flPatternSize;
		if ( pattern > 1.5 && pattern < 2.5 )
		{
			float3 drift = g_vAxis * time * g_flPatternScroll;
			float n = Noise( p * 1.3 + drift ) * 0.65 + Noise( p * 2.9 + drift + 7.0 ) * 0.35;
			return smoothstep( 0.47, 0.53, n );
		}

		float angle = g_flPatternTurn + time * g_flPatternSpin * 6.28318;
		float2x2 turn = float2x2( cos( angle ), -sin( angle ), sin( angle ), cos( angle ) );
		float scroll = time * g_flPatternScroll;
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

	// The moving colour of band band of bands, ms milliseconds in, by World at War's clan tag rules (the selector's
	// ColorCycle, element by element): rain scrolls the palette, cycl wipes one colour across, cyln sweeps a highlight
	float3 BandColour( float motion, float band, float bands, float ms, float along )
	{
		float count = g_flPaletteCount;
		if ( motion < 1.5 )
			return Hue( frac( along - ms * 0.00025 ) );

		if ( motion < 2.5 )
			return PaletteAt( Mod( floor( ms / 100.0 ), count ) + band );

		if ( motion < 3.5 )
		{
			float base = Mod( floor( ms / ( bands * 100.0 ) ), count );
			float position = Mod( floor( ms / 100.0 ), bands );
			return PaletteAt( band < position ? base : base + count - 1.0 );
		}

		if ( motion < 4.5 )
		{
			float position = floor( ( cos( ms * 0.004 ) + 1.0 ) * 0.5 * bands );
			return band == position ? g_vPalette0 : g_vPalette1;
		}

		// A palette going round, each colour fading into the next once a second
		float step = ms / 1000.0;
		return lerp( PaletteAt( floor( step ) ), PaletteAt( floor( step ) + 1.0 ), smoothstep( 0.0, 1.0, frac( step ) ) );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );
		float2 uv = i.vTextureCoords.xy;
		float4 colour = g_tColor.Sample( TextureFiltering, uv );
		if ( g_flAlphaTest > 0.5 )
			clip( colour.a - 0.5 );

		float4 normalTs = g_tNormal.Sample( TextureFiltering, uv );
		m.Normal = TransformNormal( DecodeNormal( normalTs.xyz ), i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		m.Metalness = g_flSkinMetalness;
		m.Roughness = g_flSkinRoughness;
		m.Opacity = 1.0;

		float3 albedo = colour.rgb * g_vSkinTint;
		float shade = dot( colour.rgb, float3( 0.3, 0.59, 0.11 ) );
		float along = saturate( ( dot( i.vPositionOs, g_vAxis ) - g_vAxisRange.x ) / max( g_vAxisRange.y - g_vAxisRange.x, 0.001 ) );

		// A pattern takes the place of the weapon's own colour, keeping a little of its light and dark
		if ( g_flPattern > 0.5 )
		{
			float value = PatternAt( i.vPositionOs, normalize( i.vNormalOs ), along );
			float3 painted = lerp( g_vPatternA, g_vPatternB, value ) * g_vSkinTint;
			albedo = painted * lerp( 1.0, saturate( shade * 2.2 ), 0.35 );
		}

		float motion = g_flMotion;
		float ms = g_flTime * 1000.0 * g_flSpeed;
		float3 emission = 0.0;
		if ( motion > 0.5 && ( motion < 4.5 || motion > 7.5 ) )
		{
			float bands = floor( g_flBands );
			float band = min( floor( along * bands ), bands - 1.0 );
			float3 moving = BandColour( motion, band, bands, ms, along );
			float light = max( dot( albedo, float3( 0.3, 0.59, 0.11 ) ), 0.35 );
			albedo = lerp( albedo, moving * saturate( light * 1.8 ), 0.85 );
			emission = moving * 0.12;
		}
		else if ( motion > 4.5 && motion < 5.5 )
		{
			// Pulse: brighter and dimmer in its colour
			float beat = 0.5 - 0.5 * cos( g_flTime * g_flSpeed * 6.28318 );
			albedo = lerp( albedo, albedo * g_vPalette0 * 1.4, beat );
		}
		else if ( motion > 5.5 && motion < 6.5 )
		{
			// Glow: lit from within, breathing
			float beat = 0.6 + 0.4 * sin( g_flTime * g_flSpeed * 6.28318 );
			emission = g_vPalette0 * beat * 1.5;
		}
		else if ( motion > 6.5 && motion < 7.5 )
		{
			// Shimmer: a sheen sweeping along it, once every two seconds at speed 1
			float sweep = frac( g_flTime * g_flSpeed * 0.5 ) * 1.6 - 0.3;
			float offset = ( along - sweep ) * 16.0;
			float sheen = exp( -offset * offset );
			emission = g_vPalette0 * sheen * 1.2;
			m.Roughness = lerp( m.Roughness, 0.1, sheen );
		}

		m.Albedo = albedo;
		m.Emission = emission;
		return ShadingModelStandard::Shade( i, m );
	}
}
