#include "Common.hlsl"

struct VertexInput
{
	uint vertexId : SV_VertexID;
	float3 position : POSITION;
	float2 uv : TEXCOORD;
};

struct FragmentInput
{
	float4 position : SV_POSITION;
	float3 worldPosition : POSITION1;
	float2 uv : TEXCOORD0;
	float3 color : COLOR;
};

float4 _SkyColor;

FragmentInput Vertex(VertexInput input)
{
	float3 worldPosition = ObjectToWorldPosition(input.position * 6, 0);

	FragmentInput output;
	output.worldPosition = worldPosition;
	output.position = WorldToClipPosition(worldPosition);
	output.uv = input.uv;
	output.position.z /= output.position.w;
	
	float alpha = (input.vertexId & 1) ? 0.0 : 1.0;
	output.color = lerp(FogColor, _SkyColor.rgb, alpha);
	return output;
}

float3 Fragment(FragmentInput input) : SV_Target
{
	float3 color = input.color;
	
	//if (ViewPosition.y < WaterHeight)
	//	color = lerp(color, UnderwaterColor, UnderwaterColorWeight);
	
	return color;
}