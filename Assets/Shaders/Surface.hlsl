#include "Common.hlsl"

struct VertexInput
{
	uint instanceId : SV_InstanceID;
	float3 position : POSITION;
	
	#ifndef UNITY_PASS_SHADOWCASTER
		float3 normal : NORMAL;
		float2 uv : TEXCOORD;
		float3 color : COLOR;
	#endif
};

struct FragmentInput
{
	float4 position : SV_Position;
	
	#ifndef UNITY_PASS_SHADOWCASTER
		float3 worldPosition : POSITION1;
		float2 uv : TEXCOORD;
		float3 normal : NORMAL;
		float3 color : COLOR;
	#endif
};

struct FragmentOutput
{
	#ifndef UNITY_PASS_SHADOWCASTER
		float4 color : SV_Target;
	#endif
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
	float3 worldPosition = ObjectToWorld(input.position, input.instanceId);
	
	FragmentInput output;
	output.position = WorldToClip(worldPosition);
	
	#ifndef UNITY_PASS_SHADOWCASTER
		output.worldPosition = worldPosition;
		output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
		output.normal = ObjectToWorldNormal(input.normal, input.instanceId);
		output.color = _AmbientLight * input.color + _EmissionColor;
	#endif
	
	return output;
}

FragmentOutput Fragment(FragmentInput input)
{
	FragmentOutput output;
	
	#ifndef UNITY_PASS_SHADOWCASTER
		float4 color = _MainTex.Sample(sampler_MainTex, input.uv);
	
		float3 normal = normalize(input.normal);
		float3 lighting = saturate(dot(normal, _SunDirection)) * _SunColor;
	
		float3 shadowPosition = MultiplyPoint3x4((float3x4) _WorldToShadow, input.worldPosition);
		if (all(saturate(shadowPosition.xy) == shadowPosition.xy))
			lighting *= _DirectionalShadows.SampleCmpLevelZero(sampler_DirectionalShadows, shadowPosition.xy, shadowPosition.z);
	
		lighting += input.color;
		color.rgb *= lighting;
	
		float fogFactor = saturate(input.position.w * _FogScale + _FogOffset);
		color.rgb = lerp(color.rgb, _FogColor, fogFactor);
		
		output.color = color;
	#endif
	
	return output;
}