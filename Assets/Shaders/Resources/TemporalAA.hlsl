#include "../Common.hlsl"

Texture2D<float3> _Input, _History;
Texture2D<float2> _Motion;
Texture2D<float> _Depth;

float4 _FinalBlendParameters; // x: static, y: dynamic, z: motion amplification
float _Sharpness;

#define HALF_MAX_MINUS1 65472.0 // (2 - 2^-9) * 2^15

float4 Vertex(uint id : SV_VertexID) : SV_Position
{
	float2 uv = float2((id << 1) & 2, id & 2);
	return float4(uv * 2.0 - 1.0, 1.0, 1.0);
}

// 5-tap bicubic sampling - taken from MiniEngine by MSFT, few things changed (minor) to fit my approach
float3 BicubicSampling5(float2 uv)
{
	float2 fractional = frac(uv);

    // 5-tap bicubic sampling (for Hermite/Carmull-Rom filter) -- (approximate from original 16->9-tap bilinear fetching) 
	float2 t = fractional;
	float2 t2 = fractional * fractional;
	float2 t3 = fractional * fractional * fractional;
	float s = 0.5;
	float2 w0 = -s * t3 + 2.0 * s * t2 - s * t;
	float2 w1 = (2.0 - s) * t3 + (s - 3.0) * t2 + 1.0;
	float2 w2 = (s - 2.0) * t3 + (3 - 2.0 * s) * t2 + s * t;
	float2 w3 = s * t3 - s * t2;
	float2 s0 = w1 + w2;
	float2 f0 = w2 / (w1 + w2);

	float3 A = _History[uv + float2(f0.x, -1.0)];
	float3 B = _History[uv + float2(-1.0, f0.y)];
	float3 C = _History[uv + float2(f0.x, f0.y)];
	float3 D = _History[uv + float2(2.0, f0.y)];
	float3 E = _History[uv + float2(f0.x, 2.0)];
	
	return 
	(0.5 * (A + B) * w0.x + A * s0.x + 0.5 * (A + B) * w3.x) * w0.y + 
	(B * w0.x + C * s0.x + D * w3.x) * s0.y + 
	(0.5 * (B + E) * w0.x + E * s0.x + 0.5 * (D + E) * w3.x) * w3.y;
}

 float3 ClipToAABB(float3 history, float3 center, float3 extents, float3 color)
{
	#if 1
	float3 rayDir = color - history;
	float3 rayPos = history - center;

	float3 invDir = rcp(rayDir);
	float3 t0 = (extents - rayPos) * invDir;
	float3 t1 = -(extents + rayPos) * invDir;

	float AABBIntersection = max(max(min(t0.x, t1.x), min(t0.y, t1.y)), min(t0.z, t1.z));
	return lerp(history, center, saturate(AABBIntersection));
	#else
    // This is actually `distance`, however the keyword is reserved
	float3 offset = history - center;

    float3 ts = abs(extents / (offset + 0.0001));
    float t = saturate(Min3(ts));
	return center + offset * t;
	#endif
}

float Luminance(float3 linearRgb)
{
	return dot(linearRgb, float3(0.2126729, 0.7151522, 0.0721750));
}

float3 RGBToYCoCg(float3 RGB)
{
	return mul(float3x3(0.25, 0.5, 0.25, 0.5, 0, -0.5, -0.25, 0.5, -0.25), RGB);
}
    
float3 YCoCgToRGB(float3 YCoCg)
{
	return mul(float3x3(1, 1, -1, 1, 0, 1, 1, -1, -1), YCoCg);
}

float3 Fragment(float4 positionCS : SV_Position) : SV_Target
{
	float3 input = RGBToYCoCg(_Input[positionCS.xy]);
	input *= rcp(1.0 + input.r);
	
	float2 longestMotion = _Motion[positionCS.xy];
	float motionLengthSqr = dot(longestMotion, longestMotion), weightSum = exp(-2.29 * dot(_Jitter, _Jitter));
	float3 result = input * weightSum, minValue = input, maxValue = input, mean = input, stdDev = input * input;
    
    [unroll]
	for (int y = -1; y <= 1; y++)
	{
        [unroll]
		for (int x = -1; x <= 1; x++)
		{
			if(x == 0 && y == 0)
				continue;
			
			int2 coord = positionCS.xy + int2(x, y);
			int2 clampedCoord = clamp(coord, 0, _ScreenParams.xy - 1);
			float3 color = RGBToYCoCg(_Input[clampedCoord]);
			color *= rcp(1.0 + color.r);
			
			minValue = min(minValue, color);
			maxValue = max(maxValue, color);
			
			mean += color;
			stdDev += color * color;
			
			float2 delta = int2(x, y) - _Jitter;
			float2 weights = max(0.0, 1.0 - abs(delta));
			float weight = weights.x * weights.y;
			weight = exp(-2.29 * dot(delta, delta));
			
			result += color * weight;
			weightSum += weight;
			
			float2 motion = _Motion[clampedCoord];
			if (dot(motion, motion) > motionLengthSqr)
			{
				longestMotion = motion;
				motionLengthSqr = dot(motion, motion);
			}
		}
	}
	
	result /= weightSum;
	
	mean /= 9.0;
	stdDev = sqrt(abs(stdDev / 9.0 - mean * mean));
	
	float3 center = mean;
	float3 extents = stdDev;
	
	minValue = max(minValue, mean - stdDev);
	maxValue = min(maxValue, mean + stdDev);
	
	center = 0.5 * (maxValue + minValue);
	extents = 0.5 * (maxValue - minValue);
	
	float3 history = RGBToYCoCg(BicubicSampling5(positionCS.xy - 0.5 - longestMotion * _ScreenParams.xy));
	history *= rcp(1.0 + history.r);

    // Clip history samples
	float motionLength = length(longestMotion);
	history = ClipToAABB(history, center, extents, result);

	float weight = lerp(_FinalBlendParameters.x, _FinalBlendParameters.y, saturate(motionLength * _FinalBlendParameters.z));

	result = lerp(result, history, weight);
	result *= rcp(1.0 - result.r);
	
	return YCoCgToRGB(result);
}
