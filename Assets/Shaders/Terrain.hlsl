#include "Common.hlsl"

struct VertexInput
{
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

Texture2DArray<float3> _MainTex;
Texture2D<float> _Control;

cbuffer UnityPerMaterial
{
	float4 _Control_ST, _MainTex_ST, _Control_TexelSize;
};

FragmentInput Vertex(VertexInput input)
{
	float3 worldPosition = ObjectToWorld(input.position, 0);
	
    FragmentInput output;
	output.position = WorldToClip(worldPosition);
	
	#ifndef UNITY_PASS_SHADOWCASTER
		output.uv = float4(input.uv.xy, input.uv.zw * _MainTex_ST.xy + _MainTex_ST.zw);
		output.worldPosition = worldPosition;
		output.color = input.color;
		output.normal = input.normal;
	#endif
	
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

#ifdef UNITY_PASS_SHADOWCASTER
void Fragment() { }
#else
float3 Fragment(FragmentInput input) : SV_Target
{	
	float4 terrainData = _Control.Gather(_PointClampSampler, UnjitterTextureUV(input.uv.xy)) * 255.0;
	float4 weights = BilinearWeights(UnjitterTextureUV(input.uv.xy), _Control_TexelSize.zw);
	
	float3 color = _MainTex.Sample(_TrilinearRepeatAniso16Sampler, float3(UnjitterTextureUV(input.uv.zw), terrainData.x)) * weights.x;
	color += _MainTex.Sample(_TrilinearRepeatAniso16Sampler, float3(UnjitterTextureUV(input.uv.zw), terrainData.y)) * weights.y;
	color += _MainTex.Sample(_TrilinearRepeatAniso16Sampler, float3(UnjitterTextureUV(input.uv.zw), terrainData.z)) * weights.z;
	color += _MainTex.Sample(_TrilinearRepeatAniso16Sampler, float3(UnjitterTextureUV(input.uv.zw), terrainData.w)) * weights.w;
	
	float3 normal = normalize(input.normal);
	float3 lighting = GetLighting(normal, input.worldPosition, input.position.xy, input.position.w, color, 0.0, 1.0);
	
	lighting += _AmbientLightColor * _Exposure * color;

	if (!_AoEnabled)
		lighting = ApplyFog(lighting, input.position.xy, input.position.w);
	
	return lighting;
}
#endif