
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

Texture2D<float4> CameraColor;
SamplerState PointClampSampler;
float2 ViewSize;

#ifdef DEPTH
	Texture2D<float> CameraDepth;
#elif defined(DEPTH_MSAA_2)
	Texture2DMS<float, 2> CameraDepth;
#elif defined(DEPTH_MSAA_4)
	Texture2DMS<float, 4> CameraDepth;
#elif defined(DEPTH_MSAA_8)
	Texture2DMS<float, 8> CameraDepth;
#endif

float4 Fragment(float4 position : SV_Position, 
#if defined(DEPTH) || defined(DEPTH_MSAA_2) || defined(DEPTH_MSAA_4) || defined(DEPTH_MSAA_8)
	out float depth : SV_Depth,
#endif
	float2 uv : TEXCOORD) : SV_Target
{
	#ifdef DEPTH
		depth = CameraDepth.Sample(PointClampSampler, uv);
	#elif defined(DEPTH_MSAA_2) || defined(DEPTH_MSAA_4) || defined(DEPTH_MSAA_8)
		float2 coord = position.xy;
		coord.y = ViewSize.y - coord.y;
		depth = CameraDepth.Load(coord, 0);
	#endif

	return CameraColor.Sample(PointClampSampler, uv);
}
