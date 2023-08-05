#include "Common.hlsl"

struct VertexInput
{
	uint instanceID : SV_InstanceID;
	float3 position : POSITION;
};

struct FragmentInput
{
	float4 position : SV_Position;
	float3 worldPosition : POSITION1;
	float2 uv : TEXCOORD;
};

Texture2D _MainTex, _EmissionMap;

cbuffer UnityPerMaterial
{
	float4 _MainTex_ST;
	float _Alpha, _Fade, _Tiling;
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
	output.worldPosition = ObjectToWorld(input.position, input.instanceID);
	output.position = WorldToClip(output.worldPosition);
	output.uv = output.worldPosition.xz * _Tiling;
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	float4 color = _MainTex.Sample(_LinearRepeatSampler, input.uv);
	color.a = _Alpha;
	
	float3 lighting = GetLighting(float3(0, 1, 0), input.worldPosition) + _Ambient;
	color.rgb *= lighting;
	color.rgb = ApplyFog(color.rgb, input.worldPosition, InterleavedGradientNoise(input.position.xy, 0));
	
	return color;
}