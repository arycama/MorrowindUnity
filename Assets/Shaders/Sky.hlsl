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
	float4 color : TEXCOORD2;
};

sampler2D _MainTex, _FadeTexture;
float4 _MainTex_ST;
float4 _SkyColor, unity_FogColor;
float _CloudSpeed, _TimeOfDay, _LerpFactor, _Alpha;

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.position = ObjectToClip(input.position, input.instanceID);
	output.uv = ApplyScaleOffset(input.uv, _MainTex_ST) + _CloudSpeed * _Time.y * 0.003;
	output.position.z /= output.position.w;

	output.color.a = input.position.y * 0.005 - 0.5;
	output.color.rgb = lerp(_FogColor, _SkyColor.rgb, output.color.a);

	return output;
}

float4 Fragment(FragmentInput i) : SV_Target
{
	float4 color = tex2D(_MainTex, i.uv);
	float4 fadeTex = tex2D(_FadeTexture, i.uv);

	// Fade between the two textures based on transition factor
	color = lerp(color, fadeTex, _LerpFactor);

	color.rgb = lerp(i.color.rgb, color.rgb * _FogColor, color.a * i.color.a);
	color.a = saturate(_SkyColor.a + color.a);

	return color;
}
