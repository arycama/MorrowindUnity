#include "Common.hlsl"

struct VertexInput
{
	uint instanceID : SV_InstanceID;
	float3 position : POSITION;
	float2 uv : TEXCOORD;
};

struct FragmentInput
{
	float4 position : SV_Position;
	float3 worldPosition : POSITION1;
	float2 uv : TEXCOORD;
};

Texture2D _MainTex;

cbuffer UnityPerMaterial
{
	float4 _Color;
};

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.worldPosition = ObjectToWorld(input.position, input.instanceID);
	output.position = WorldToClip(output.worldPosition);
	output.uv = input.uv;
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	float4 volumetricLighting = SampleVolumetricLighting(input.worldPosition);
	float fogFactor = 1.0 - volumetricLighting.a;
	float3 fogColor = volumetricLighting.rgb / fogFactor;
	float4 color = _MainTex.Sample(_LinearRepeatSampler, input.uv) * _Color;
	
	//color.rgb *= color.a;
	//color.rgb *= 1.0 - fogFactor;
	//color.rgb += fogColor * fogFactor;
	//return float4(color.rgb, 0.0);
	
	#if 1
	// Remove fog
	float3 offset = -fogFactor * fogColor / (1.0 - fogFactor);
	float scale = 1.0 / (1.0 - fogFactor);
	offset += color.rgb * color.a * (1.0 - fogFactor);
	
	// Re-add fog
	offset += fogColor * fogFactor / (1.0 - fogFactor);
	scale *= (1.0 - fogFactor);
	scale *= 1.0 - color.a;
	#else
		// Remove fog
		float3 offset = color.rgb * color.a * (1.0 - fogFactor);
	
		// Re-add fog
		float scale = 1.0;
	#endif

	return float4(offset, scale);
}