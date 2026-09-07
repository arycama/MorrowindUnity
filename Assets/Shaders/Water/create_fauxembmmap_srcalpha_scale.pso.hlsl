// Reads 4 plain textures, no texm3x2* ops — this is a prepass that bakes the bump-offset map itself (from source-alpha channels) that fauxembm_displace_2.pso's t0 later samples

sampler2D Tex0 : register(s0);
sampler2D Tex1 : register(s1);
sampler2D Tex2 : register(s2);
sampler2D Tex3 : register(s3);
float4 c5 : register(c5); // app-set scale
float4 c6 : register(c6); // app-set scale

float4 main(float2 uv0 : TEXCOORD0, float2 uv1 : TEXCOORD1, float2 uv2 : TEXCOORD2, float2 uv3 : TEXCOORD3) : COLOR0
{
	float t0 = tex2D(Tex0, uv0).a;
	float t1 = tex2D(Tex1, uv1).a;
	float4 r0 = (t0 - t1) * float4(c5.xyz, 1.0);
	
	float t2 = tex2D(Tex2, uv2).a;
	float t3 = tex2D(Tex3, uv3).a;
	r0 += (t3 - t2) * c6;
	
	r0 *= 2.0;
	r0 += float4(0.5, 0.5, 1.0, 0);
	return r0;
}