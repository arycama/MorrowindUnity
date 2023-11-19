#define CUSTOM_LIGHTING_FALLOFF

#include "packages/com.arycama.customrenderpipeline/ShaderLibrary/Common.hlsl"

float CalculateLightFalloff(float rcpLightDist, float sqrLightDist, float rcpSqLightRange)
{
	float lightRange = rcp(sqrt(rcpSqLightRange));
	return Remap(lightRange * rcp(3.0) * rcpLightDist, rcp(3.0));
}