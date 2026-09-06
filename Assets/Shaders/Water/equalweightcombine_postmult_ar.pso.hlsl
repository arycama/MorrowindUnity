// Generic 4-texture equal-weight combine, likely a compositing/downsample utility used somewhere in the same pipeline

sampler2D Tex0 : register(s0);
sampler2D Tex1 : register(s1);
sampler2D Tex2 : register(s2);
sampler2D Tex3 : register(s3);

float4 main(float2 uv0 : TEXCOORD0, float2 uv1 : TEXCOORD1, float2 uv2 : TEXCOORD2, float2 uv3 : TEXCOORD3) : COLOR0
{
	float4 t0 = tex2D(Tex0, uv0) - 0.5;
	float4 t1 = tex2D(Tex1, uv1) - 0.5;
	float4 t2 = tex2D(Tex2, uv2) - 0.5;
	float4 t3 = tex2D(Tex3, uv3) - 0.5;

	float4 r0 = t0 + t1;
	float4 r1 = t2 + t3;
	r1 = r0 + r1;
	r0 = r1 + float4(0.5, 0.5, 0.5, 0.5);

	float s = dot(r0.xyz, float3(0, 0, 1)); // extracts .z
	r0.w = s;
	return r0;
}