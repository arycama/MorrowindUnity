#include "Common.hlsl"

#ifdef __INTELLISENSE__
	#define GBUFFER
#endif

struct VertexInput
{
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
		float3 viewPosition : POSITION1;
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

cbuffer UnityPerMaterial
{
	float4 _MainTex_ST;
};

float4 _Control_TexelSize;

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.position = ObjectToClipPosition(input.position, 0);
	
	#ifdef GBUFFER
		output.viewPosition = ObjectToViewPosition(input.position, 0);
		output.uv = float4(input.uv.xy, input.uv.zw * _MainTex_ST.xy + _MainTex_ST.zw);
		output.color = GammaToLinear(input.color);
		output.normal = WorldToViewNormal(input.normal);
	#endif
	
	return output;
}

float4 BilinearWeights(float2 uv, float2 textureSize)
{
	float2 localUv = frac(uv * textureSize - 0.5 + rcp(512.0));
	float4 weights = localUv.xxyy * float4(-1, 1, 1, -1) + float4(1, 0, 0, 1);
	return weights.zzww * weights.xyyx;
}

FragmentOutput Fragment(FragmentInput input)
{
	FragmentOutput output;

	#ifdef GBUFFER
		float4 terrainData = _Control.Gather(LinearClampSampler, input.uv.xy) * 255.0;
		float4 weights = BilinearWeights(input.uv.xy, _Control_TexelSize.zw);
	
		float3 color = _MainTex.Sample(LinearRepeatSampler, float3(input.uv.zw, terrainData.x)) * weights.x;
		color += _MainTex.Sample(LinearRepeatSampler, float3(input.uv.zw, terrainData.y)) * weights.y;
		color += _MainTex.Sample(LinearRepeatSampler, float3(input.uv.zw, terrainData.z)) * weights.z;
		color += _MainTex.Sample(LinearRepeatSampler, float3(input.uv.zw, terrainData.w)) * weights.w;
		color *= input.color;
		
		float3 emissive = AmbientLight * color;
		if (ViewPosition.y < WaterHeight)
			emissive = lerp(emissive, emissive * UnderwaterColor, UnderwaterColorWeight);
		
		output.gbuffer = OutputGbuffer(color, input.normal, emissive, normalize(-input.viewPosition), input.position.xy);
	#endif
	
	return output;
}
