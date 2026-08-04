#include "Common.hlsl"

struct VertexInput
{
	uint instanceId : SV_InstanceID;
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

struct FragmentOutput
{
	#ifdef GBUFFER
		GbufferOutput gbuffer;
	#endif
	
	#ifdef FORWARD
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
	output.position = WorldToClipPosition(worldPosition);
	output.worldPosition = worldPosition;
	output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
	output.normal = ObjectToWorldNormal(input.normal, input.instanceId);
	output.color = AmbientLight * input.color + _EmissionColor;
	return output;
}

FragmentOutput Fragment(FragmentInput input)
{
	FragmentOutput output;

	float4 color = _MainTex.Sample(sampler_MainTex, input.uv);
	float3 normal = normalize(input.normal);
	
	#ifdef GBUFFER
		output.gbuffer.albedoMetallic = float4(color.rgb, 0.0);
		output.gbuffer.normalOcclusionRoughness = float4(normal * 0.5 + 0.5, 1.0);
		output.gbuffer.emission = float4(input.color * color.rgb, 0.0);
	#endif
	
	#ifdef FORWARD
		float3 lighting = GetLighting(normal, input.worldPosition, input.position);
		lighting += input.color;
		color.rgb *= lighting;
	
		color.rgb = ApplyFog(color.rgb, input.position.w).rgb;
		
	//	color.rgb = ApplyVolumetricLight(color.rgb, input.position.xy, input.position.w);
		
		output.color = color;
	#endif
	
	return output;
}