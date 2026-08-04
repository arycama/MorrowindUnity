#include "Assets/Shaders/Common.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/CommonShaders.hlsl"

Texture2D<float> CameraDepth;
Texture2D<float3> CameraTarget;
Texture2D<float4> GBufferAlbedoMetallic, GBufferNormalOcclusionRoughness;

VertexFullscreenTriangleOutput Vertex(VertexInput input)
{
	VertexFullscreenTriangleOutput output = VertexFullscreenTriangle(input);
	output.position.z = 0.0;
	return output;
}

float4 Fragment(VertexFullscreenTriangleOutput input) : SV_Target
{
	float depth = CameraDepth[input.position.xy];
	float4 albedoMetallic = GBufferAlbedoMetallic[input.position.xy];
	float4 normalOcclusionRoughness = GBufferNormalOcclusionRoughness[input.position.xy];

	float eyeDepth = LinearEyeDepth(depth);
	float viewDistance = eyeDepth * length(input.worldDirection);
	float3 worldPosition = eyeDepth * input.worldDirection + ViewPosition;
	
	float3 normal = normalize(normalOcclusionRoughness.xyz * 2.0 - 1.0);
	float3 result = GetLighting(normal, worldPosition, float4(input.position.xy, depth, eyeDepth)) * albedoMetallic.rgb;
	
	#ifdef VOLUMETRIC_LIGHT_ON
		float3 volumetricUv = float3(input.position.xy / ViewSize, eyeDepth / MaxDepth);
		volumetricUv.y = 1 - volumetricUv.y;
	
		float4 volumetricLight = VolumetricLighting.Sample(LinearClampSampler, volumetricUv);
		result.rgb = result.rgb * volumetricLight.a + volumetricLight.rgb;
		return float4(result, 1.0 - volumetricLight.a);
	#else
		float fogFactor = FogFactor(viewDistance);
		result = lerp(result, FogColor, fogFactor);
		return float4(result, fogFactor);
	#endif
}
