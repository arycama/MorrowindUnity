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
	float Alpha, Scale;
	float3 Extinction, Albedo;
};

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.worldPosition = ObjectToWorld(input.position, input.instanceId);
	output.position = WorldToClipPosition(output.worldPosition);
	output.uv = output.worldPosition.xz * Scale / 64.0 / 3;
	output.normal = float3(0.0, 1.0, 0.0);//	ObjectToWorldNormal(input.normal, input.instanceId);
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	float4 color = float4(_MainTex.Sample(sampler_MainTex, input.uv), Alpha);
	float3 normal =	normalize(input.normal);
	float viewDistance = distance(ViewPosition, input.worldPosition);
	return GetLuminanceAndFog(color, AmbientLight, normal, input.position.xy, input.position.w, viewDistance, false, input.worldPosition);
}