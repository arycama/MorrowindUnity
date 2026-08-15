#include "Assets/Shaders/Common.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/CommonShaders.hlsl"

Texture2D<float> ScreenSpaceOcclusion;
Texture2D<float3> ScreenSpaceDiffuse;

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
	float3 viewPosition = eyeDepth * input.worldDirection;
	float3 V = normalize(-viewPosition);
	
	float3 N = PyramidUvToNormal(normalOcclusionRoughness.xy);
	N = -FromToRotationZ(-V, N, false);
	
	float3 result = GetLuminanceAndFog(float4(albedoMetallic.rgb, 1.0), 0.0, N, input.position.xy, viewPosition).rgb;
	
	#ifdef RAYTRACING_ON
		float occlusion = ScreenSpaceOcclusion[input.position.xy];
		result += ScreenSpaceDiffuse[input.position.xy] * albedoMetallic.rgb;
	#endif
	
	return float4(result, 0);
}
