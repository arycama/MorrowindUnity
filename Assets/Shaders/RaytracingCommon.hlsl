#ifndef RAYTRACING_COMMON_INCLUDED
#define RAYTRACING_COMMON_INCLUDED

#ifdef __INTELLISENSE__
const static uint RAY_FLAG_NONE = 0x00,
	RAY_FLAG_FORCE_OPAQUE = 0x01,
	RAY_FLAG_FORCE_NON_OPAQUE = 0x02,
	RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH = 0x04,
	RAY_FLAG_SKIP_CLOSEST_HIT_SHADER = 0x08,
	RAY_FLAG_CULL_BACK_FACING_TRIANGLES = 0x10,
	RAY_FLAG_CULL_FRONT_FACING_TRIANGLES = 0x20,
	RAY_FLAG_CULL_OPAQUE = 0x40,
	RAY_FLAG_CULL_NON_OPAQUE = 0x80,
	RAY_FLAG_SKIP_TRIANGLES = 0x100,
	RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES = 0x200;
#endif

RaytracingAccelerationStructure SceneRaytracingAccelerationStructure : register(t0, space1);

const static uint kMaxVertexStreams = 8;
Buffer<uint> unity_MeshIndexBuffer_RT, unity_MeshVertexBuffers_RT[kMaxVertexStreams];

struct AttributeData
{
	float2 barycentrics;
};

float1 BarycentricInterpolate(float1 x, float1 y, float1 z, float2 uv) { return mad(uv.y, z, mad(uv.x, y, mad(-x, uv.y, mad(-x, uv.x, x)))); }
float2 BarycentricInterpolate(float2 x, float2 y, float2 z, float2 uv) { return mad(uv.y, z, mad(uv.x, y, mad(-x, uv.y, mad(-x, uv.x, x)))); }
float3 BarycentricInterpolate(float3 x, float3 y, float3 z, float2 uv) { return mad(uv.y, z, mad(uv.x, y, mad(-x, uv.y, mad(-x, uv.x, x)))); }
float4 BarycentricInterpolate(float4 x, float4 y, float4 z, float2 uv) { return mad(uv.y, z, mad(uv.x, y, mad(-x, uv.y, mad(-x, uv.x, x)))); }

#endif