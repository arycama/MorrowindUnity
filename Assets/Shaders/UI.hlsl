#include "Common.hlsl"

struct VertexInput
{
	float3 position : POSITION;
	float4 color : COLOR;
	float2 uv : TEXCOORD;
};

struct FragmentInput
{
	float4 position : SV_Position;
	float2 uv : TEXCOORD;
	float4 color : COLOR;
};

Texture2D _MainTex;
SamplerState sampler_MainTex;

cbuffer UnityPerMaterial
{
	float4 _Color, _MainTex_ST;
};

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.position = MultiplyPointProj(UiOverlayMatrix, input.position);
	output.uv = input.uv;
	output.color = float4((input.color.rgb), input.color.a) * _Color;
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	#ifdef TEXT_ON
		float4 color = float2(1.0, _MainTex.Sample(sampler_MainTex, input.uv).a).rrrg * input.color;
	#else
		float4 color = _MainTex.Sample(sampler_MainTex, input.uv) * input.color;
	#endif
	
	//clip (color.a - 0.01);
	return color;
}