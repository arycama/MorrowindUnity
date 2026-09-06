float4x4 WorldViewProj : register(c2); // c2-c5
float4 ScaleVec : register(c30);
float4 OffsetVec : register(c35);
float4 UVScale : register(c36);

struct VetexInput
{
	float4 position : POSITION;
	float4 uv : TEXCOORD;
};

struct FragmentInput
{
	float4 Pos : POSITION;
	float4 Tex0 : TEXCOORD0;
	float4 Color0 : COLOR0; // oD0
};

FragmentInput Vertex(VetexInput input)
{
	FragmentInput output;
	float4 r0 = input.position * UVScale;
	output.Pos = mul(r0, WorldViewProj);

	float4 r1 = input.position;
	r1.y = ScaleVec.z - r1.y;
	output.Tex0 = r1 - OffsetVec;

	output.Color0 = input.uv.z; // oD0 = v1.z, broadcast
	return output;
}