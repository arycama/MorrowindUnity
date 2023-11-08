#include "../Common.hlsl"

Texture2D<float3> _Input, _History;
Texture2D<float2> _Motion;

float4 _FinalBlendParameters; // x: static, y: dynamic, z: motion amplification
float _Sharpness, _HasHistory;

float4 Vertex(uint id : SV_VertexID) : SV_Position
{
	float2 uv = float2((id << 1) & 2, id & 2);
	return float4(uv * 2.0 - 1.0, 1.0, 1.0);
}

// From Filmic SMAA presentation[Jimenez 2016]
// A bit more verbose that it needs to be, but makes it a bit better at latency hiding
float3 HistoryBicubic5Tap(float2 UV)
{
    float2 samplePos = UV;
    float2 tc1 = floor(samplePos - 0.5) + 0.5;
    float2 f = samplePos - tc1;
    float2 f2 = f * f;
    float2 f3 = f * f2;

    const float c = 0.125;

    float2 w0 = -c         * f3 +  2.0 * c         * f2 - c * f;
    float2 w1 =  (2.0 - c) * f3 - (3.0 - c)        * f2          + 1.0;
    float2 w2 = -(2.0 - c) * f3 + (3.0 - 2.0 * c)  * f2 + c * f;
    float2 w3 = c          * f3 - c                * f2;

    float2 w12 = w1 + w2;
	float2 tc0 = rcp(_ScreenParams.xy) * (tc1 - 1.0);
	float2 tc3 = rcp(_ScreenParams.xy) * (tc1 + 2.0);
	float2 tc12 = rcp(_ScreenParams.xy) * (tc1 + w2 / w12);

    float3 s0 = _History.Sample(_LinearClampSampler, float2(tc12.x, tc0.y));
    float3 s1 = _History.Sample(_LinearClampSampler, float2(tc0.x, tc12.y));
    float3 s2 = _History.Sample(_LinearClampSampler, float2(tc12.x, tc12.y));
    float3 s3 = _History.Sample(_LinearClampSampler, float2(tc3.x, tc0.y));
    float3 s4 = _History.Sample(_LinearClampSampler, float2(tc12.x, tc3.y));

    float cw0 = (w12.x * w0.y);
    float cw1 = (w0.x * w12.y);
    float cw2 = (w12.x * w12.y);
    float cw3 = (w3.x * w12.y);
    float cw4 = (w12.x *  w3.y);

    s0 *= cw0;
    s1 *= cw1;
    s2 *= cw2;
    s3 *= cw3;
    s4 *= cw4;

    float3 historyFiltered = s0 + s1 + s2 + s3 + s4;
    float weightSum = cw0 + cw1 + cw2 + cw3 + cw4;

    float3 filteredVal = historyFiltered * rcp(weightSum);
    return filteredVal;
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
	
	//if (!_HasHistory)
	//	return result;
	
	float3 history = HistoryBicubic5Tap(positionCS.xy - longestMotion * _ScreenParams.xy);
	//history = _History.Sample(_LinearClampSampler, positionCS.xy / _ScreenParams.xy - longestMotion);
	
	float3 invDir = rcp(result - history);
	float3 t0 = (mean - stdDev - history) * invDir;
	float3 t1 = (mean + stdDev - history) * invDir;
	float t = saturate(Max3(min(t0, t1)));
	history = lerp(history, result, t);

	float motionLength = length(longestMotion);
	float weight = lerp(_FinalBlendParameters.x, _FinalBlendParameters.y, saturate(motionLength * _FinalBlendParameters.z));
	return lerp(result, history, weight);
}
