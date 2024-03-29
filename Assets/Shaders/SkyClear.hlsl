﻿#include "Common.hlsl"

float4 Vertex(uint id : SV_VertexID) : SV_Position
{
	float2 uv = float2((id << 1) & 2, id & 2);
	return float4(uv * 2.0 - 1.0, 1.0, 1.0);
}

float3 Fragment(float4 position : SV_Position) : SV_Target
{
	return SampleVolumetricLighting(position.xy, _Far);
}
