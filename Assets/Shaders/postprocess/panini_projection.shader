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
        o.pos = float4( i.pos.xy, 0.0f, 1.0f );
        o.uv = i.uv;
        return o;
    }
}

PS
{
    #include "postprocess/common.hlsl"

    float4 MainPs( PixelInput i ) : SV_Target0
    {
        // ABSOLUTE MINIMUM: just output solid red
        return float4( 1, 0, 0, 1 );
    }
}
