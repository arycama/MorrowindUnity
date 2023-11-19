#include "../Common.hlsl"

float4 Vertex(uint id : SV_VertexID) : SV_Position
{
	float2 uv = float2((id << 1) & 2, id & 2);
	return float4(uv * 2.0 - 1.0, 1.0, 1.0);
}

float2 Fragment(float4 position : SV_Position) : SV_Target
{
	float depth = _CameraDepth[position.xy];
	float3 positionWS = PixelToWorld(float3(position.xy, depth));
	float4 nonJitteredPositionCS = WorldToClipNonJittered(positionWS);
	float4 previousPositionCS = WorldToClipPrevious(positionWS);
	return MotionVectorFragment(nonJitteredPositionCS, previousPositionCS);
}
