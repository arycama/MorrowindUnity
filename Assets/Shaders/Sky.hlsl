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
	FragmentInput output;
	output.position = ObjectToClipPosition(input.position, 0);
	output.position.z /= output.position.w;
	output.uv = input.uv;// * _MainTex_ST.xy + _MainTex_ST.zw; //	+_CloudSpeed * Time * 0.003;
	
	float alpha = 1.0;
	uint i = input.vertexId;
	if (i >= 49 && i <= 64)
		alpha = 0.0; // bottom-most row
	else if (i >= 33 && i <= 48)
		alpha = 0.25098; // second row
		
	output.color = float4(FogColor.rgb, alpha);
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	float4 color = _MainTex.Sample(sampler_MainTex, input.uv) * input.color;
	
	#ifdef VOLUMETRIC_LIGHT_ON
		float3 volumetricUv = float3(input.position.xy / ViewSize, 1.0);
		float4 volumetricLight = VolumetricLight.Sample(LinearClampSampler, volumetricUv);
		float3 fogLuminance = volumetricLight.rgb;
		float fogTransmittance = volumetricLight.a;
		
		//color.rgb += fogLuminance * color.a;
		
		//return lerp(fogLuminance * (1.0 - fogTransmittance), _SkyColor.rgb, input.fogFactor);
		
	//#else
		//return lerp(FogColor, _SkyColor.rgb, input.fogFactor);
	#endif
	
	//if (ViewPosition.y < WaterHeight)
	//	color.rgb = lerp(color.rgb, UnderwaterColor, UnderwaterColorWeight);
	
	return color;
}