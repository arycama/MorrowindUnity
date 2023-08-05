#include "Common.hlsl"

struct VertexInput
{
	float3 position : POSITION;
	float3 normal : NORMAL;
    float3 color : COLOR;
    float4 uv : TEXCOORD;
	uint instanceID : SV_InstanceID;
};

struct FragmentInput
{
	float4 position : SV_Position;
	float3 worldPosition : POSITION1;
	float3 normal : NORMAL;
	float3 color : COLOR;
	float4 uv : TEXCOORD;
};

Texture2DArray<float3> _MainTex;
Texture2D<float> _Control;
float4 _Control_ST, _MainTex_ST, _Control_TexelSize;

const static float Pi = radians(180.0);

FragmentInput Vertex(VertexInput input)
{
    FragmentInput output;
	output.worldPosition = ObjectToWorld(input.position, input.instanceID);
	output.position = WorldToClip(output.worldPosition);
	output.uv = float4(input.uv.xy, input.uv.zw * _MainTex_ST.xy + _MainTex_ST.zw);
	output.color = input.color;
	output.normal = input.normal;
	return output;
}

float4 BilinearWeights(float2 uv)
{
	float4 weights = uv.xxyy * float4(-1, 1, 1, -1) + float4(1, 0, 0, 1);
	return weights.zzww * weights.xyyx;
}

float4 BilinearWeights(float2 uv, float2 textureSize)
{
	const float2 offset = 1.0 / 512.0;
	float2 localUv = frac(uv * textureSize + (-0.5 + offset));
	return BilinearWeights(localUv);
}

// Output:\ weights associated with each hex tile and integer centers
void TriangleGrid(out float w1, out float w2, out float w3, out int2 vertex1, out int2 vertex2, out int2 vertex3, float2 st)
{
	// Scaling of the input
	st *= 2.0 * sqrt(3.0);

	// Skew input space into simplex triangle grid
	const float2x2 gridToSkewedGrid = float2x2(1.0, -0.57735027, 0.0, 1.15470054);
	float2 skewedCoord = mul(gridToSkewedGrid, st);

	int2 baseId = int2(floor(skewedCoord));
	float3 temp = float3(frac(skewedCoord), 0);
	temp.z = 1.0 - temp.x - temp.y;

	float s = step(0.0, -temp.z);
	float s2 = 2 * s - 1;

	w1 = -temp.z * s2;
	w2 = s - temp.y * s2;
	w3 = s - temp.x * s2;

	vertex1 = baseId + int2(s, s);
	vertex2 = baseId + int2(s, 1 - s);
	vertex3 = baseId + int2(1 - s, s);
}

float2 hash(float2 p)
{
	float2 r = mul(float2x2(127.1, 311.7, 269.5, 183.3), p);
	return frac(sin(r) * 43758.5453);
}

float2 MakeCenST(int2 Vertex)
{
	float2x2 invSkewMat = float2x2(1.0, 0.5, 0.0, 1.0 / 1.15470054);

	return mul(invSkewMat, Vertex) / (2 * sqrt(3));
}

float2x2 LoadRot2x2(int2 idx, float rotStrength)
{
	float angle = abs(idx.x * idx.y) + abs(idx.x + idx.y) + Pi;

	// remap to +/-pi
	angle = fmod(angle, 2 * Pi);
	if (angle < 0)
		angle += 2 * Pi;
	if (angle > Pi)
		angle -= 2 * Pi;

	angle *= rotStrength;

	float cs = cos(angle), si = sin(angle);

	return float2x2(cs, -si, si, cs);
}

float3 ProduceHexWeights(float3 W, int2 vertex1, int2 vertex2, int2 vertex3)
{
	float3 res = 0.0;

	int v1 = (vertex1.x - vertex1.y) % 3;
	if (v1 < 0)
		v1 += 3;

	int vh = v1 < 2 ? (v1 + 1) : 0;
	int vl = v1 > 0 ? (v1 - 1) : 2;
	int v2 = vertex1.x < vertex3.x ? vl : vh;
	int v3 = vertex1.x < vertex3.x ? vh : vl;

	res.x = v3 == 0 ? W.z : (v2 == 0 ? W.y : W.x);
	res.y = v3 == 1 ? W.z : (v2 == 1 ? W.y : W.x);
	res.z = v3 == 2 ? W.z : (v2 == 2 ? W.y : W.x);

	return res;
}

float3 Gain3(float3 x, float r)
{
	// increase contrast when r>0.5 and
	// reduce contrast if less
	float k = log(1 - r) / log(0.5);

	float3 s = 2 * step(0.5, x);
	float3 m = 2 * (1 - s);

	float3 res = 0.5 * s + 0.25 * m * pow(max(0.0, s + x * m), k);
	
	return res.xyz / (res.x + res.y + res.z);
}


// Input:\ tex is a texture with color
// Input:\ r increase contrast when r>0.5
// Output:\ color is the blended result
// Output:\ weights shows the weight of each hex tile
void hex2colTex(out float4 color, out float3 weights, Texture2D tex, SamplerState samp, float2 st, float rotStrength, float r = 0.5)
{
	float2 dSTdx = ddx(st), dSTdy = ddy(st);

	// Get triangle info
	float w1, w2, w3;
	int2 vertex1, vertex2, vertex3;
	TriangleGrid(w1, w2, w3, vertex1, vertex2, vertex3, st);

	float2x2 rot1 = LoadRot2x2(vertex1, rotStrength);
	float2x2 rot2 = LoadRot2x2(vertex2, rotStrength);
	float2x2 rot3 = LoadRot2x2(vertex3, rotStrength);

	float2 cen1 = MakeCenST(vertex1);
	float2 cen2 = MakeCenST(vertex2);
	float2 cen3 = MakeCenST(vertex3);

	float2 st1 = mul(st - cen1, rot1) + cen1 + hash(vertex1);
	float2 st2 = mul(st - cen2, rot2) + cen2 + hash(vertex2);
	float2 st3 = mul(st - cen3, rot3) + cen3 + hash(vertex3);

	// Fetch input
	float4 c1 = tex.SampleGrad(samp, st1, mul(dSTdx, rot1), mul(dSTdy, rot1));
	float4 c2 = tex.SampleGrad(samp, st2, mul(dSTdx, rot2), mul(dSTdy, rot2));
	float4 c3 = tex.SampleGrad(samp, st3, mul(dSTdx, rot3), mul(dSTdy, rot3));

	// use luminance as weight
	float3 Lw = float3(0.299, 0.587, 0.114);
	float3 Dw = float3(dot(c1.xyz, Lw), dot(c2.xyz, Lw), dot(c3.xyz, Lw));
	
	float g_fallOffContrast = 0.6;
	float g_exp = 7.0;
	
	Dw = lerp(1.0, Dw, g_fallOffContrast); // 0.6
	float3 W = Dw * pow(float3(w1, w2, w3), g_exp); // 7
	W /= (W.x + W.y + W.z);
	if (r != 0.5)
		W = Gain3(W, r);

	color = W.x * c1 + W.y * c2 + W.z * c3;
	weights = ProduceHexWeights(W.xyz, vertex1, vertex2, vertex3);
}

float3 Fragment(FragmentInput input) : SV_Target
{
	float4 terrainData = _Control.Gather(_PointClampSampler, input.uv.xy) * 255.0;
	float4 weights = BilinearWeights(input.uv.xy, _Control_TexelSize.zw);
	
	float3 color = _MainTex.Sample(_LinearRepeatSampler, float3(input.uv.zw, terrainData.x)) * weights.x;
	color += _MainTex.Sample(_LinearRepeatSampler, float3(input.uv.zw, terrainData.y)) * weights.y;
	color += _MainTex.Sample(_LinearRepeatSampler, float3(input.uv.zw, terrainData.z)) * weights.z;
	color += _MainTex.Sample(_LinearRepeatSampler, float3(input.uv.zw, terrainData.w)) * weights.w;
	
	float3 normal = normalize(input.normal);
	float3 lighting = GetLighting(normal, input.worldPosition);
	
	lighting += _Ambient;
	color.rgb *= lighting * input.color;
	color.rgb = ApplyFog(color.rgb, input.worldPosition, InterleavedGradientNoise(input.position.xy, 0));
	
	return color;
}