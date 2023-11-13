Texture2D _MainTex;
SamplerState _LinearClampSampler;
float4 _ScreenParams;
float2 _Resolution;
float FFTBloomThreshold;

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
	float2 uv = position.xy / _Resolution;
    //uv.y = 1.0 - uv.y;
    float aspect = _ScreenParams.x / _ScreenParams.y;
    if(_ScreenParams.x > _ScreenParams.y)
    {
        //uv.y *= aspect;
    }
    else
    {
        //uv.x /= aspect;
    }
    if(uv.y > 1.0) return float4(0, 0, 0, 0);

    float3 color = _MainTex.Sample(_LinearClampSampler, uv).rgb;
    float luma = Luma(color);
    float scale = saturate(luma - FFTBloomThreshold);
    float3 finalColor = color * scale;
	return _MainTex.Sample(_LinearClampSampler, uv);
}
