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

float3 GetNormal(uint vertexIndex)
{
	uint data = unity_MeshVertexBuffers_RT[1].Load(vertexIndex);
	uint4 bytes = (data >> uint4(0, 8, 16, 24)) & 255;
	int4 signedByte = (int4) (bytes << 24) >> 24;
	return signedByte.xyz / 127.0;
}

float4 GetUv(uint vertexIndex)
{
	uint2 data;
	data.x = unity_MeshVertexBuffers_RT[2].Load(vertexIndex * 2 + 0);
	data.y = unity_MeshVertexBuffers_RT[2].Load(vertexIndex * 2 + 1);
	return f16tof32(data.xxyy >> uint2(0u, 16u).xyxy);
}

float4 GetColor(uint vertexIndex)
{
	uint data = unity_MeshVertexBuffers_RT[3].Load(vertexIndex);
	return ((data >> uint4(0, 8, 16, 24)) & 255) / 255.0;
}

[shader("closesthit")]
void Raytracing(inout RayColorPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
{
	uint index = PrimitiveIndex();
	uint3 triangleIndices = GetTriangleIndices(index);
	
	float3 normal0 = GetNormal(triangleIndices.x);
	float3 normal1 = GetNormal(triangleIndices.y);
	float3 normal2 = GetNormal(triangleIndices.z);
	float3 normal = BarycentricInterpolate(normal0, normal1, normal2, attribs.barycentrics);
	
	float4 uv0 = GetUv(triangleIndices.x);
	float4 uv1 = GetUv(triangleIndices.y);
	float4 uv2 = GetUv(triangleIndices.z);
	float4 uv = BarycentricInterpolate(uv0, uv1, uv2, attribs.barycentrics);
	
	float3 color0 = GammaToLinear(GetColor(triangleIndices.x).rgb);
	float3 color1 = GammaToLinear(GetColor(triangleIndices.y).rgb);
	float3 color2 = GammaToLinear(GetColor(triangleIndices.z).rgb);
	
	float3 vertexColor = BarycentricInterpolate(color0, color1, color2, attribs.barycentrics);
	
	float3 viewNormal = WorldToViewNormal(normal);
	
	float4 terrainData = _Control.Gather(LinearClampSampler, uv.xy) * 255.0;
	float4 weights = BilinearWeights(uv.xy, _Control_TexelSize.zw);
	
	uv.zw = uv.zw * _MainTex_ST.xy + _MainTex_ST.zw;
	
	float3 color = _MainTex.SampleLevel(LinearRepeatSampler, float3(uv.zw, terrainData.x), 0.0) * weights.x;
	color += _MainTex.SampleLevel(LinearRepeatSampler, float3(uv.zw, terrainData.y), 0.0) * weights.y;
	color += _MainTex.SampleLevel(LinearRepeatSampler, float3(uv.zw, terrainData.z), 0.0) * weights.z;
	color += _MainTex.SampleLevel(LinearRepeatSampler, float3(uv.zw, terrainData.w), 0.0) * weights.w;
	color *= vertexColor;
	
	float3 L = MultiplyVector(ViewToWorld, SunDirection);
	//L = SampleConeUniform(u.x, u.y, SunCosAngle, L);
	
	RayTransmittancePayload shadowPayload;
	shadowPayload.transmittance = 0.0;
	
	RayDesc shadowRay;
	shadowRay.Origin = WorldRayOrigin() + WorldRayDirection() * RayTCurrent();
	shadowRay.Direction = L;
	shadowRay.TMin = 0.1;
	shadowRay.TMax = 8192.0;
		
	uint flags = RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_CLOSEST_HIT_SHADER | RAY_FLAG_CULL_BACK_FACING_TRIANGLES;
	TraceRay(SceneRaytracingAccelerationStructure, flags, 0xFF, 0, 1, 1, shadowRay, shadowPayload);
	
	color *= AmbientLight + saturate(dot(viewNormal, SunDirection)) * SunColor * shadowPayload.transmittance;
	payload.color = color;
}