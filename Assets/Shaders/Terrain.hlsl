#include "Common.hlsl"

struct VertexInput
{
	float3 position : POSITION;
	float3 normal : NORMAL;
	float3 color : COLOR;
	float4 uv : TEXCOORD;
};

struct FragmentInput
{
	float4 position : SV_Position;
	float3 worldPosition : POSITION1;
	float3 normal : NORMAL;
	float3 color : COLOR;
	float4 uv : TEXCOORD;
};

Texture2D _Control;
Texture2DArray<float3> _MainTex;
SamplerState sampler_Control, sampler_MainTex;

cbuffer UnityPerMaterial
{
	float4 _Control_ST, _MainTex_ST, _Control_TexelSize;
};

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.worldPosition = ObjectToWorld(input.position);
	output.position = WorldToClip(output.worldPosition);
	output.uv = float4(input.uv.xy, input.uv.zw * _MainTex_ST.xy + _MainTex_ST.zw);
	output.color = input.color;
	output.normal = input.normal;
	return output;
}

float3 Fragment(FragmentInput input) : SV_Target
{
	float4 terrainData = _Control.Gather(sampler_Control, input.uv.xy) * 255.0;
	float4 weights = BilinearWeights(input.uv.xy, _Control_TexelSize.zw);
	
	float3 color = _MainTex.Sample(sampler_MainTex, float3(input.uv.zw, terrainData.x)) * weights.x;
	color += _MainTex.Sample(sampler_MainTex, float3(input.uv.zw, terrainData.y)) * weights.y;
	color += _MainTex.Sample(sampler_MainTex, float3(input.uv.zw, terrainData.z)) * weights.z;
	color += _MainTex.Sample(sampler_MainTex, float3(input.uv.zw, terrainData.w)) * weights.w;
	
	float3 normal = normalize(input.normal);
	float3 lighting = saturate(dot(normal, _SunDirection)) * _SunColor;
	
	float4 shadowPosition = mul(_WorldToShadow, float4(input.worldPosition, 1.0));
	if (all(saturate(shadowPosition.xyz) == shadowPosition.xyz))
		lighting *= _DirectionalShadows.SampleCmpLevelZero(sampler_DirectionalShadows, shadowPosition.xy, shadowPosition.z);
	
	lighting += _AmbientLight;
	color.rgb *= lighting * input.color;
	
	if (_FogEnabled)
	{
		float fogFactor = saturate((input.position.w - _FogStartDistance) / (_FogEndDistance - _FogStartDistance));
		color.rgb = lerp(color.rgb, _FogColor, fogFactor);
	}
	
	return color;
}