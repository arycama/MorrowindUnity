#include "../Common.hlsl"

Texture2D<float3> _MainTex, _Bloom;
float4 _MainTex_TexelSize;
float2 _RcpResolution;
float _Strength;

float4 Vertex(uint id : SV_VertexID) : SV_Position
{
	float2 uv = float2((id << 1) & 2, id & 2);
	return float4(uv * 2.0 - 1.0, 1.0, 1.0);
}

float3 FragmentDownsample(float4 position : SV_Position) : SV_Target
{
	float2 uv = position.xy * _RcpResolution;

	float3 a = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(-2, 2), 0.0);
	float3 b = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(0, 2), 0.0);
	float3 c = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(2, 2), 0.0);

	float3 d = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(-2, 0), 0.0);
	float3 e = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(0, 0), 0.0);
	float3 f = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(2, 0), 0.0);

	float3 g = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(-2, -2), 0.0);
	float3 h = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(0, -2), 0.0);
	float3 i = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(2, -2), 0.0);

	float3 j = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(-1, 1), 0.0);
	float3 k = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(1, 1), 0.0);
	float3 l = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(-1, -1), 0.0);
	float3 m = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(1, -1), 0.0);
	
	float3 color = e * 0.125;
	color += (a + c + g + i) * 0.03125;
	color += (b + d + f + h) * 0.0625;
	color += (j + k + l + m) * 0.125;
	return color;
}

float4 FragmentUpsample(float4 position : SV_Position) : SV_Target
{
	float2 uv = position.xy * _RcpResolution;
	
	float3 a = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(-1, 1), 0.0);
	float3 b = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(0, 1), 0.0);
	float3 c = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(1, 1), 0.0);

	float3 d = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(-1, 0), 0.0);
	float3 e = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(0, 0), 0.0);
	float3 f = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(1, 0), 0.0);

	float3 g = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(-1, -1), 0.0);
	float3 h = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(0, -1), 0.0);
	float3 i = _MainTex.SampleLevel(_LinearClampSampler, uv + _MainTex_TexelSize.xy * float2(1, -1), 0.0);
	
	float3 color = e * 4.0;
	color += (b + d + f + h) * 2.0;
	color += (a + c + g + i);
	color *= 1.0 / 16.0;
	
	return float4(color, _Strength);
}