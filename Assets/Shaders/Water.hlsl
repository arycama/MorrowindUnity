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

Texture2D<float3>  _MainTex;
SamplerState sampler_MainTex;

cbuffer UnityPerMaterial
{
	float _Alpha, _Tiling;
};

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.worldPosition = ObjectToWorld(input.position, input.instanceId);
	output.position = WorldToClip(output.worldPosition);
	output.uv = output.worldPosition.xz * _Tiling;
	output.normal = ObjectToWorldNormal(input.normal, input.instanceId);
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	float4 color = float4(_MainTex.Sample(sampler_MainTex, input.uv), _Alpha);
	
	float3 normal = normalize(input.normal);
	float3 lighting = saturate(dot(normal, _SunDirection)) * _SunColor;
	
	float3 shadowPosition = MultiplyPoint3x4((float3x4) _WorldToShadow, input.worldPosition);
	if (all(saturate(shadowPosition.xy) == shadowPosition.xy))
		lighting *= _DirectionalShadows.SampleCmpLevelZero(sampler_DirectionalShadows, shadowPosition.xy, shadowPosition.z);
	
	lighting += _AmbientLight;
	color.rgb *= lighting;
	
	float fogFactor = saturate(input.position.w * _FogScale + _FogOffset);
	color.rgb = lerp(color.rgb, _FogColor, fogFactor);
	
	return color;
}