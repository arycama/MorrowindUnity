// VS builds a per-vertex 3×2 bump basis into oT2/oT3 (3-component!) — exactly what texm3x2pad t2 / texm3x2tex t3 in the PS consume

float4x4 WorldViewProj : register(c2); // c2-c5
float4 ViewPos : register(c1); // used as c1 (fresnel bias, .x=1.0 typically)
float4 EyePos : register(c6); // world-space eye/camera position
float4 Consts30 : register(c30); // .x,.y = small numeric constants (e.g. 0, 1)
float4 FogParams : register(c54); // fog range/scale/bias
float2 UVScale : register(c53);

struct VS_OUTPUT
{
	float4 Pos : POSITION;
	float Fog : FOG;
	float3 Tex0 : TEXCOORD0;
	float3 Tex2 : TEXCOORD2; // bump-basis row 0
	float3 Tex3 : TEXCOORD3; // bump-basis row 1
	float4 Color0 : COLOR0; // fresnel term (oD0)
};

VS_OUTPUT main(float4 Pos : POSITION, float2 BaseUV : TEXCOORD0)
{
	VS_OUTPUT OUT;
	OUT.Pos = mul(Pos, WorldViewProj);

    // fog
	float r5 = dot(Pos, WorldViewProj[2]) - FogParams.x; // dp4 against c4 (Z row)
	OUT.Fog = mad(-r5, FogParams.z, FogParams.w);

    // view vector (eye - vertex), and its negation for a second use
	float3 viewVec = EyePos.xyz - Pos.xyz; // r4 = -v0 + c6
	float3 r9 = viewVec;

	float3 r4 = viewVec;
	r4.z = Consts30.x;
	r4 = normalize(r4);

	float3 r6 = r4;
	r6.z = Consts30.y;

	float3 r5v = Consts30.xxz; // c30.xxzx (only xyz matter here)
	float3 r8 = r5v.yzx * r4.zxy - r4.yzx * r5v.zxy; // cross-product-like term
	r8.z = Consts30.y;

	float3 r7 = r6;
	r7.y = r8.x;
	r8.x = r6.y;

	float3 nrmView = normalize(r9);
	float nz = max(nrmView.z, -nrmView.z);
	float f = ViewPos.x - nz;
	f = f * f * Consts30.y;

	r7.z = mad(f, r4.x, r7.z);
	r8.z = mad(f, r4.y, r8.z);

	OUT.Tex2 = float3(r7.xy * UVScale, r7.z);
	OUT.Tex3 = float3(r8.xy * UVScale, r8.z);

	OUT.Color0 = mad(nrmView.zzzz, Consts30.y, Consts30.y);
	OUT.Tex0 = float3(BaseUV, 0);
	return OUT;
}