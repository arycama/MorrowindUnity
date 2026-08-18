
float4 Vertex(uint vertexId : SV_VertexID, out float2 uv : TEXCOORD) : SV_Position
{
	#ifdef FLIP
		uv = (vertexId << uint2(0, 1)) & 2;
		float4 position = float3(uv * 2.0 - 1.0, 1.0).xyzz;
		uv.y = 1.0 - uv.y;
	#else
		uv = (vertexId << uint2(1, 0)) & 2;
		float4 position = float3(uv * 2.0 - 1.0, 1.0).xyzz;
	#endif
	
	return position;
}

Texture2D<float4> CameraTarget;
SamplerState PointClampSampler;

float3 Fragment(float4 position : SV_Position, float2 uv : TEXCOORD) : SV_Target
{
	return CameraTarget.Sample(PointClampSampler, uv).rgb;
}
