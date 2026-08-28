#ifndef RAYTRACING_COMMON_INCLUDED
#define RAYTRACING_COMMON_INCLUDED

const static uint kMaxVertexStreams = 8;

ByteAddressBuffer unity_MeshIndexBuffer_RT;
ByteAddressBuffer unity_MeshVertexBuffers_RT[kMaxVertexStreams];

struct AttributeData
{
	float2 barycentrics;
};

float1 BarycentricInterpolate(float1 x, float1 y, float1 z, float u, float v)
{
	return mad(v, z, mad(u, y, mad(-x, v, mad(-x, u, x))));
}

float2 BarycentricInterpolate(float2 x, float2 y, float2 z, float u, float v)
{
	return mad(v, z, mad(u, y, mad(-x, v, mad(-x, u, x))));
}

float3 BarycentricInterpolate(float3 x, float3 y, float3 z, float u, float v)
{
	return mad(v, z, mad(u, y, mad(-x, v, mad(-x, u, x))));
}

float4 BarycentricInterpolate(float4 x, float4 y, float4 z, float u, float v)
{
	return mad(v, z, mad(u, y, mad(-x, v, mad(-x, u, x))));
}

#endif