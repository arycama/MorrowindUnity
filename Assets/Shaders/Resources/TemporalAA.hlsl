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

	#if 1
	float3 result = _History[uv + float2(f0.x, -1.0)] * (0.5 * w0.x * w0.y + s0.x * w0.y + 0.5 * w3.x * w0.y);
	result += _History[uv + float2(-1.0, f0.y)] * (0.5 * w3.x * w0.y + 0.5 * w0.x * w0.y + w0.x * s0.y + 0.5 * w0.x * w3.y);
	result += _History[uv + float2(f0.x, f0.y)] * s0.x * s0.y;
	result += _History[uv + float2(2.0, f0.y)] * (w3.x * s0.y + 0.5 * w3.x * w3.y);
	return result + _History[uv + float2(f0.x, 2.0)] * (0.5 * w0.x * w3.y + s0.x * w3.y + 0.5 * w3.x * w3.y);
	#else
	float3 A = _History[uv + float2(f0.x, -1.0)];
	float3 B = _History[uv + float2(-1.0, f0.y)];
	float3 C = _History[uv + float2(f0.x, f0.y)];
	float3 D = _History[uv + float2(2.0, f0.y)];
	float3 E = _History[uv + float2(f0.x, 2.0)];
	
	return
	(0.5 * (A + B) * w0.x + A * s0.x + 0.5 * (A + B) * w3.x) * w0.y +
	(B * w0.x + C * s0.x + D * w3.x) * s0.y +
	(0.5 * (B + E) * w0.x + E * s0.x + 0.5 * (D + E) * w3.x) * w3.y;
	#endif
}

float3 Fragment(float4 positionCS : SV_Position) : SV_Target
{
	float3 result = 0.0, mean = 0.0, stdDev = 0.0;
	float2 longestMotion = 0.0;
	float motionLengthSqr = 0.0, weightSum = 0.0;
    
    [unroll]
	for (int y = -1; y <= 1; y++)
	{
        [unroll]
		for (int x = -1; x <= 1; x++)
		{
			int2 coord = positionCS.xy + int2(x, y);
			int2 clampedCoord = clamp(coord, 0, _ScreenParams.xy - 1);
			float3 color = _Input[clampedCoord];
			
			mean += color;
			stdDev += color * color;
			
			float2 delta = int2(x, y) - _Jitter;
			float weight = exp(-2.29 * dot(delta, delta));
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
	stdDev = sqrt(stdDev / 9.0 - mean * mean);
	
	float3 history = BicubicSampling5(positionCS.xy - 0.5 - longestMotion * _ScreenParams.xy);
	
	float3 invDir = rcp(result - history);
	float3 t0 = (mean - stdDev - history) * invDir;
	float3 t1 = (mean + stdDev - history) * invDir;
	float t = saturate(Max3(min(t0, t1)));
	history = lerp(history, result, t);

	float motionLength = length(longestMotion);
	float weight = lerp(_FinalBlendParameters.x, _FinalBlendParameters.y, saturate(motionLength * _FinalBlendParameters.z));
	return lerp(result, history, weight);
}
