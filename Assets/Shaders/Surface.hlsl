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

cbuffer UnityPerMaterial
{
	float4 _Color, _MainTex_ST;
	float3 _EmissionColor;
	float _Cutoff;
};

FragmentInput Vertex(VertexInput input)
{
	#ifdef INSTANCING_ON
		float4x4 localToWorld = unity_Builtins0Array[unity_BaseInstanceID + input.instanceID].unity_ObjectToWorldArray;
		float4x4 worldToObject = unity_Builtins0Array[unity_BaseInstanceID + input.instanceID].unity_WorldToObjectArray;
	#else
		float4x4 localToWorld = unity_ObjectToWorld;
		float4x4 worldToObject = unity_WorldToObject;
	#endif
	
	
    FragmentInput output;
	output.worldPosition = mul(localToWorld, float4(input.position, 1.0)).xyz;
	output.position = mul(unity_MatrixVP, float4(output.worldPosition, 1.0));
	output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
	output.normal = mul(input.normal, (float3x3)worldToObject);
	output.color = _Ambient * input.color + _EmissionColor;
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	float4 color = _MainTex.Sample(_LinearRepeatSampler, input.uv);
	
	float3 normal = normalize(input.normal);
	float3 lighting = saturate(dot(normal, _SunDirection)) * _SunColor;
	
	float4 shadowPosition = mul(_WorldToShadow, float4(input.worldPosition, 1.0));
	if (all(saturate(shadowPosition.xyz) == shadowPosition.xyz))
		lighting *= _DirectionalShadows.SampleCmpLevelZero(_LinearClampCompareSampler, shadowPosition.xy, shadowPosition.z);
	
	lighting += input.color;
	color.rgb *= lighting;
	
	if (_FogEnabled)
	{
		float fogFactor = saturate((input.position.w - _FogStartDistance) / (_FogEndDistance - _FogStartDistance));
		color.rgb = lerp(color.rgb, _FogColor, fogFactor);
	}
	
	return color;
}