#include "Common.hlsl"

struct VertexInput
{
	float3 position : POSITION;
	float3 normal : NORMAL;
    float3 color : COLOR;
    float2 uv : TEXCOORD;
};

struct FragmentInput
{
	float4 position : SV_Position;
	float3 worldPosition : POSITION1;
	float3 normal : NORMAL;
	float3 color : COLOR;
	float2 uv : TEXCOORD;
};

Texture2DArray<float3> _MainTex;
Texture2D _Control, _Blend;
float4 _Control_ST, _MainTex_ST, _Control_TexelSize;

FragmentInput Vertex(VertexInput input)
{
    FragmentInput output;
	output.worldPosition = mul(unity_ObjectToWorld, float4(input.position, 1.0)).xyz;
	output.position = mul(unity_MatrixVP, float4(output.worldPosition, 1.0));
	output.uv = input.uv;
	output.color = input.color;
	output.normal = input.normal;
	return output;
}

float4 BilinearWeights(float2 uv)
{
	float4 weights = uv.xxyy * float4(-1, 1, 1, -1) + float4(1, 0, 0, 1);
	return weights.zzww * weights.xyyx;
}

float4 BilinearWeights(float2 uv, float2 textureSize)
{
	const float2 offset = 1.0 / 512.0;
	float2 localUv = frac(uv * textureSize + (-0.5 + offset));
	return BilinearWeights(localUv);
}

float3 Fragment(FragmentInput input) : SV_Target
{
	//float2 terrainUv = (input.uv * (_Control_TexelSize.zw - 1.0) + 0.5) * _Control_TexelSize.xy;
	//float4 terrainData = _Control.GatherAlpha(_PointClampSampler, floor(terrainUv * _Control_TexelSize.zw - 0.5) * _Control_TexelSize.xy) * 255.0;
	float4 terrainData = _Control.GatherAlpha(_PointClampSampler, input.uv) * 255.0;
	float4 weights = BilinearWeights(input.uv, _Control_TexelSize.zw);
	
	//float2 localUv = frac(terrainUv * _Control_TexelSize.zw - 0.5);
	
	float3 color = _MainTex.Sample(_LinearRepeatSampler, float3(input.uv * _MainTex_ST.xy + _MainTex_ST.zw, terrainData.x)) * weights.x;
	color += _MainTex.Sample(_LinearRepeatSampler, float3(input.uv * _MainTex_ST.xy + _MainTex_ST.zw, terrainData.y)) * weights.y;
	color += _MainTex.Sample(_LinearRepeatSampler, float3(input.uv * _MainTex_ST.xy + _MainTex_ST.zw, terrainData.z)) * weights.z;
	color += _MainTex.Sample(_LinearRepeatSampler, float3(input.uv * _MainTex_ST.xy + _MainTex_ST.zw, terrainData.w)) * weights.w;
	
	float3 normal = normalize(input.normal);
	float3 lighting = saturate(dot(normal, _SunDirection)) * _SunColor;
	
	float4 shadowPosition = mul(_WorldToShadow, float4(input.worldPosition, 1.0));
	if (all(saturate(shadowPosition.xyz) == shadowPosition.xyz))
		lighting *= _DirectionalShadows.SampleCmpLevelZero(_LinearClampCompareSampler, shadowPosition.xy, shadowPosition.z);
	
	lighting += _Ambient;
	color.rgb *= lighting * input.color;
	
	if(_FogEnabled)
	{
		float fogFactor = saturate((input.position.w - _FogStartDistance) / (_FogEndDistance - _FogStartDistance));
		color.rgb = lerp(color.rgb, _FogColor, fogFactor);
	}
	
	return color;
}