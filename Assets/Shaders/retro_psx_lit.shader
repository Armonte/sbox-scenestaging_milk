HEADER
{
	Description = "Standard lit shader with PS1 vertex snapping";
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

	float g_flSnapResolution < Attribute( "SnapResolution" ); Default( 160.0 ); >;

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );

		// PS1 vertex snapping: snap clip XY to integer pixel grid
		float snap = g_flSnapResolution;
		if ( snap > 0.0 )
		{
			o.vPositionPs.xy = round( o.vPositionPs.xy / o.vPositionPs.w * snap ) / snap * o.vPositionPs.w;
		}

		return FinalizeVertex( o );
	}
}

PS
{
    #include "common/pixel.hlsl"

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::From( i );
		return ShadingModelStandard::Shade( i, m );
	}
}
