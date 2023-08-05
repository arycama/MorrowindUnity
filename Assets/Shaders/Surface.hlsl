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

cbuffer UnityPerMaterial
{
	float4 _Color, _MainTex_ST;
	float3 _EmissionColor;
	float _Cutoff;
};

Texture2D _MainTex, _EmissionMap;

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.worldPosition = ObjectToWorld(input.position, input.instanceID);
	output.position = WorldToClip(output.worldPosition);
	output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
	output.normal = ObjectToWorldNormal(input.normal, input.instanceID);
	output.color = _AmbientLightColor * input.color + _EmissionColor;
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	float4 color = _MainTex.Sample(_LinearRepeatSampler, input.uv);
	float3 normal = normalize(input.normal);
	float3 lighting = GetLighting(normal, input.worldPosition);
	
	// Emissive
	lighting += input.color;
	color.rgb *= lighting;
	//color.rgb = ApplyFog(color.rgb, input.worldPosition, InterleavedGradientNoise(input.position.xy, 0));
	
	#if defined(UNITY_PASS_SHADOWCASTER) && defined(_ALPHABLEND_ON)
		clip(color.a - InterleavedGradientNoise(input.position.xy, 0));
	#endif
	
	return color;
}