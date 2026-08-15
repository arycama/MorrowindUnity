#include "Common.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Raytracing.hlsl"

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

#ifdef _ALPHABLEND_ON
[shader("anyhit")]
void AnyHitVisibility(inout RayColorPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
{
	uint index = PrimitiveIndex();
	uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(index);
	
	float2 uv0 = UnityRayTracingFetchVertexAttribute2(triangleIndices.x, kVertexAttributeTexCoord0);
	float2 uv1 = UnityRayTracingFetchVertexAttribute2(triangleIndices.y, kVertexAttributeTexCoord0);
	float2 uv2 = UnityRayTracingFetchVertexAttribute2(triangleIndices.z, kVertexAttributeTexCoord0);
	float2 uv = BarycentricInterpolate(uv0, uv1, uv2, attribs.barycentrics.x, attribs.barycentrics.y);
	
	float3 normal0 = UnityRayTracingFetchVertexAttribute3(triangleIndices.x, kVertexAttributeNormal);
	float3 normal1 = UnityRayTracingFetchVertexAttribute3(triangleIndices.y, kVertexAttributeNormal);
	float3 normal2 = UnityRayTracingFetchVertexAttribute3(triangleIndices.z, kVertexAttributeNormal);
	float3 normal = BarycentricInterpolate(normal0, normal1, normal2, attribs.barycentrics.x, attribs.barycentrics.y);
	
	float4 color0 = UnityRayTracingFetchVertexAttribute4(triangleIndices.x, kVertexAttributeColor);
	float4 color1 = UnityRayTracingFetchVertexAttribute4(triangleIndices.x, kVertexAttributeColor);
	float4 color2 = UnityRayTracingFetchVertexAttribute4(triangleIndices.x, kVertexAttributeColor);
	float4 vertexColor = BarycentricInterpolate(color0, color1, color2, attribs.barycentrics.x, attribs.barycentrics.y);
	
	float3 worldNormal = MultiplyVector(ObjectToWorld3x4(), normal);
	float3 viewNormal = WorldToViewNormal(worldNormal);
	
	float3 color = AmbientLight * GammaToLinear(vertexColor.rgb) + _EmissionColor;
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
		uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(index);
	
		float2 uv0 = UnityRayTracingFetchVertexAttribute2(triangleIndices.x, kVertexAttributeTexCoord0);
		float2 uv1 = UnityRayTracingFetchVertexAttribute2(triangleIndices.y, kVertexAttributeTexCoord0);
		float2 uv2 = UnityRayTracingFetchVertexAttribute2(triangleIndices.z, kVertexAttributeTexCoord0);
		float2 uv = BarycentricInterpolate(uv0, uv1, uv2, attribs.barycentrics.x, attribs.barycentrics.y);
	
		float3 normal0 = UnityRayTracingFetchVertexAttribute3(triangleIndices.x, kVertexAttributeNormal);
		float3 normal1 = UnityRayTracingFetchVertexAttribute3(triangleIndices.y, kVertexAttributeNormal);
		float3 normal2 = UnityRayTracingFetchVertexAttribute3(triangleIndices.z, kVertexAttributeNormal);
		float3 normal = BarycentricInterpolate(normal0, normal1, normal2, attribs.barycentrics.x, attribs.barycentrics.y);
		
		float4 color0 = UnityRayTracingFetchVertexAttribute4(triangleIndices.x, kVertexAttributeColor);
		float4 color1 = UnityRayTracingFetchVertexAttribute4(triangleIndices.x, kVertexAttributeColor);
		float4 color2 = UnityRayTracingFetchVertexAttribute4(triangleIndices.x, kVertexAttributeColor);
		float4 vertexColor = BarycentricInterpolate(color0, color1, color2, attribs.barycentrics.x, attribs.barycentrics.y);
	
		float3 worldNormal = MultiplyVector(ObjectToWorld3x4(), normal);
		float3 viewNormal = WorldToViewNormal(worldNormal);
		
		float3 color = AmbientLight * GammaToLinear(vertexColor.rgb) + _EmissionColor;
		color += saturate(dot(viewNormal, SunDirection)) * SunColor;
	
		float4 albedoOpacity = _MainTex.SampleLevel(LinearRepeatSampler, uv, 0.0) * _Color;
		color *= albedoOpacity.rgb;
	
		payload.color = color.rgb;
#endif
}