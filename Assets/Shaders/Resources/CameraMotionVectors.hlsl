#include "../Common.hlsl"

Texture2D<float> _UnityFBInput0;

float4 GetFullScreenTriangleVertexPosition(uint vertexID)
{
    // note: the triangle vertex position coordinates are x2 so the returned UV coordinates are in range -1, 1 on the screen.
	float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
	return float4(uv * 2.0 - 1.0, 1.0, 1.0);
}

float4 Vertex(uint id : SV_VertexID) : SV_Position
{
	return GetFullScreenTriangleVertexPosition(id);
}

float2 Fragment(float4 positionCS : SV_Position) : SV_Target
{
	float depth = _UnityFBInput0[positionCS.xy];
	
	// Flip due to matrix stupidity
	float3 positionNDC = float3(positionCS.xy / _ScreenParams.xy * 2 - 1, depth);
	positionNDC.y = -positionNDC.y;
	
	float3 positionWS = MultiplyPointProj(_InvVPMatrix, positionNDC).xyz;
	
	float4 nonJitteredPositionCS = MultiplyPoint(_NonJitteredVPMatrix, positionWS);
	float4 previousPositionCS = MultiplyPoint(_PreviousVPMatrix, positionWS);
	return MotionVectorFragment(nonJitteredPositionCS, previousPositionCS);
}
