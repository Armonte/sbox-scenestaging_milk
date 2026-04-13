//
// Portal surface shader. Displays a render target captured from a secondary
// camera positioned at the linked portal. Adapted from mirror_8.shader —
// uses screen-space UVs to sample the portal texture, with a void fallback
// when no linked portal is active.
//

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	VrForward();
	Depth();
}

COMMON
{
	#define S_SPECULAR 1
	#define F_DYNAMIC_REFLECTIONS 0
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	// Disable backface culling so the portal quad stays visible when the player's
	// camera briefly crosses to the far side during pass-through (prevents 1-frame
	// flash of the scene geometry behind the quad). Screen-space UV sampling
	// means the view looks correct from either side.
	RenderState( CullMode, NONE );

	#include "common/pixel.hlsl"

	bool g_bReflection < Default(0.0f); Attribute( "HasReflectionTexture" ); >;
	CreateTexture2D( g_tReflectionTexture ) < Attribute( "ReflectionTexture" ); SrgbRead( false ); Filter( MIN_MAG_MIP_LINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	// Fallback color when no linked portal is rendering (dark void)
	float4 g_vFallbackColor < UiType(Color); Default4( 0.02, 0.01, 0.05, 1.0 ); UiGroup("Portal"); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		// No portal texture — show dark void
		if ( !g_bReflection )
		{
			return g_vFallbackColor;
		}

		// Screen-space UV: the portion of the screen this portal pixel occupies
		// maps 1:1 to the portion of the render target that shows what's visible
		// through the portal. This works because the portal camera is set up with
		// the main camera's transformed position + same FOV — so its render target
		// contains exactly what the player would see through the opening.
		float2 uv = i.vPositionSs.xy * g_vInvViewportSize;

		float3 portalColor = g_tReflectionTexture.SampleLevel( g_tReflectionTexture_sampler, uv, 0 ).rgb;

		// Apply scene fog so distant portals fade naturally
		float3 worldPos = g_vCameraPositionWs + i.vPositionWithOffsetWs;
		portalColor = Fog::Apply( worldPos, i.vPositionSs.xy, portalColor );

		return float4( portalColor, 1.0 );
	}
}
