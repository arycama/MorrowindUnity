#include "Common.hlsl"

struct VertexInput
{
	uint vertexId : SV_VertexID;
	float3 position : POSITION;
	float2 uv : TEXCOORD;
	float4 color : COLOR;
};

struct FragmentInput
{
	float4 position : SV_POSITION;
	float3 worldPosition : POSITION1;
	float2 uv : TEXCOORD0;
	float4 color : COLOR;
};

Texture2D _MainTex, _FadeTexture;
float4 _SkyColor;
SamplerState sampler_MainTex, sampler_FadeTexture;

cbuffer UnityPerMaterial
{
	float4 _MainTex_ST;
	float _CloudSpeed, _TimeOfDay, _LerpFactor, _Alpha;
};

FragmentInput Vertex(VertexInput input)
{
	float3 worldPosition = ObjectToWorld(input.position * 6, 0);

	FragmentInput output;
	output.worldPosition = worldPosition;
	output.position = WorldToClipPosition(worldPosition);
	output.uv = input.uv;// * _MainTex_ST.xy + _MainTex_ST.zw; //	+_CloudSpeed * Time * 0.003;
	output.position.z /= output.position.w;
	
	float alpha = (input.vertexId & 1) ? 0.0 : 1.0;
	output.color = float4(_SkyColor.rgb, alpha);
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	return input.color;
}