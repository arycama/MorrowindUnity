#include "Common.hlsl"

struct VertexInput
{
	uint instanceID : SV_InstanceID;
	float3 position : POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD;
	float3 color : COLOR;
};

struct FragmentInput
{
	float4 position : SV_Position;
	float3 worldPosition : POSITION1;
	float2 uv : TEXCOORD;
	float3 normal : NORMAL;
	float3 color : COLOR;
};

Texture2D _MainTex, _EmissionMap;
SamplerState sampler_MainTex;

cbuffer UnityPerMaterial
{
	float4 _Color, _MainTex_ST;
	float3 _EmissionColor;
	float _Cutoff;
};

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.worldPosition = ObjectToWorld(input.position);
	output.position = WorldToClip(output.worldPosition);
	output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
	output.normal = ObjectToWorldNormal(input.normal);
	output.color = _AmbientLight * input.color + _EmissionColor;
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	float4 color = _MainTex.Sample(sampler_MainTex, input.uv);
	
	float3 normal = normalize(input.normal);
	float3 lighting = saturate(dot(normal, _SunDirection)) * _SunColor;
	
	float4 shadowPosition = mul(_WorldToShadow, float4(input.worldPosition, 1.0));
	lighting *= _DirectionalShadows.SampleCmpLevelZero(sampler_DirectionalShadows, shadowPosition.xy, shadowPosition.z);
	
	lighting += input.color;
	color.rgb *= lighting;
	
	if (_FogEnabled)
	{
		float fogFactor = saturate((input.position.w - _FogStartDistance) / (_FogEndDistance - _FogStartDistance));
		color.rgb = lerp(color.rgb, _FogColor, fogFactor);
	}
	
	return color;
}