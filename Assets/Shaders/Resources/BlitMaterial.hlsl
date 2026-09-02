#include "../Common.hlsl"
#include "../BloomCommon.hlsl"

Texture2D<float4> _UnityFBInput0;
Texture2D<float3> DepthOfField, Bloom;
float4 Bloom_TexelSize;
float BloomStrength;

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
	float2 coord = position.xy;
	coord.y = ViewSize.y - coord.y;
	
	#ifdef DEPTH
		#ifdef MSAA
			depth = CameraDepth.Load(coord, 0);
		#else
			depth = CameraDepth.Sample(PointClampSampler, uv);
		#endif
	#endif
	
	#ifdef RAYTRACED_DEPTH_OF_FIELD
		return float4(DepthOfField[coord], 1.0);
	#endif
	
	#ifdef DIRECT
		float4 color = float4(_UnityFBInput0[position.xy].rgb, 1.0);
	#else
		float4 color = float4(CameraColor.Sample(PointClampSampler, uv), 1.0);
	#endif
	
	#ifdef BLOOM
		float3 bloom = SampleBloom(uv, Bloom, Bloom_TexelSize.xy, 0);
		color.rgb = lerp(color.rgb, bloom, BloomStrength);
	#endif
	
	return color;
}
