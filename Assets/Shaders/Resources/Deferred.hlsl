#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/GBuffer.hlsl"
#include "Assets/Shaders/Common.hlsl"

Texture2D<float> ScreenSpaceOcclusion;
Texture2D<float3> ScreenSpaceDiffuse;

#ifdef MSAA_ON
	Texture2DMS<float4, 8> _UnityFBInput0;
	Texture2DMS<float4, 8> _UnityFBInput1;
#else
	Texture2D<float4> _UnityFBInput0;
	Texture2D<float4> _UnityFBInput1;
#endif

struct FragmentInput
{
	float4 position : SV_Position;
	float3 worldDirection : TEXCOORD;
	
	#ifdef MSAA_ON
		#ifdef SHADER_STAGE_FRAGMENT
			uint sampleIndex : SV_SampleIndex;
		#else
			uint sampleIndex : TEXCOORD1;
		#endif
	#endif
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
		float depth = _UnityFBInput0.Load(input.position.xy, input.sampleIndex).r;
		float4 albedoNormal = _UnityFBInput1.Load(input.position.xy, input.sampleIndex);
	#else
		float depth = _UnityFBInput0[input.position.xy].r;
		float4 albedoNormal = _UnityFBInput1[input.position.xy];
	#endif

	float eyeDepth = LinearEyeDepth(depth);
	float3 viewPosition = eyeDepth * input.worldDirection;
	float3 V = normalize(-viewPosition);
	
	float3 albedo = UnpackAlbedo(albedoNormal.rg, input.position.xy);
	float3 N = PyramidUvToNormal(albedoNormal.ba);
	N = -FromToRotationZ(-V, N, false);
	
	float3 result = GetLuminanceAndFog(float4(albedo, 1.0), 0.0, N, input.position.xy, viewPosition).rgb;
	
	#ifdef RAYTRACING_ON
		//float occlusion = ScreenSpaceOcclusion[input.position.xy];
		//result += ScreenSpaceDiffuse[input.position.xy] * albedoMetallic.rgb;
	#endif
	
	return float4(result, FogFactor(viewPosition));
}
