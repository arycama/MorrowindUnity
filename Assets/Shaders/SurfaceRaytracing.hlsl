#include "Common.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Raytracing.hlsl"

#ifdef __INTELLISENSE__
	#define _ALPHABLEND_ON
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
void AnyHitVisibility(inout RayTransmittancePayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
{
	uint index = PrimitiveIndex();
	uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(index);
	
	float2 uv0 = UnityRayTracingFetchVertexAttribute2(triangleIndices.x, kVertexAttributeTexCoord0);
	float2 uv1 = UnityRayTracingFetchVertexAttribute2(triangleIndices.y, kVertexAttributeTexCoord0);
	float2 uv2 = UnityRayTracingFetchVertexAttribute2(triangleIndices.z, kVertexAttributeTexCoord0);
	float2 uv = BarycentricInterpolate(uv0, uv1, uv2, attribs.barycentrics.x, attribs.barycentrics.y);
	
	float4 color = _MainTex.SampleLevel(LinearRepeatSampler, uv, 0.0) * _Color;
	payload.transmittance *= 1.0 - color.a;
	
	if (payload.transmittance <= 0.0)
	{
		AcceptHitAndEndSearch();
	}
	else
	{
		IgnoreHit();
	}
}
#else
[shader("closesthit")]
void Raytracing(inout RayTransmittancePayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
{
	payload.transmittance = 0.0;
}
#endif

