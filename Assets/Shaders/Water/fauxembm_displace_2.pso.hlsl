sampler2D BumpMap : register(s0); // t0 - bump/normal source
sampler2D EnvMap : register(s3); // t3 - sampled with perturbed UV

float4 BumpBasisRow : register(c2); // multiplies bump vector
float4 EnvTint : register(c4);
float4 FresnelBias : register(c1); // 0.5,0.5,0.5,0.5
float4 WaterColor : register(c6); // lerp target
float4 AlphaBias : register(c7);

struct PS_INPUT
{
	float2 Tex0 : TEXCOORD0;
	float3 Tex2 : TEXCOORD2; // bump basis row 0 (from fauxembm_displace.vso)
	float3 Tex3 : TEXCOORD3; // bump basis row 1
	float4 Color0 : COLOR0; // v0 - fresnel term from VS
};

float4 main(PS_INPUT IN) : COLOR0
{
	float4 bump = tex2D(BumpMap, IN.Tex0);
	float3 bumpVec = bump.xyz * 2.0 - 1.0; // _bx2

    // texm3x2pad / texm3x2tex: perturb UV via two dot products, then sample stage3
	float2 perturbedUV;
	perturbedUV.x = dot(IN.Tex2, bumpVec);
	perturbedUV.y = dot(IN.Tex3, bumpVec);
	float4 env = tex2D(EnvMap, perturbedUV);

	float4 t1 = BumpBasisRow * float4(bumpVec, bump.w * 2 - 1);
	float3 fresnelIn = IN.Color0.xyz * 2.0 - 1.0; // v0_bx2
	float r1w = dot(t1.xyz, fresnelIn);

	float3 r0 = env.xyz * EnvTint.xyz;
	float r0w = mad(r1w, FresnelBias.w, FresnelBias.w);

	float3 r1 = fresnelIn;
	r0 = lerp(WaterColor.xyz, r0, r1w); // lrp r0.xyz, r1.w, c6, r0  (note operand order)
	r0w = r0w * r0w;
	r0w = r0w - AlphaBias.w;

	return float4(r0, r0w);
}