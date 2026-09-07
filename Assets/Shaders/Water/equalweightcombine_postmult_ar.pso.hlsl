// Generic 4-texture equal-weight combine, likely a compositing/downsample utility used somewhere in the same pipeline

sampler2D Tex0 : register(s0);
sampler2D Tex1 : register(s1);
sampler2D Tex2 : register(s2);
sampler2D Tex3 : register(s3);

float4 main(float2 uv0 : TEXCOORD0, float2 uv1 : TEXCOORD1, float2 uv2 : TEXCOORD2, float2 uv3 : TEXCOORD3) : COLOR0
{
	float3 t0 = tex2D(Tex0, uv0).xyz - 0.5;
	float3 t1 = tex2D(Tex1, uv1).xyz - 0.5;
	float3 t2 = tex2D(Tex2, uv2).xyz - 0.5;
	float3 t3 = tex2D(Tex3, uv3).xyz - 0.5;
	return float3(t0 + t1 + t2 + t3 + 0.5).xyzz;
}