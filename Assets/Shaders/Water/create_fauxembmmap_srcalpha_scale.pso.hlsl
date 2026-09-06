// Reads 4 plain textures, no texm3x2* ops — this is a prepass that bakes the bump-offset map itself (from source-alpha channels) that fauxembm_displace_2.pso's t0 later samples

sampler2D Tex0 : register(s0);
sampler2D Tex1 : register(s1);
sampler2D Tex2 : register(s2);
sampler2D Tex3 : register(s3);
float4 c5 : register(c5); // app-set scale
float4 c6 : register(c6); // app-set scale

float4 main(float2 uv0 : TEXCOORD0, float2 uv1 : TEXCOORD1, float2 uv2 : TEXCOORD2, float2 uv3 : TEXCOORD3) : COLOR0
{
	float4 t0 = tex2D(Tex0, uv0);
	float4 t1 = tex2D(Tex1, uv1);
	float4 t2 = tex2D(Tex2, uv2);
	float4 t3 = tex2D(Tex3, uv3);

	const float4 c4 = float4(0, 0, 1, 1);
	const float4 c2 = float4(0.5, 0.5, 0, 0);
	const float4 c1 = float4(1, 1, 0, 0);
	const float4 c3 = float4(0, 0, 1, 0);

	float r0w = t0.w - t1.w; // sub r0.w, t0, t1 -> alpha diff
	float3 t0xyz = r0w * c5.xyz; // mul t0.xyz, r0.w, c5

	float r1w = t3.w - t2.w;
	float4 r0 = mad(r1w, c6, float4(t0xyz, r0w));

	r0 = r0 + r0;
	r0 = r0 + c2;
	r0 = r0 + c3;
	return r0;
}