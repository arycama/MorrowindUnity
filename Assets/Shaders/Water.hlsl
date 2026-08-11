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
	float3 viewPosition : POSITION1;
	float2 uv : TEXCOORD;
	float3 normal : NORMAL;
};

Texture2D<float> CameraDepth;
Texture2D<float3> _MainTex, CameraColor, FadeTexture;
SamplerState sampler_MainTex;

cbuffer UnityPerMaterial
{
	float Alpha, Scale, Fade;
	float3 Extinction, Albedo;
};

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.viewPosition = WorldToViewPosition(input.position - ViewPosition);
	output.position = ViewToClipPosition(output.viewPosition);
	output.uv = input.position.xz * Scale / 64.0 / 3;
	output.normal = WorldToViewNormal(float3(0.0, 1.0, 0.0));
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	float3 current = _MainTex.Sample(sampler_MainTex, input.uv).rgb;
	float3 next = FadeTexture.Sample(sampler_MainTex, input.uv).rgb;
	float4 color = float4(lerp(current, next, frac(Fade)), Alpha);
	float3 normal =	normalize(input.normal);
	return GetLuminanceAndFog(color, AmbientLight, normal, input.position.xy, input.viewPosition);
}