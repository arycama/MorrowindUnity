#define CUSTOM_LIGHTING_FALLOFF
#define CUSTOM_LIGHTING

#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Common.hlsl"

float CalculateLightFalloff(float rcpLightDist, float sqrLightDist, float rcpSqLightRange)
{
	float lightRange = rcp(sqrt(rcpSqLightRange));
	return Remap(lightRange * rcp(3.0) * rcpLightDist, rcp(3.0));
}

float3 CalculateLighting(float3 albedo, float3 f0, float roughness, float3 L, float3 V, float3 N)
{
	return albedo;
}