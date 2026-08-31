#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/GBuffer.hlsl"
#include "Assets/Shaders/Common.hlsl"

struct FragmentInput
{
	float4 position : SV_Position;
};

FragmentInput Vertex(uint id : SV_VertexID)
{
	FragmentInput output;
	float2 uv = (id << uint2(0, 1)) & 2;
	output.position = float4(uv * 2.0 - 1.0, 0.0, 1.0);
	return output;
}

float3 Fragment(FragmentInput input) : SV_Target
{
	#ifdef VOLUMETRIC_LIGHT_ON
		float3 volumetricUv = float3(input.position.xy / ViewSize, 1.0);
		return VolumetricLight.Sample(LinearClampSampler, volumetricUv).rgb;
	#else
		return FogColor;
	#endif
}
