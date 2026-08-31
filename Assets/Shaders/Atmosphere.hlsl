#ifdef __INTELLISENSE__
	#define VOLUMETRIC_LIGHT_ON
#endif

#include "Common.hlsl"

struct VertexInput
{
	uint vertexId : SV_VertexID;
	float3 position : POSITION;
	float2 uv : TEXCOORD;
};

struct FragmentInput
{
	float4 position : SV_POSITION;
	float fogFactor : TEXCOORD;
};

float4 _SkyColor;

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.position = ObjectToClipPosition(input.position * 6, 0);
	output.position.z /= output.position.w;
	
	output.fogFactor = (input.vertexId & 1) ? 0.0 : 1.0;
	return output;
}

float3 Fragment(FragmentInput input) : SV_Target
{
	#ifdef VOLUMETRIC_LIGHT_ON
		float3 volumetricUv = float3(input.position.xy / ViewSize, 1.0);
		float4 volumetricLight = VolumetricLight.Sample(LinearClampSampler, volumetricUv);
		float3 fogLuminance = volumetricLight.rgb;
		float fogTransmittance = volumetricLight.a;
		
		return lerp(fogLuminance * (1.0 - fogTransmittance), _SkyColor.rgb, input.fogFactor);
		
	#else
		return lerp(FogColor, _SkyColor.rgb, input.fogFactor);
	#endif
}