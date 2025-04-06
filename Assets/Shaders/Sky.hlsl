#include "Common.hlsl"

struct VertexInput
{
	float3 position : POSITION;
	float2 uv : TEXCOORD;
};

struct FragmentInput
{
	float4 position : SV_POSITION;
	float2 uv : TEXCOORD0;
	float4 color : TEXCOORD2;
};

sampler2D _MainTex, _FadeTexture;
float4 _SkyColor;

cbuffer UnityPerMaterial
{
	float4 _MainTex_ST;
	float _CloudSpeed, _TimeOfDay, _LerpFactor, _Alpha;
};

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.position = mul(unity_MatrixVP, float4(mul(unity_ObjectToWorld, float4(input.position, 1.0)).xyz, 1.0));
	output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw + _CloudSpeed * _Time * 0.003;
	output.position.z /= output.position.w;

	output.color.a = input.position.y * 0.005 - 0.5;
	output.color.rgb = lerp(_FogColor, _SkyColor.rgb, output.color.a);
	return output;
}

float3 Fragment(FragmentInput i) : SV_Target
{
	float4 color = tex2D(_MainTex, i.uv);
	float4 fadeTex = tex2D(_FadeTexture, i.uv);

	// Fade between the two textures based on transition factor
	color = lerp(color, fadeTex, _LerpFactor);

	return lerp(i.color.rgb, color.rgb * _FogColor, color.a * i.color.a);
}