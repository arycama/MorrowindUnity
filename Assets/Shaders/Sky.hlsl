#include "Common.hlsl"

struct VertexInput
{
	float3 position : POSITION;
	float2 uv : TEXCOORD;
	uint instanceID : SV_InstanceID;
};

struct FragmentInput
{
	float2 uv : TEXCOORD0;
	float4 position : SV_POSITION;
};

Texture2D<float4> _MainTex, _FadeTexture;
float3 _SkyColor;

cbuffer UnityPerMaterial
{
	float4 _MainTex_ST;
	float _CloudSpeed, _TimeOfDay, _LerpFactor, _Alpha;
}

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.position = ObjectToClip(input.position, input.instanceID);
	output.uv = ApplyScaleOffset(input.uv, _MainTex_ST) + _CloudSpeed * _Time * 0.003;
	return output;
}

float3 Fragment(FragmentInput input) : SV_Target
{
	float4 color = _MainTex.SampleBias(_LinearRepeatSampler, input.uv, _MipBias);
	float4 fadeTex = _FadeTexture.SampleBias(_LinearRepeatSampler, input.uv, _MipBias);

	// Fade between the two textures based on transition factor
	color = lerp(color, fadeTex, _LerpFactor);
	
	// Actual sky fades between clouds and sky color
	color.rgb = lerp(_SkyColor, color.rgb, color.a) * _Exposure;
	
	return ApplyFog(color.rgb, input.position.xy, input.position.w);
}

