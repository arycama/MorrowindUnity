#include "../Common.hlsl"

Texture2D<float3> _MainTex, _Bloom;
float _IsSceneView, _BloomStrength;

float4 Vertex(uint id : SV_VertexID) : SV_Position
{
	float2 uv = float2((id << 1) & 2, id & 2);
	return float4(uv * 2.0 - 1.0, 1.0, 1.0);
}

float3 Uncharted2ToneMapping(float3 color)
{
	float A = 0.15;
	float B = 0.50;
	float C = 0.10;
	float D = 0.20;
	float E = 0.02;
	float F = 0.30;
	float W = 11.2;
	float exposure = 2.;
	color *= exposure;
	color = ((color * (A * color + C * B) + D * E) / (color * (A * color + B) + D * F)) - E / F;
	float white = ((W * (A * W + C * B) + D * E) / (W * (A * W + B) + D * F)) - E / F;
	color /= white;
	//color = pow(color, vec3(1. / gamma));
	return color;
}

float3 ACESFilm(float3 x)
{
	float a = 2.51f;
	float b = 0.03f;
	float c = 2.43f;
	float d = 0.59f;
	float e = 0.14f;
	return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

// sRGB => XYZ => D65_2_D60 => AP1 => RRT_SAT
static const float3x3 ACESInputMat =
{
	{ 0.59719, 0.35458, 0.04823 },
	{ 0.07600, 0.90834, 0.01566 },
	{ 0.02840, 0.13383, 0.83777 }
};

// ODT_SAT => XYZ => D60_2_D65 => sRGB
static const float3x3 ACESOutputMat =
{
	{ 1.60475, -0.53108, -0.07367 },
	{ -0.10208, 1.10813, -0.00605 },
	{ -0.00327, -0.07276, 1.07602 }
};

float3 RRTAndODTFit(float3 v)
{
	float3 a = v * (v + 0.0245786f) - 0.000090537f;
	float3 b = v * (0.983729f * v + 0.4329510f) + 0.238081f;
	return a / b;
}

float3 ACESFitted(float3 color)
{
	color = mul(ACESInputMat, color);

    // Apply RRT and ODT
	color = RRTAndODTFit(color);

	color = mul(ACESOutputMat, color);

    // Clamp to [0, 1]
	color = saturate(color);

	return color;
}

half3 SRGBToLinear(half3 c)
{
	half3 linearRGBLo = c / 12.92;
	half3 linearRGBHi = pow((c + 0.055) / 1.055, half3(2.4, 2.4, 2.4));
	half3 linearRGB = (c <= 0.04045) ? linearRGBLo : linearRGBHi;
	return linearRGB;
}

half3 LinearToSRGB(half3 c)
{
    half3 sRGBLo = c * 12.92;
    half3 sRGBHi = (pow(c, half3(1.0 / 2.4, 1.0 / 2.4, 1.0 / 2.4)) * 1.055) - 0.055;
    half3 sRGB = (c <= 0.0031308) ? sRGBLo : sRGBHi;
    return sRGB;
}

float3 Fragment(float4 position : SV_Position) : SV_Target
{
	// Need to flip for game view
	if (!_IsSceneView)
		position.y = _ScreenParams.y - position.y;
	
	float3 input = _MainTex[position.xy];
	float2 uv = position.xy / _ScreenParams.xy;
	
	// Take 9 samples around current texel:
    // a - b - c
    // d - e - f
    // g - h - i
    // === ('e' is the current texel) ===
	float3 a = _Bloom.SampleLevel(_LinearClampSampler, uv, 0.0, int2(-1, 1));
	float3 b = _Bloom.SampleLevel(_LinearClampSampler, uv, 0.0, int2(0, 1));
	float3 c = _Bloom.SampleLevel(_LinearClampSampler, uv, 0.0, int2(1, 1));

	float3 d = _Bloom.SampleLevel(_LinearClampSampler, uv, 0.0, int2(-1, 0));
	float3 e = _Bloom.SampleLevel(_LinearClampSampler, uv, 0.0, int2(0, 0));
	float3 f = _Bloom.SampleLevel(_LinearClampSampler, uv, 0.0, int2(1, 0));

	float3 g = _Bloom.SampleLevel(_LinearClampSampler, uv, 0.0, int2(-1, -1));
	float3 h = _Bloom.SampleLevel(_LinearClampSampler, uv, 0.0, int2(0, -1));
	float3 i = _Bloom.SampleLevel(_LinearClampSampler, uv, 0.0, int2(1, -1));
	
	// Apply weighted distribution, by using a 3x3 tent filter:
    //  1   | 1 2 1 |
    // -- * | 2 4 2 |
    // 16   | 1 2 1 |
	float3 upsample = e * 4.0;
	upsample += (b + d + f + h) * 2.0;
	upsample += (a + c + g + i);
	upsample *= 1.0 / 16.0;
	
	input = lerp(input, upsample, _BloomStrength);
	
	// Reinhard
	//input *= rcp(1.0 + Luminance(input));
	
	input = Uncharted2ToneMapping(input);
	
	//input = SRGBToLinear(ACESFitted(LinearToSRGB(input)));
	
	return input;
}
