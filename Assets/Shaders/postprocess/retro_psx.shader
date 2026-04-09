HEADER
{
    DevShader = true;
    Description = "PSX-style retro post-process: pixelization, PS1 dithering, scanlines, jitter";
}

MODES
{
    Default();
    Forward();
}

COMMON
{
    #include "postprocess/shared.hlsl"
}

struct VertexInput
{
    float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
    float2 vTexCoord : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
};

struct PixelInput
{
    float2 vTexCoord : TEXCOORD0;

    #if ( PROGRAM == VFX_PROGRAM_VS )
        float4 vPositionPs : SV_Position;
    #endif

    #if ( ( PROGRAM == VFX_PROGRAM_PS ) )
        float4 vPositionSs : SV_Position;
    #endif
};

VS
{
    PixelInput MainVs( VertexInput i )
    {
        PixelInput o;
        o.vPositionPs = float4(i.vPositionOs.xyz, 1.0f);
        o.vTexCoord = i.vTexCoord;
        return o;
    }
}

PS
{
    #include "postprocess/common.hlsl"
    #include "postprocess/functions.hlsl"
    #include "procedural.hlsl"

    Texture2D g_tColorBuffer < Attribute( "ColorBuffer" ); SrgbRead( true ); >;

    float2 g_vInternalResolution < Attribute( "internal_resolution" ); >;
    float g_flColorDepth < Attribute( "color_depth" ); >;
    float g_flDitherEnabled < Attribute( "dither_enabled" ); >;
    float g_flDitherSpread < Attribute( "dither_spread" ); >;
    float g_flStrength < Attribute( "strength" ); >;
    float g_flScanlineStrength < Attribute( "scanline_strength" ); >;
    float g_flScanlineWidth < Attribute( "scanline_width" ); >;
    float g_flJitterEnabled < Attribute( "jitter_enabled" ); >;
    float g_flJitterAmount < Attribute( "jitter_amount" ); >;

    // Authentic PS1 dither matrix (from PSX hardware documentation)
    // Returns -4 to +3 integer noise value
    float PS1DitherNoise( float2 vScreenPos )
    {
        // PS1 dither matrix per psx-spx.consoledev.net
        int ix = int( fmod( abs( vScreenPos.x ), 4.0 ) );
        int iy = int( fmod( abs( vScreenPos.y ), 4.0 ) );

        // Compute index and look up value using math instead of array
        // Matrix: -4  0 -3  1 / 2 -2  3 -1 / -3  1 -4  0 / 3 -1  2 -2
        int row = iy;
        int col = ix;

        // Base checkerboard pattern
        int sign = ( ( col + row ) % 2 == 0 ) ? -1 : 1;
        int base = ( col / 2 + row / 2 ) % 2;
        int mag = ( col % 2 == 0 ) ? ( 3 + base ) : base;

        return float( sign * mag ) * 1.0;
    }

    float4 MainPs( PixelInput i ) : SV_Target0
    {
        float2 vScreenUv = CalculateViewportUv( i.vPositionSs.xy );
        float2 vInternalRes = g_vInternalResolution;

        // Original color for strength blending
        float3 vOriginal = g_tColorBuffer.SampleLevel( g_sBilinearMirror, vScreenUv, 0 ).rgb;

        // --- Pixel grid jitter ---
        float2 vJitterOffset = float2( 0.0, 0.0 );
        if ( g_flJitterEnabled > 0.5 )
        {
            // Camera position directly drives pixel grid offset
            // Moving the camera shifts which pixels land on the grid
            vJitterOffset = frac( g_vCameraPositionWs.xy * g_flJitterAmount ) / vInternalRes;
        }

        // --- Pixelization ---
        float2 vPixelUv = floor( ( vScreenUv + vJitterOffset ) * vInternalRes ) / vInternalRes - vJitterOffset;
        float3 vColor = g_tColorBuffer.SampleLevel( g_sBilinearMirror, vPixelUv, 0 ).rgb;

        // --- PS1 15-bit color quantization with authentic dithering ---
        // (from Acerola / psx-spx.consoledev.net documentation)
        float3 vResult = vColor;

        float flBits = pow( 2.0, g_flColorDepth ); // e.g. 5 bits = 32 levels

        if ( g_flDitherEnabled > 0.5 )
        {
            // Get dither noise from PS1 dither matrix at internal resolution pixel position
            float2 vDitherPos = vPixelUv * vInternalRes;
            float flNoise = PS1DitherNoise( vDitherPos ) * g_flDitherSpread;

            // Quantize: convert to 0-255, add dither noise, quantize to N-bit
            vResult = round( vColor * 255.0 + flNoise );
            vResult = clamp( vResult, float3( 0, 0, 0 ), float3( 255, 255, 255 ) );
            vResult = clamp( vResult / ( 256.0 / flBits ), float3( 0, 0, 0 ), float3( flBits - 1.0, flBits - 1.0, flBits - 1.0 ) );
            vResult /= ( flBits - 1.0 );
        }
        else
        {
            // Quantize without dithering
            vResult = floor( vColor * flBits ) / flBits;
        }

        // --- Scanlines ---
        if ( g_flScanlineStrength > 0.001 )
        {
            float2 vPixelPos = vScreenUv * vInternalRes;
            float flLine = frac( vPixelPos.y * 0.5 );
            float flScanline = lerp( 1.0, smoothstep( 0.0, g_flScanlineWidth, abs( flLine - 0.5 ) ), g_flScanlineStrength );
            vResult *= flScanline;
        }

        // --- Strength blend ---
        vResult = lerp( vOriginal, vResult, g_flStrength );

        return float4( vResult, 1.0 );
    }
}
