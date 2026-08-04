#include "Common.hlsl"

struct VertexInput
{
	uint instanceId : SV_InstanceID;
	float3 position : POSITION;
	
	#ifdef GBUFFER
		float3 normal : NORMAL;
		float3 color : COLOR;
		float4 uv : TEXCOORD;
	#endif
};

struct FragmentInput
{
	float4 position : SV_Position;
	
	#ifdef GBUFFER
		float3 worldPosition : POSITION1;
		float3 normal : NORMAL;
		float3 color : COLOR;
		float4 uv : TEXCOORD;
	#endif
};

struct FragmentOutput
{
	#ifdef GBUFFER
		GbufferOutput gbuffer;
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
	output.position = WorldToClipPosition(worldPosition);
	
	#ifdef GBUFFER
		output.worldPosition = worldPosition;
		output.uv = float4(input.uv.xy, input.uv.zw * _MainTex_ST.xy + _MainTex_ST.zw);
		output.color = input.color;
		output.normal = input.normal;
	#endif
	
	return output;
}

FragmentOutput Fragment(FragmentInput input)
{
	FragmentOutput output;

	#ifdef GBUFFER
		float4 terrainData = _Control.Gather(sampler_Control, input.uv.xy) * 255.0;
		float4 weights = BilinearWeights(input.uv.xy, _Control_TexelSize.zw);
	
		float3 color = _MainTex.Sample(sampler_MainTex, float3(input.uv.zw, terrainData.x)) * weights.x;
		color += _MainTex.Sample(sampler_MainTex, float3(input.uv.zw, terrainData.y)) * weights.y;
		color += _MainTex.Sample(sampler_MainTex, float3(input.uv.zw, terrainData.z)) * weights.z;
		color += _MainTex.Sample(sampler_MainTex, float3(input.uv.zw, terrainData.w)) * weights.w;
		color *= input.color;
		
		output.gbuffer.albedoMetallic = float4(color, 0.0);
		output.gbuffer.normalOcclusionRoughness.xyz = normalize(input.normal);
		output.gbuffer.emission = float4(AmbientLight * color, 0.0);
	#endif
	
	return output;
}
