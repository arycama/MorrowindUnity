#include "Common.hlsl"

struct VertexInput
{
	uint instanceId : SV_InstanceID;
	float3 position : POSITION;
	
	#ifndef UNITY_PASS_SHADOWCASTER
		float3 normal : NORMAL;
		float3 color : COLOR;
		float4 uv : TEXCOORD;
	#endif
};

struct FragmentInput
{
	float4 position : SV_Position;
	
	#ifndef UNITY_PASS_SHADOWCASTER
		float3 worldPosition : POSITION1;
		float3 normal : NORMAL;
		float3 color : COLOR;
		float4 uv : TEXCOORD;
	#endif
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
	float3 worldPosition = ObjectToWorld(input.position, input.instanceId);
	
	FragmentInput output;
	output.position = WorldToClip(worldPosition);
	
	#ifndef UNITY_PASS_SHADOWCASTER
		output.worldPosition = worldPosition;
		output.uv = float4(input.uv.xy, input.uv.zw * _MainTex_ST.xy + _MainTex_ST.zw);
		output.color = input.color;
		output.normal = input.normal;
	#endif
	
	return output;
}

#ifdef UNITY_PASS_SHADOWCASTER
	void Fragment() { }
#else
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
	
		float3 shadowPosition =  MultiplyPoint3x4((float3x4)_WorldToShadow, input.worldPosition);
		if (all(saturate(shadowPosition.xy) == shadowPosition.xy))
			lighting *= _DirectionalShadows.SampleCmpLevelZero(sampler_DirectionalShadows, shadowPosition.xy, shadowPosition.z);
	
		lighting += _AmbientLight;
		color *= lighting * input.color;
	
		float fogFactor = saturate(input.position.w * _FogScale + _FogOffset);
		color = lerp(color, _FogColor, fogFactor);
	
		return color;
	}
#endif