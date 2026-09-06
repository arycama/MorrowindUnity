// Generates 4 scrolling/offset UV sets (oT0-oT3) via relative-addressed constants (c8/c13/c18/c23[a0.x]) → feeds the 4-texture height-diff blend from your last message

float4x4 WorldViewProj : register(c2); // c2-c5
float4 FrameIndex : register(c50); // .x used as array index
float4 FrameOffsets[20] : register(c8); // c8..c27, stride-5 slots per "frame"

struct VS_OUTPUT
{
	float4 Pos : POSITION;
	float4 Tex0 : TEXCOORD0;
	float4 Tex1 : TEXCOORD1;
	float4 Tex2 : TEXCOORD2;
	float4 Tex3 : TEXCOORD3;
};

VS_OUTPUT main(float4 Pos : POSITION, float4 Tex : TEXCOORD0)
{
	VS_OUTPUT OUT;
	OUT.Pos = mul(Pos, WorldViewProj);

	int idx = (int) FrameIndex.x;
	OUT.Tex0 = FrameOffsets[idx + 0] + Tex;
	OUT.Tex1 = FrameOffsets[idx + 1] + Tex; // c13 = c8 + 5
	OUT.Tex2 = FrameOffsets[idx + 2] + Tex; // c18 = c8 + 10
	OUT.Tex3 = FrameOffsets[idx + 3] + Tex; // c23 = c8 + 15
	return OUT;
}