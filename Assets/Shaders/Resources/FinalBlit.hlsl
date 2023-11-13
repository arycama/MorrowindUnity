SamplerState _LinearClampSampler;
Texture2D<float3> _MainTex, _Input;

float4 _ScreenParams;
float FFTBloomIntensity;

float4 Vertex(uint id : SV_VertexID) : SV_Position
{
	float2 uv = float2((id << 1) & 2, id & 2);
	return float4(uv * 2.0 - 1.0, 1.0, 1.0);
}

float3 Fragment(float4 position : SV_Position) : SV_Target
{
    float aspect = _ScreenParams.x / _ScreenParams.y;
	float2 uv = position.xy / _ScreenParams.xy;
	
	//if(uv.x < 0.5)
		return _Input[position.xy];
	
   // uv.x *= 0.5;
	//return _MainTex.Sample(_LinearClampSampler, uv) * FFTBloomIntensity;
}
