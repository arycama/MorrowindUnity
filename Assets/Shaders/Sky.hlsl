#include "Common.hlsl"

struct VertexInput
{
	float3 position : POSITION;
	float2 uv : TEXCOORD;
	uint instanceID : SV_InstanceID;
};

struct FragmentInput
{
	float3 uv : TEXCOORD0;
	float4 position : SV_POSITION;
};

sampler2D _MainTex, _FadeTexture;
float4 _SkyColor, unity_FogColor;

cbuffer UnityPerMaterial
{
	float4 _MainTex_ST;
	float _CloudSpeed, _TimeOfDay, _LerpFactor, _Alpha;
}

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.position = ObjectToClip(input.position, input.instanceID);
	output.uv.xy = ApplyScaleOffset(input.uv, _MainTex_ST) + _CloudSpeed * _Time.y * 0.003;
	output.uv.z = input.position.y;
	output.position.z /= output.position.w;

	return output;
}

float4 Fragment(FragmentInput i) : SV_Target
{
	float4 color = tex2D(_MainTex, i.uv.xy);
	float4 fadeTex = tex2D(_FadeTexture, i.uv.xy);

	// Fade between the two textures based on transition factor
	color = lerp(color, fadeTex, _LerpFactor);
	
	float4 volumetricFog = SampleVolumetricLighting(i.position.xy, _ProjectionParams.z);
	float3 fog = _FogColor;// volumetricFog.rgb;
	//return float4(fog, 1);
	float4 fogColor;
	fogColor.a = i.uv.z * 0.005 - 0.5;
	fogColor.rgb = lerp(fog, _SkyColor.rgb, fogColor.a);

	color.rgb = lerp(fogColor.rgb, color.rgb * fog, color.a * fogColor.a) ;
	color.a = saturate(_SkyColor.a + color.a);
	return color;
}
