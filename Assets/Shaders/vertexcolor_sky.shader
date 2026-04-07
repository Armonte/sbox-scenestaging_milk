HEADER
{
	Description = "Unlit vertex color skybox shader";
}

FEATURES
{
	#include "vr_common_features.fxc"
}

MODES
{
	Forward();
	Depth( S_MODE_DEPTH );
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VertexInput
{
	float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
	float4 vColor : COLOR0 < Semantic( None ); >;
	float3 vNormalOs : NORMAL < Semantic( None ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	PixelInput MainVs( const VertexInput v )
	{
		PixelInput i;
		i.vPositionWs = v.vPositionOs;
		i.vPositionPs = Position3WsToPs( v.vPositionOs );
		i.vNormalWs = v.vNormalOs;
		i.vVertexColor = v.vColor;
		i.vTextureCoords = float4( 0, 0, 0, 0 );
		return i;
	}
}

PS
{
	RenderState( CullMode, BACK );

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		return i.vVertexColor;
	}
}
