float WrapUV(float u)
{
    if(u < 0 && u > -0.5)
    {
        return u + 0.5;
    }
    if(u > 0.5 && u < 1)
    {
        return u - 0.5;
    }
    return -1;
}

SamplerState _LinearClampSampler, _LinearRepeatSampler;
Texture2D _MainTex;

float2 _Resolution;
float4 FFTBloomKernelGenParam;
float4 FFTBloomKernelGenParam1;

float Luma(float3 color)
{
    return dot(color, float3(0.299f, 0.587f, 0.114f));
}

float4 Vertex(uint id : SV_VertexID) : SV_Position
{
	float2 uv = float2((id << 1) & 2, id & 2);
	return float4(uv * 2.0 - 1.0, 1.0, 1.0);
}

float4 Fragment(float4 position : SV_Position) : SV_Target
{
    float2 offset = FFTBloomKernelGenParam.xy;
    float2 scale = FFTBloomKernelGenParam.zw;
    float KernelDistanceExp = FFTBloomKernelGenParam1.x;
    float KernelDistanceExpClampMin = FFTBloomKernelGenParam1.y;
    float KernelDistanceExpScale = FFTBloomKernelGenParam1.z;
    bool bUseLuminance = FFTBloomKernelGenParam1.w > 0.0f;

    // 用来缩放那些不是 hdr 格式的滤波盒
	float dis = (1.0 - length(position.xy / _Resolution - float2(0.5, 0.5)));
    float kernelScale = max(pow(dis, KernelDistanceExp) * KernelDistanceExpScale, KernelDistanceExpClampMin); 

	float2 uv = position.xy / _Resolution;
	uv.y = 1.0 - uv.y;
    
	float2 xy = uv * 2 - 1;
    xy /= scale;
    xy = xy * 0.5 + 0.5;

    float2 uv1 = xy - 0.5;
    uv1.x = WrapUV(uv1.x);
    uv1.y = WrapUV(uv1.y);

	//return float4(_MainTex.Sample(_LinearRepeatSampler, uv - 0.5).rgb, 0.0);
    if(bUseLuminance)
    {
        float lum = Luma(_MainTex.Sample(_LinearClampSampler, xy).rgb);
        return float4(float3(lum, lum, lum) * kernelScale, 0.0);
    }
	return float4(_MainTex.Sample(_LinearClampSampler, xy).rgb * kernelScale, 0.0);
}
