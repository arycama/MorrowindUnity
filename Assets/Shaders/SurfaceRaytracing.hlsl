#include "Common.hlsl"
#include "RaytracingCommon.hlsl"

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

float2 GetUv(uint vertexIndex)
{
	uint2 data;
	data.x = unity_MeshVertexBuffers_RT[2].Load(vertexIndex * 2 + 0);
	data.y = unity_MeshVertexBuffers_RT[2].Load(vertexIndex * 2 + 1);
	return asfloat(data);
}

#ifdef _ALPHABLEND_ON
[shader("anyhit")]
void AnyHitVisibility(inout RayTransmittancePayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
{
	uint index = PrimitiveIndex();
	uint3 triangleIndices = GetTriangleIndices(index);
	float2 uv0 = GetUv(triangleIndices.x);
	float2 uv1 = GetUv(triangleIndices.y);
	float2 uv2 = GetUv(triangleIndices.z);
	float2 uv = BarycentricInterpolate(uv0, uv1, uv2, attribs.barycentrics);
	
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

