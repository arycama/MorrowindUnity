#include "Common.hlsl"
#include "RaytracingCommon.hlsl"

#ifdef __INTELLISENSE__
	//#define _ALPHABLEND_ON
#endif

Texture2D<float4> _MainTex, _EmissionMap;
SamplerState sampler_MainTex;

cbuffer UnityPerMaterial
{
	float4 _Color, _MainTex_ST;
	float3 _EmissionColor;
	float _Cutoff, _SrcBlend;
};

uint3 GetTriangleIndices(uint primitiveIndex)
{
	uint byteOffset = (primitiveIndex * 3) << 1;
	uint index = byteOffset >> 2;

	uint2 data;
	data.x = unity_MeshIndexBuffer_RT[index + 0u];
	data.y = unity_MeshIndexBuffer_RT[index + 1u];

	bool isOdd = primitiveIndex & 1u;
	uint3 raw = uint3(data, isOdd ? data.y : data.x).xzy;
	uint3 shift = uint2(isOdd, !isOdd).xyx << 4;
	return (raw >> shift) & 0xffff;
}

float3 GetNormal(uint vertexIndex)
{
	uint3 data;
	data.x = unity_MeshVertexBuffers_RT[1].Load(vertexIndex * 3 + 0);
	data.y = unity_MeshVertexBuffers_RT[1].Load(vertexIndex * 3 + 1);
	data.z = unity_MeshVertexBuffers_RT[1].Load(vertexIndex * 3 + 2);
	return asfloat(data);
}

float4 GetColor(uint vertexIndex)
{
	uint data = unity_MeshVertexBuffers_RT[2].Load(vertexIndex);
	return ((data >> uint4(0, 8, 16, 24)) & 255) / 255.0;
}

float2 GetUv(uint vertexIndex)
{
	uint2 data;
	data.x = unity_MeshVertexBuffers_RT[3].Load(vertexIndex * 2 + 0);
	data.y = unity_MeshVertexBuffers_RT[3].Load(vertexIndex * 2 + 1);
	return asfloat(data);
}

#ifdef _ALPHABLEND_ON
[shader("anyhit")]
void AnyHitVisibility(inout RayColorPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
{
	uint index = PrimitiveIndex();
	uint3 triangleIndices = GetTriangleIndices(index);
	
	float2 uv0 = GetUv(triangleIndices.x);
	float2 uv1 = GetUv(triangleIndices.y);
	float2 uv2 = GetUv(triangleIndices.z);
	float2 uv = BarycentricInterpolate(uv0, uv1, uv2, attribs.barycentrics);
	
	float3 normal0 = GetNormal(triangleIndices.x);
	float3 normal1 = GetNormal(triangleIndices.y);
	float3 normal2 = GetNormal(triangleIndices.z);
	float3 normal = normalize(BarycentricInterpolate(normal0, normal1, normal2, attribs.barycentrics));
	
	float3 color0 = GammaToLinear(GetColor(triangleIndices.x).rgb);
	float3 color1 = GammaToLinear(GetColor(triangleIndices.y).rgb);
	float3 color2 = GammaToLinear(GetColor(triangleIndices.z).rgb);
	float3 vertexColor = BarycentricInterpolate(color0, color1, color2, attribs.barycentrics);
	
	float3 worldNormal = MultiplyVector(ObjectToWorld3x4(), normal);
	float3 viewNormal = WorldToViewNormal(worldNormal);
	
	float3 color = AmbientLight * vertexColor + _EmissionColor;
	color += saturate(dot(viewNormal, SunDirection)) * SunColor;
	
	float4 albedoOpacity = _MainTex.SampleLevel(LinearRepeatSampler, uv, 0.0) * _Color;
	color *= albedoOpacity.rgb;
	
	payload.color += color.rgb * payload.transmittance;
	payload.transmittance *= 1.0 - albedoOpacity.a;
	
	if (payload.transmittance <= 0.0)
	{
		AcceptHitAndEndSearch();
	}
	else
	{
		IgnoreHit();
	}
}
#endif

[shader("closesthit")]
void Raytracing(inout RayColorPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
{
	#ifndef _ALPHABLEND_ON
		uint index = PrimitiveIndex();
		uint3 triangleIndices = GetTriangleIndices(index);
	
		float3 normal0 = GetNormal(triangleIndices.x);
		float3 normal1 = GetNormal(triangleIndices.y);
		float3 normal2 = GetNormal(triangleIndices.z);
		float3 normal = normalize(BarycentricInterpolate(normal0, normal1, normal2, attribs.barycentrics));
		
		float3 color0 = GetColor(triangleIndices.x).rgb;
		float3 color1 = GetColor(triangleIndices.y).rgb;
		float3 color2 = GetColor(triangleIndices.z).rgb;
		float3 vertexColor = BarycentricInterpolate(color0, color1, color2, attribs.barycentrics);
		
		float2 uv0 = GetUv(triangleIndices.x);
		float2 uv1 = GetUv(triangleIndices.y);
		float2 uv2 = GetUv(triangleIndices.z);
		float2 uv = BarycentricInterpolate(uv0, uv1, uv2, attribs.barycentrics);
	
		float3 worldNormal = MultiplyVector(ObjectToWorld3x4(), normal);
		float3 viewNormal = WorldToViewNormal(worldNormal);
		
		float3 color = AmbientLight * vertexColor + _EmissionColor;
		
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
		
		color += saturate(dot(viewNormal, SunDirection)) * SunColor * shadowPayload.transmittance;
	
		float4 albedoOpacity = _MainTex.SampleLevel(LinearRepeatSampler, uv, 0.0) * _Color;
		color *= albedoOpacity.rgb;
	
		payload.color = color.rgb;
#endif
}