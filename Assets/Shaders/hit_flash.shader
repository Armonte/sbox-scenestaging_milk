HEADER
{
	Description = "Unlit white hit-flash. Apply via MaterialAccessor.SetOverride for a damage pop.";
}

MODES
{
	VrForward();
	Depth();
}

FEATURES
{
	#include "common/features.hlsl"
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
	#include "common/pixel.hlsl"

	float g_flFlashIntensity < Attribute( "FlashIntensity" ); Default( 1.0 ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		return float4( g_flFlashIntensity, g_flFlashIntensity, g_flFlashIntensity, 1.0 );
	}
}
