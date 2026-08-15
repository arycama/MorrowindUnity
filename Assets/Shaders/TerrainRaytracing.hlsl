#include "Common.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Raytracing.hlsl"

cbuffer UnityPerMaterial
{
	float4 _Control_ST, _MainTex_ST, _Control_TexelSize;
};

[shader("closesthit")]
void Raytracing(inout RayTransmittancePayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
{
	payload.transmittance = 0.0;
}