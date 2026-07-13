#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/CommonShaders.hlsl"

Texture2D<float3> CameraTarget;

float3 Fragment(VertexFullscreenTriangleMinimalOutput input) : SV_Target
{
	return CameraTarget[input.position.xy];
}
