#include "../Common.hlsl"

Texture2D<float3> _Input, _History;
Texture2D<float2> _CameraMotionVectorsTexture;
Texture2D<float> _CameraDepthTexture;

float2 _Jitter;
float4 _FinalBlendParameters; // x: static, y: dynamic, z: motion amplification
float _Sharpness;

#define HALF_MAX_MINUS1 65472.0 // (2 - 2^-9) * 2^15

float4 Vertex(uint id : SV_VertexID) : SV_Position
{
	float2 uv = float2((id << 1) & 2, id & 2);
	return float4(uv * 2.0 - 1.0, 1.0, 1.0);
}

float2 GetClosestFragment(float2 uv)
{
	const float2 k = 1.0 / _ScreenParams.xy;

	const float4 neighborhood = float4(
                _CameraDepthTexture.Sample(_PointClampSampler, uv - k),
                _CameraDepthTexture.Sample(_PointClampSampler, uv + float2(k.x, -k.y)),
                _CameraDepthTexture.Sample(_PointClampSampler, uv + float2(-k.x, k.y)),
                _CameraDepthTexture.Sample(_PointClampSampler, uv + k));

#define COMPARE_DEPTH(a, b) step(b, a)

	float3 result = float3(0.0, 0.0, _CameraDepthTexture.Sample(_PointClampSampler, uv));
	result = lerp(result, float3(-1.0, -1.0, neighborhood.x), COMPARE_DEPTH(neighborhood.x, result.z));
	result = lerp(result, float3(1.0, -1.0, neighborhood.y), COMPARE_DEPTH(neighborhood.y, result.z));
	result = lerp(result, float3(-1.0, 1.0, neighborhood.z), COMPARE_DEPTH(neighborhood.z, result.z));
	result = lerp(result, float3(1.0, 1.0, neighborhood.w), COMPARE_DEPTH(neighborhood.w, result.z));

	return (uv + result.xy * k);
}

 float3 ClipToAABB(float3 color, float3 minimum, float3 maximum)
{
    // Note: only clips towards aabb center (but fast!)
    float3 center = 0.5 * (maximum + minimum);
    float3 extents = 0.5 * (maximum - minimum);

    // This is actually `distance`, however the keyword is reserved
    float3 offset = color.rgb - center;

    float3 ts = abs(extents / (offset + 0.0001));
    float t = saturate(Min3(ts));
	return center + offset * t;
}

float Luminance(float3 linearRgb)
{
	return dot(linearRgb, float3(0.2126729, 0.7151522, 0.0721750));
}

float3 Fragment(float4 positionCS : SV_Position) : SV_Target
{
	float2 texcoord = positionCS.xy / _ScreenParams.xy;
    
	float2 closest = GetClosestFragment(texcoord);
	float2 motion = _CameraMotionVectorsTexture.Sample(_LinearClampSampler, closest);
    
	const float2 k = 1.0 / _ScreenParams.xy;
    float2 uv = texcoord - _Jitter;

	float3 color = _Input.Sample(_LinearClampSampler, uv);

	float3 topLeft = _Input.Sample(_LinearClampSampler, uv - k * 0.5);
	float3 bottomRight = _Input.Sample(_LinearClampSampler, uv + k * 0.5);

    float3 corners = 4.0 * (topLeft + bottomRight) - 2.0 * color;

    // Sharpen output
    color += (color - (corners * 0.166667)) * 2.718282 * _Sharpness;
    color = clamp(color, 0.0, HALF_MAX_MINUS1);

    // Tonemap color and history samples
    float3 average = (corners + color) * 0.142857;

	float3 history = _History.Sample(_LinearClampSampler, texcoord - motion);

    float motionLength = length(motion);
    float2 luma = float2(Luminance(average), Luminance(color));
    //float nudge = 4.0 * abs(luma.x - luma.y);
    float nudge = lerp(4.0, 0.25, saturate(motionLength * 100.0)) * abs(luma.x - luma.y);

    float3 minimum = min(bottomRight, topLeft) - nudge;
    float3 maximum = max(topLeft, bottomRight) + nudge;

    // Clip history samples
    history = ClipToAABB(history, minimum.xyz, maximum.xyz);

    // Blend method
    float weight = clamp(
        lerp(_FinalBlendParameters.x, _FinalBlendParameters.y, motionLength * _FinalBlendParameters.z),
        _FinalBlendParameters.y, _FinalBlendParameters.x
    );

    color = lerp(color, history, weight);
    color = clamp(color, 0.0, HALF_MAX_MINUS1);

	//return _Input[positionCS.xy];
	return color;
}
