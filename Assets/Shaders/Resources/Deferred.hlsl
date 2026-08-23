#include "Assets/Shaders/Common.hlsl"

Texture2D<float> ScreenSpaceOcclusion;
Texture2D<float3> ScreenSpaceDiffuse;

struct FragmentInput
{
	float4 position : SV_Position;
	float3 worldDirection : TEXCOORD;
};

FragmentInput Vertex(uint id : SV_VertexID)
{
	FragmentInput output;
	float2 uv = (id << uint2(0, 1)) & 2;
	output.position = float4(uv * 2.0 - 1.0, 0.0, 1.0);
	output.worldDirection = float3(TanHalfFov * output.position.xy, 1.0);
	return output;
}

float3 Fragment(FragmentInput input) : SV_Target
{
	#ifdef MSAA_ON
		float depth = CameraDepth.Load(input.position.xy, 0);
		float4 albedoMetallic = GBufferAlbedoMetallic.Load(input.position.xy, 0);
		float4 normalOcclusionRoughness = GBufferNormalOcclusionRoughness.Load(input.position.xy, 0);
	#else
		float depth = CameraDepth[input.position.xy];
		float4 albedoMetallic = GBufferAlbedoMetallic[input.position.xy];
		float4 normalOcclusionRoughness = GBufferNormalOcclusionRoughness[input.position.xy];
	#endif
	
	float eyeDepth = LinearEyeDepth(depth);
	float3 viewPosition = eyeDepth * input.worldDirection;
	float3 V = normalize(-viewPosition);
	
	float3 N = PyramidUvToNormal(normalOcclusionRoughness.xy);
	N = -FromToRotationZ(-V, N, false);
	
	float3 result = GetLuminanceAndFog(float4(albedoMetallic.rgb, 1.0), 0.0, N, input.position.xy, viewPosition).rgb;
	
	#ifdef RAYTRACING_ON
		//float occlusion = ScreenSpaceOcclusion[input.position.xy];
		//result += ScreenSpaceDiffuse[input.position.xy] * albedoMetallic.rgb;
	#endif
	
	return result;
}
