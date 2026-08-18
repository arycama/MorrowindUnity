
float4 Vertex(uint vertexId : SV_VertexID) : SV_Position
{
	uint localId = vertexId % 3;
	float2 uv = (localId << uint2(0, 1)) & 2;
	return float3(uv * 2.0 - 1.0, 1.0).xyzz;
}

Texture2D<float4> _UnityFBInput0;

float3 Fragment(float4 position : SV_Position) : SV_Target
{
	return _UnityFBInput0[position.xy].rgb;
}
