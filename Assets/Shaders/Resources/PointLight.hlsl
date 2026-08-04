#include "../Common.hlsl"

struct VertexInput
{
	uint instanceId : SV_InstanceID;
	float3 position : POSITION;
};

struct FragmentInput
{
	float4 position : SV_Position;
	nointerpolation uint2 data : TEXCOORD;
};

RWTexture2DArray<uint> VisibleLightBitsWrite : register(u0);

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	
	Light light = PointLights[input.instanceId];
	float3 viewPosition = input.position * light.cullingSphere.w + light.cullingSphere.xyz;
	
	// Invert culling if camera inside
	//if(light.cullingSphere.z - light.cullingSphere.w * 1.075 <= 0)
	//	viewPosition = -input.position * light.cullingSphere.w + light.cullingSphere.xyz;
	
	output.position = mul(ViewToClip, float4(viewPosition, 1.0));
	output.data.x = input.instanceId / 32u; // Offset
	output.data.y = 1 << (input.instanceId % 32); // Bit
	return output;
}

//uint WaveCompactValue(uint checkValue)
//{
//	uint mask; // lane unique compaction mask
//	for (;;) // Loop until all active lanes removed
//	{
//		uint firstValue = WaveReadLaneFirst(checkValue);
//		mask = WaveActiveBallot(firstValue == checkValue); // mask is only updated for remaining active lanes
//		if (firstValue == checkValue)
//			break; // exclude all lanes with firstValue from next iteration
//	}
//	// At this point, each lane of mask should contain a bit mask of all other lanes with the same value.
//	uint index = WavePrefixCountBits(mask); // Note this is performed independently on a different mask for each lane.
//	return index;
//}

[earlydepthstencil]
void Fragment(FragmentInput input)
{
	float2 tile = input.position.xy * RcpTileSize;
	InterlockedOr(VisibleLightBitsWrite[uint3(tile, input.data.x)], input.data.y);
}