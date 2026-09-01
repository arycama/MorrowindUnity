#include "../Common.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Geometry.hlsl"

struct VertexInput
{
	uint instanceId : SV_InstanceID;
	float3 position : POSITION;
};

struct FragmentInput
{
	float4 position : SV_Position;
	float4 sphere : TEXCOORD0;
	float3 rayDirection : TEXCOORD1;
	nointerpolation uint4 data : TEXCOORD2;
};

RWTexture2DArray<uint> VisibleLightBitsWrite : register(u0);
uint IndexOffset;

FragmentInput Vertex(VertexInput input)
{
	uint index = input.instanceId + IndexOffset;

	FragmentInput output;
	
	Light light = PointLights[index];
	float3 viewPosition = input.position * light.cullingSphere.w + light.cullingSphere.xyz;
	
	output.position = mul(ViewToClip, float4(viewPosition, 1.0));
	output.sphere = light.cullingSphere;
	output.rayDirection = viewPosition;
	output.data.x = index / 32u; // Offset
	output.data.y = 1 << (index % 32); // Bit
	output.data.z = light.angleScale == 0;
	output.data.w = output.data.x * TileCount;
	return output;
}

uint WaveCompactValue(uint checkValue, uint bit, out uint mergedBit)
{
	uint mask, firstValue;
	do
	{
		uint firstValue = WaveReadLaneFirst(checkValue);
		mask = WaveActiveBallot(firstValue == checkValue);
	} while (firstValue != checkValue);
	
	uint index = WavePrefixCountBits(mask);
	return index;
	
}

[earlydepthstencil]
void Fragment(FragmentInput input)
{
	float2 hits;
	if (!IntersectRaySphere(-input.sphere.xyz, input.rayDirection, input.sphere.w, hits))
		discard;
		
	uint2 tile = input.position.xy * RcpTileSize;
	uint addrLinear = tile.x + tile.y * TileCountX + input.data.w;

	uint mergedBit;
	uint hash = WaveCompactValue(addrLinear, input.data.y, mergedBit);

	[branch]
	if (!hash)
		InterlockedOr(VisibleLightBitsWrite[uint3(tile, input.data.x)], input.data.y);
}