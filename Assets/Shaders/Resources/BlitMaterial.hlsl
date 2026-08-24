#include "../Common.hlsl"

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

float4 Fragment(float4 position : SV_Position, 
#ifdef DEPTH
	out float depth : SV_Depth,
#endif
	float2 uv : TEXCOORD) : SV_Target
{
	#ifdef DEPTH
		#ifdef MSAA
			float2 coord = position.xy;
			coord.y = ViewSize.y - coord.y;
			depth = CameraDepth.Load(coord, 0);
		#else
			depth = CameraDepth.Sample(PointClampSampler, uv);
		#endif
	#endif

	return float4(CameraColor.Sample(PointClampSampler, uv), 1.0);
}
