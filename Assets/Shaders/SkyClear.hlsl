#include "Common.hlsl"

float4 Vertex(uint id : SV_VertexID) : SV_Position
{
	float2 uv = float2((id << 1) & 2, id & 2);
	return float4(uv * 2.0 - 1.0, 1.0, 1.0);
}

float3 Fragment(float4 position : SV_Position) : SV_Target
{
	float3 volumeUv = float3(position.xy / floor(_ScreenParams.xy * _Scale), 1.0);
	return _VolumetricLighting.Sample(_LinearClampSampler, volumeUv).rgb;
}
