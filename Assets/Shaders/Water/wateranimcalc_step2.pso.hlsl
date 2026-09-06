// Only emits 1 texcoord (oT0) + a diffuse scalar (oD0); step2 only samples t0/t1 (2nd stage likely reuses the same generated coord via texture-stage state)

sampler2D Tex0 : register(s0);
sampler2D Tex1 : register(s1);
float4 c0 : register(c0); // blend factor, app-set
float4 c1 : register(c1); // app-set
float4 c4 : register(c4); // app-set

float4 main(float2 uv0 : TEXCOORD0, float2 uv1 : TEXCOORD1) : COLOR0
{
	float4 t0 = tex2D(Tex0, uv0);
	float4 t1 = tex2D(Tex1, uv1);

	const float4 c5 = float4(0.0, 0.5, 0.0, 0.0);
	const float4 c6 = float4(0.0, 1.0, 0.0, 0.0);
	const float4 c7 = float4(1.0, 0.0, 0.0, 0.0);

	float4 r0;
	r0.xyz = t0.xyz - 0.5; // _bias
	r0.w = t1.x - t0.x; // sub r0.w, t1, t0 (scalar op on .w channel per encoding, uses default .x src component since no swizzle given -> actually replicated x by ps.1.x default read rule)

	float4 t1v = r0 + r0.w;
	float s1 = dot(t1v.xyz, c7.xyz); // isolates .x after this dp3-with-(1,0,0)
	float4 r1 = mad(s1, c0, t0 - 0.5); // mad r1, t1, c0, t0_bias
	float s2 = dot(r1.xyz, c6.xyz); // isolates .y
	float4 r0b = mad(s2, c1, t0);
	float4 t1w = mad(s2, c6, c5);
	r0b = mad(r0b, c4, t1w);
	return r0b;
}