#include "Common.hlsl"
#include "RaytracingCommon.hlsl"

Texture2D _Control;
Texture2DArray<float3> _MainTex;

cbuffer UnityPerMaterial
{
	float4 _MainTex_ST;
};

float4 _Control_TexelSize;

float4 BilinearWeights(float2 uv, float2 textureSize)
{
	float2 localUv = frac(uv * textureSize - 0.5 + rcp(512.0));
	float4 weights = localUv.xxyy * float4(-1, 1, 1, -1) + float4(1, 0, 0, 1);
	return weights.zzww * weights.xyyx;
}

uint3 FetchTriangleIndices(uint primitiveIndex)
{
	uint offsetInBytes = (primitiveIndex * 3) << 1;
	uint dwordAlignedOffset = offsetInBytes & ~3;
	uint2 fourIndices = unity_MeshIndexBuffer_RT.Load2(dwordAlignedOffset);

	uint3 indices;
	if (dwordAlignedOffset == offsetInBytes)
	{
		indices.x = fourIndices.x & 0xffff;
		indices.y = (fourIndices.x >> 16) & 0xffff;
		indices.z = fourIndices.y & 0xffff;
	}
	else
	{
		indices.x = (fourIndices.x >> 16) & 0xffff;
		indices.y = fourIndices.y & 0xffff;
		indices.z = (fourIndices.y >> 16) & 0xffff;
	}

	return indices;
}

float4 FetchUv(uint vertexIndex)
{
	uint2 fourHalfs = unity_MeshVertexBuffers_RT[2].Load2(vertexIndex * 8);
	return f16tof32(fourHalfs.xxyy >> uint4(0, 16, 0, 16));
}

float4 FetchColor(uint vertexIndex)
{
	uint data = unity_MeshVertexBuffers_RT[3].Load(vertexIndex * 4);
	return ((data >> uint4(0, 8, 16, 24)) & 255) / 255.0;
}

float3 FetchNormal(uint vertexIndex)
{
	uint data = unity_MeshVertexBuffers_RT[1].Load(vertexIndex * 4);
	uint4 bytes = (data >> uint4(0, 8, 16, 24)) & 255;
	int4 signedByte = (int4) (bytes << 24) >> 24;
	return signedByte / 127.0f;
}

[shader("closesthit")]
void Raytracing(inout RayColorPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
{
	uint index = PrimitiveIndex();
	uint3 triangleIndices = FetchTriangleIndices(index);
	
	float4 uv0 = FetchUv(triangleIndices.x);
	float4 uv1 = FetchUv(triangleIndices.y);
	float4 uv2 = FetchUv(triangleIndices.z);
	float4 uv = BarycentricInterpolate(uv0, uv1, uv2, attribs.barycentrics.x, attribs.barycentrics.y);
	
	float3 normal0 = FetchNormal(triangleIndices.x);
	float3 normal1 = FetchNormal(triangleIndices.y);
	float3 normal2 = FetchNormal(triangleIndices.z);
	float3 normal = BarycentricInterpolate(normal0, normal1, normal2, attribs.barycentrics.x, attribs.barycentrics.y);
	
	float4 color0 = FetchColor(triangleIndices.x);
	float4 color1 = FetchColor(triangleIndices.y);
	float4 color2 = FetchColor(triangleIndices.z);
	
	color0 = GammaToLinear(color0);
	color1 = GammaToLinear(color1);
	color2 = GammaToLinear(color2);
	
	float4 vertexColor = BarycentricInterpolate(color0, color1, color2, attribs.barycentrics.x, attribs.barycentrics.y);
	
	float3 viewNormal = WorldToViewNormal(normal);
	
	float4 terrainData = _Control.Gather(LinearClampSampler, uv.xy) * 255.0;
	float4 weights = BilinearWeights(uv.xy, _Control_TexelSize.zw);
	
	uv.zw = uv.zw * _MainTex_ST.xy + _MainTex_ST.zw;
	
	float3 color = _MainTex.SampleLevel(LinearRepeatSampler, float3(uv.zw, terrainData.x), 0.0) * weights.x;
	color += _MainTex.SampleLevel(LinearRepeatSampler, float3(uv.zw, terrainData.y), 0.0) * weights.y;
	color += _MainTex.SampleLevel(LinearRepeatSampler, float3(uv.zw, terrainData.z), 0.0) * weights.z;
	color += _MainTex.SampleLevel(LinearRepeatSampler, float3(uv.zw, terrainData.w), 0.0) * weights.w;
	color *= vertexColor.rgb;
	
	color *= AmbientLight + saturate(dot(viewNormal, SunDirection)) * SunColor;
	payload.color = color;
}