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

float3 Fragment(VertexFullscreenTriangleOutput input) : SV_Target
{
	float depth = CameraDepth[input.position.xy];
	float4 albedoMetallic = GBufferAlbedoMetallic[input.position.xy];
	float4 normalOcclusionRoughness = GBufferNormalOcclusionRoughness[input.position.xy];
	float eyeDepth = LinearEyeDepth(depth);
	float3 viewPosition = eyeDepth * input.worldDirection;
	float3 normal = normalize(normalOcclusionRoughness.xyz * 2.0 - 1.0);
	return GetLuminanceAndFog(float4(albedoMetallic.rgb, 1.0), 0.0, normal, input.position.xy, viewPosition).rgb;
}
