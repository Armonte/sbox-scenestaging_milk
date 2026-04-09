COMMON
{
    #include "postprocess/shared.hlsl"
}

struct VertexInput
{
    float3 pos : POSITION < Semantic( PosXyz ); >;
    float2 uv : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
};

struct PixelInput
{
    float2 uv : TEXCOORD0;
	float4 pos : SV_Position;
};

VS
{
    PixelInput MainVs( VertexInput i )
    {
        PixelInput o;

        o.pos = float4(i.pos.xy, 0.0f, 1.0f);
        o.uv = i.uv;
        return o;
    }
}

PS
{
    #include "postprocess/common.hlsl"
    #include "postprocess/functions.hlsl"
    #include "procedural.hlsl"

    Texture2D colorBuffer < Attribute( "ColorBuffer" ); SrgbRead( true ); >;
    float brightness< Attribute("brightness"); >;

    float4 MainPs( PixelInput i ) : SV_Target0
    {
        float2 uv = CalculateViewportUv( i.uv.xy );
        float4 color = colorBuffer.SampleLevel( g_sBilinearMirror, uv, 0 );

        color.rgb *= brightness;

        return color;
    }
}
