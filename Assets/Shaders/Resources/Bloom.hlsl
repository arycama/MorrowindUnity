#include "../BloomCommon.hlsl"
#include "../Common.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/CommonShaders.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Color.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Material.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Samplers.hlsl"

Texture2D<float3> Bloom;
float4 InputScaleLimit;
float2 RcpResolution;
float Strength;

float3 SampleInput(float2 uv, float2 offset)
{
	#ifdef FIRST
		return CameraColor.SampleLevel(LinearClampSampler, uv + offset * RcpResolution, 0.0);
	#else
		return Bloom.SampleLevel(LinearClampSampler, uv + offset * RcpResolution, 0.0);
	#endif
}

float KarisAverage(float3 col)
{
    // Formula is 1 / (1 + luma)
	float luma = Rec2020Luminance(col) * 0.25f;
	return 1.0f / (1.0f + luma);
}

float3 FragmentDownsample(VertexFullscreenTriangleMinimalOutput input) : SV_Target
{
	float3 a = SampleInput(input.uv, float2(-2.0, 2.0));
	float3 b = SampleInput(input.uv, float2(0.0, 2.0));
	float3 c = SampleInput(input.uv, float2(2.0, 2.0));

	float3 d = SampleInput(input.uv, float2(-2.0, 0.0));
	float3 e = SampleInput(input.uv, float2(0.0, 0.0));
	float3 f = SampleInput(input.uv, float2(2.0, 0.0));

	float3 g = SampleInput(input.uv, float2(-2.0, -2.0));
	float3 h = SampleInput(input.uv, float2(0.0, -2.0));
	float3 i = SampleInput(input.uv, float2(2.0, -2.0));

	float3 j = SampleInput(input.uv, float2(-1.0, 1.0));
	float3 k = SampleInput(input.uv, float2(1.0, 1.0));
	float3 l = SampleInput(input.uv, float2(-1.0, -1.0));
	float3 m = SampleInput(input.uv, float2(1.0, -1.0));
	
	float3 color;
	
	#ifdef FIRST
		// We are writing to mip 0, so we need to apply Karis average to each block
		// of 4 samples to prevent fireflies (very bright subpixels, leads to pulsating
		// artifacts).
		float3 groups[5];
		groups[0] = (a + b + d + e) * (0.125f / 4.0f);
		groups[1] = (b + c + e + f) * (0.125f / 4.0f);
		groups[2] = (d + e + g + h) * (0.125f / 4.0f);
		groups[3] = (e + f + h + i) * (0.125f / 4.0f);
		groups[4] = (j + k + l + m) * (0.5f / 4.0f);
		//groups[0] *= KarisAverage(groups[0]);
		//groups[1] *= KarisAverage(groups[1]);
		//groups[2] *= KarisAverage(groups[2]);
		//groups[3] *= KarisAverage(groups[3]);
		//groups[4] *= KarisAverage(groups[4]);
		color = groups[0] + groups[1] + groups[2] + groups[3] + groups[4];
	#else
		color = e * 0.125;
		color += (a + c + g + i) * 0.03125;
		color += (b + d + f + h) * 0.0625;
		color += (j + k + l + m) * 0.125;
	#endif
	
	return color;
}

float4 FragmentUpsample(VertexFullscreenTriangleMinimalOutput input) : SV_Target
{
	return float4(SampleBloom(input.uv, Bloom, RcpResolution, InputScaleLimit), Strength);
}