#include "../Common.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/CommonShaders.hlsl"

Texture2D<float3> CameraTarget;

float3 Fragment(VertexFullscreenTriangleMinimalOutput input) : SV_Target
{
	#ifdef FLIP
		float2 position = input.position.xy;
		position.y = ViewSize.y - position.y;
		return CameraTarget[position];
	#else
		return CameraTarget[input.position.xy];
	#endif
}
