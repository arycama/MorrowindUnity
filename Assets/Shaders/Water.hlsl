#include "Common.hlsl"

struct VertexInput
{
	uint instanceId : SV_InstanceID;
	float3 position : POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD;
};

struct FragmentInput
{
	float4 position : SV_Position;
	float3 worldPosition : POSITION1;
	float2 uv : TEXCOORD;
	float3 normal : NORMAL;
};

Texture2D<float> CameraDepth;
Texture2D<float3> _MainTex, CameraColor;
SamplerState sampler_MainTex;

cbuffer UnityPerMaterial
{
	float _Alpha, _Tiling;
	float3 Extinction, Albedo;
};

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.worldPosition = ObjectToWorld(input.position, input.instanceId);
	output.position = WorldToClipPosition(output.worldPosition);
	output.uv = output.worldPosition.xz * _Tiling;
	output.normal = ObjectToWorldNormal(input.normal, input.instanceId);
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	float4 color = float4(_MainTex.Sample(sampler_MainTex, input.uv), _Alpha);
		
	float backgroundDepth = rcp(CameraDepth[input.position.xy] * LinearDepthScale + LinearDepthOffset);
	float depthDistance = backgroundDepth - input.position.w;
	float3 backgroundColor = CameraColor[input.position.xy];
	float3 transmittance = exp(-depthDistance * Extinction);
	//color.rgb = lerp(color.rgb, 0.0, transmittance);
	
	float3 normal = normalize(input.normal);
	float3 lighting = saturate(dot(normal, SunDirection)) * SunColor;
	
	float3 shadowPosition = MultiplyPoint3x4((float3x4) WorldToSunShadow, input.worldPosition);
	if (all(saturate(shadowPosition.xy) == shadowPosition.xy))
		lighting *= SunShadow.SampleCmpLevelZero(LinearClampCompareSampler, shadowPosition.xy, shadowPosition.z);
	
	lighting += AmbientLight;
	color.rgb *= lighting;
	
	// Need to remove fog from background
	//if (_FogEnabled)
	{
		float fogFactor = saturate(backgroundDepth * FogScale + FogOffset);
		float3 backgroundFog = lerp(0.0, FogColor, fogFactor);
		//backgroundColor = max(0.0, backgroundColor - backgroundFog.rgb);
	}
	
	//color.rgb += backgroundColor * transmittance;
	
	return ApplyFog(color, input.position.w);
}