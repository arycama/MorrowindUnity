#include "../Common.hlsl"

Texture2D<float> _Depth;

float4 Vertex(uint id : SV_VertexID) : SV_Position
{
	float2 uv = float2((id << 1) & 2, id & 2);
	return float4(uv * 2.0 - 1.0, 1.0, 1.0);
}

float2 Fragment(float4 positionCS : SV_Position) : SV_Target
{
	float depth = _Depth[positionCS.xy];
	
	// Flip due to matrix stupidity
	float3 positionNDC = float3(positionCS.xy / _ScreenParams.xy * 2 - 1, depth);
	positionNDC.y = -positionNDC.y;
	
	float3 positionWS = MultiplyPointProj(_InvVPMatrix, positionNDC).xyz;
	
	float4 nonJitteredPositionCS = WorldToClipNonJittered(positionWS);
	float4 previousPositionCS = WorldToClipPrevious(positionWS);
	return MotionVectorFragment(nonJitteredPositionCS, previousPositionCS);
}
