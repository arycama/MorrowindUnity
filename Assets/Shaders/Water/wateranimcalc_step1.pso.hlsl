sampler2D Tex0 : register(s0);
sampler2D Tex1 : register(s1);
sampler2D Tex2 : register(s2);
sampler2D Tex3 : register(s3);

float4 BlendFactor : register(c2); // set from the app; only .z ultimately affects output

struct PS_INPUT
{
	float2 Tex0 : TEXCOORD0;
	float2 Tex1 : TEXCOORD1;
	float2 Tex2 : TEXCOORD2;
	float2 Tex3 : TEXCOORD3;
};

float4 main(PS_INPUT IN) : COLOR0
{
	float4 t0 = tex2D(Tex0, IN.Tex0);
	float4 t1 = tex2D(Tex1, IN.Tex1); // Only rgb used
	float4 t2 = tex2D(Tex2, IN.Tex2); // Only a used
	float4 t3 = tex2D(Tex3, IN.Tex3); // Only a used

	const float4 c3 = float4(0.5, 0.0, 0.0, 0.0);
	const float4 c4 = float4(0.0, 1.0, 1.0, 1.0);
	const float4 c5 = float4(1.0, 0.0, 0.0, 0.0);
	const float4 c6 = float4(0.0, 0.0, 1.0, 0.0);
	const float4 c7 = float4(0.5, 0.5, 0.5, 0.5);

	float4 r0;
	r0.xyz = t1.xyz - t0.xyz;
	r0.w = t2.w - t0.w;

	float4 r1;
	r1.xyz = c7.xyz - t0.xyz;
	r1.w = t3.w - t0.w;

	r1.xyz = r1.xyz * BlendFactor.xyz + r0.xyz;
	r1.w = r1.w + r0.w;

	r1 = r1 + r1.wwww;

	float s = dot(r1.xyz, c6.xyz); // c6 = (0,0,1,0) -> just picks r1.z
	r1 = float4(s, s, s, s);

	r1 = r1 * c5 + c3; // -> (s + 0.5, 0, 0, 0)

	r0 = t0 * c4 + r1; // -> (s+0.5, t0.g, t0.b, t0.a)

	return r0;
}