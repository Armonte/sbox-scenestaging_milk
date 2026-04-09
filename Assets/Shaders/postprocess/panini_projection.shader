HEADER
{
    DevShader = true;
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
    float2 uv : TEXCOORD0;

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
        o.vPositionPs = float4(i.vPositionOs.xy, 0.0f, 1.0f);
        o.uv = i.vTexCoord;
        return o;
    }
}

PS
{
    #include "postprocess/common.hlsl"
    #include "postprocess/functions.hlsl"
    #include "procedural.hlsl"

    Texture2D g_tColorBuffer < Attribute( "ColorBuffer" ); SrgbRead( true ); >;

    float g_flProjectionMode < Attribute( "projection_mode" ); >;
    float g_flStrength < Attribute( "strength" ); >;
    float g_flPaniniS < Attribute( "panini_s" ); >;
    float g_flScreenZoom < Attribute( "screen_zoom" ); >;
    float g_flMaxRectX < Attribute( "max_rect_x" ); >;
    float g_flMaxRectY < Attribute( "max_rect_y" ); >;
    float g_flAspect < Attribute( "aspect" ); >;

    static const float EPS = 1e-8;

    float3 RayRectilinear( float2 vProjectedXY )
    {
        return normalize( float3( vProjectedXY, 1.0 ) );
    }

    float3 PaniniInverseS( float2 vProjectedXY, float d, float s )
    {
        float dp1 = d + 1.0;
        float k = ( vProjectedXY.x * vProjectedXY.x ) / ( dp1 * dp1 );

        float disc = k * k * d * d - ( k + 1.0 ) * ( k * d * d - 1.0 );
        if ( disc <= 0.0 ) return RayRectilinear( vProjectedXY );

        float c_lon = ( -k * d + sqrt( disc ) ) / ( k + 1.0 );
        float S = dp1 / ( d + c_lon );

        float2 ang = float2( atan2( vProjectedXY.y, S ), atan2( vProjectedXY.x, S * c_lon ) );

        float cos_lat = cos( ang.x );
        float3 ray = float3( cos_lat * sin( ang.y ), sin( ang.x ), cos_lat * cos( ang.y ) );

        if ( ray.z > 0.0 )
        {
            float q = ray.x / ray.z;
            q = ( q * q ) / ( dp1 * dp1 );
            ray.y *= lerp( 1.0, 1.0 / sqrt( 1.0 + q ), s );
        }
        return ray;
    }

    float3 RayFromLatLon( float2 vLatLon )
    {
        float cos_lat = cos( vLatLon.x );
        return float3( cos_lat * sin( vLatLon.y ), sin( vLatLon.x ), cos_lat * cos( vLatLon.y ) );
    }

    float3 RayEquirectangular( float2 vScreenXY, float2 vMaxRectXY )
    {
        float2 vMaxLatLon = float2( atan( vMaxRectXY.y ), atan( vMaxRectXY.x ) );
        return RayFromLatLon( float2( vScreenXY.y * vMaxLatLon.x, vScreenXY.x * vMaxLatLon.y ) );
    }

    float3 RayFisheye( float2 vScreenXY, float2 vMaxRectXY, float flAspect, bool bStereographic )
    {
        float2 p = float2( vScreenXY.x * flAspect, vScreenXY.y );
        float r = length( p );
        if ( r < EPS ) return float3( 0.0, 0.0, 1.0 );

        float theta_max = min( atan( vMaxRectXY.x ), atan( vMaxRectXY.y ) );
        float theta = bStereographic
            ? 2.0 * atan( r * tan( 0.5 * theta_max ) )
            : 2.0 * asin( clamp( r * sin( 0.5 * theta_max ), 0.0, 1.0 ) );

        return normalize( float3( ( p / r ) * sin( theta ), cos( theta ) ) );
    }

    float3 UvBlendRay( float3 vRect, float3 vProj, float b )
    {
        float2 uv_r = vRect.xy / max( vRect.z, EPS );
        float2 uv_p = vProj.xy / max( vProj.z, EPS );
        return float3( lerp( uv_r, uv_p, b ), 1.0 );
    }

    float3 FinalViewRay( float2 vScreenXY, float2 vMaxRectXY, float flAspect )
    {
        float2 vProjectedXY = vScreenXY * vMaxRectXY;
        float3 vRect = RayRectilinear( vProjectedXY );

        int mode = (int)round( g_flProjectionMode );
        float3 vProj;

        if ( mode == 1 ) vProj = PaniniInverseS( vProjectedXY, g_flStrength, g_flPaniniS );
        else if ( mode == 2 ) vProj = RayEquirectangular( vScreenXY, vMaxRectXY );
        else if ( mode == 3 ) vProj = RayFisheye( vScreenXY, vMaxRectXY, flAspect, true );
        else if ( mode == 4 ) vProj = RayFisheye( vScreenXY, vMaxRectXY, flAspect, false );
        else return vRect;

        if ( mode == 2 ) return UvBlendRay( vRect, vProj, g_flStrength );
        if ( mode == 1 ) return vProj;
        return lerp( vRect, vProj, g_flStrength );
    }

    float4 MainPs( PixelInput i ) : SV_Target0
    {
        float2 vScreenUv = CalculateViewportUv( i.vPositionSs.xy );

        // Convert UV [0,1] to centered coords [-1,1], apply fill zoom
        float2 vScreenXY = ( vScreenUv * 2.0 - 1.0 ) / max( g_flScreenZoom, EPS );

        float2 vMaxRectXY = float2( g_flMaxRectX, g_flMaxRectY );

        float3 ray = FinalViewRay( vScreenXY, vMaxRectXY, g_flAspect );

        if ( ray.z <= 0.0 )
            return float4( 0.0, 0.0, 0.0, 1.0 );

        // Project ray back to source UV
        float2 vSourceUv = ( ray.xy / ray.z ) / vMaxRectXY * 0.5 + 0.5;

        // Black for out-of-bounds
        if ( vSourceUv.x < 0.0 || vSourceUv.x > 1.0 || vSourceUv.y < 0.0 || vSourceUv.y > 1.0 )
            return float4( 0.0, 0.0, 0.0, 1.0 );

        return g_tColorBuffer.SampleLevel( g_sBilinearMirror, vSourceUv, 0 );
    }
}
