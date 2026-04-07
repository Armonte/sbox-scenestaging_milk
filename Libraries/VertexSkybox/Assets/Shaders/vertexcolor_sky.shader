HEADER
{
	DevShader = true;
	CompileTargets = ( IS_SM_50 && ( PC || VULKAN ) );
	Description = "Unlit vertex color skybox";
}

MODES
{
	Default();
	Forward();
}

FEATURES
{
}

COMMON
{
	#include "system.fxc"
	#include "common.fxc"
	#include "math_general.fxc"
	#include "vr_common.fxc"
}

struct VS_INPUT
{
	float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
	float4 vColor : COLOR0 < Semantic( Color ); >;
	float3 vNormalOs : NORMAL < Semantic( OptionallyCompressedTangentFrame ); >;
	uint nInstanceTransformID : TEXCOORD13 < Semantic( InstanceTransformUv ); >;
};

struct PS_INPUT
{
	float4 vColor : COLOR0;

	#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 vPositionPs : SV_Position;
	#endif

	#if ( PROGRAM == VFX_PROGRAM_PS )
		float4 vPositionSs : SV_Position;
	#endif
};

VS
{
	#include "instancing.fxc"

	PS_INPUT MainVs( VS_INPUT i )
	{
		PS_INPUT o;

		float3x4 matObjectToWorld = GetTransformMatrix( i.nInstanceTransformID );
		float3 vPositionWs = mul( matObjectToWorld, float4( i.vPositionOs.xyz, 1.0 ) );
		o.vPositionPs = Position3WsToPs( vPositionWs );
		o.vColor = i.vColor;

		return o;
	}
}

PS
{
	RenderState( CullMode, NONE );
	RenderState( DepthWriteEnable, false );

	struct PS_OUTPUT
	{
		float4 vColor : SV_Target0;
	};

	PS_OUTPUT MainPs( PS_INPUT i )
	{
		PS_OUTPUT o;
		o.vColor = i.vColor;
		return o;
	}
}
