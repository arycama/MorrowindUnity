#ifndef COMMON_INCLUDED
#define COMMON_INCLUDED

#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/MatrixUtils.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Packing.hlsl"

cbuffer EnvironmentData
{
	float3 AmbientLight;
	float FogScale;
	
	float3 FogColor;
	float FogOffset;
	
	float Time;
	float3 FrameDataPadding;
};

cbuffer ViewData
{
	matrix WorldToClip;
	matrix ViewToClip;
	matrix WorldToView;
	matrix PixelToClip;
	float LinearDepthScale, LinearDepthOffset, Near, Far;
};

cbuffer CascadeData
{
	matrix WorldToShadowClip;
};

struct Light
{
	float3 position;
	float rcpRangeSquared;
	float3 forward;
	float angleScale;
	float3 color;
	float angleOffset;
	float4 cullingSphere;
};

const static uint MaxLightsPerTile = 32;

cbuffer LightingData
{
	float3 SunDirection;
	float SunShadowFadeScale;
	float3 SunColor;
	float SunShadowFadeOffset;
	
	float4x4 WorldToSunShadow;
	
	float SunShadowRcpResolution;
	float SunShadowResolution;
	float2 LightingDataPadding;
};

cbuffer PointLightData
{
	float TileSize;
	uint LightCount;
	uint TileCountX;
	uint LightIndexCount;
	
	uint LightCullDepthSlices;
	float LightBinWidth;
	float LinearToLogScale;
	float LinearToLogOffset;
};

Texture2D<float> SunShadow;
StructuredBuffer<Light> PointLights;
StructuredBuffer<uint> VisibleLightBits, LightDepthMinMax;

SamplerComparisonState LinearClampCompareSampler;

#ifdef INSTANCING_ON
	cbuffer UnityDrawCallInfo
	{
		uint unity_BaseInstanceID;
	};

	cbuffer UnityInstancing_PerDraw0
	{
		struct
		{
			matrix unity_ObjectToWorldArray;
		}
	
		unity_Builtins0Array[2];
	};
#else
	cbuffer UnityPerDraw
	{
		float3x4 unity_ObjectToWorld, unity_WorldToObject;
		float4 unity_LODFade; // x is the fade value ranging within [0,1]. y is x quantized into 16 levels
		float4 unity_WorldTransformParams; // w is usually 1.0, or -1.0 for odd-negative scale transforms
	};
#endif

float3 ObjectToWorld(float3 position, uint instanceId)
{
	#ifdef INSTANCING_ON
		float3x4 objectToWorld = (float3x4)unity_Builtins0Array[unity_BaseInstanceID + instanceId].unity_ObjectToWorldArray;
	#else
		float3x4 objectToWorld = unity_ObjectToWorld;
	#endif
	
	return MultiplyPoint3x4(objectToWorld, position);
}

float3 WorldToViewPosition(float3 position)
{
	return MultiplyPoint3x4(WorldToView, position);
}

float3 ObjectToViewPosition(float3 position, uint instanceId)
{
	return WorldToViewPosition(ObjectToWorld(position, instanceId));
}

float4 WorldToClipPosition(float3 position)
{
	#ifdef UNITY_PASS_SHADOWCASTER
		return MultiplyPoint(WorldToShadowClip, position);
	#else
		return MultiplyPoint(WorldToClip, position);
	#endif
}

float4 ObjectToClip(float3 position, uint instanceId)
{
	return WorldToClipPosition(ObjectToWorld(position, instanceId));
}

float3 ObjectToWorldNormal(float3 normal, uint instanceId)
{
	// Source: https://www.shadertoy.com/view/3s33zj
	#ifdef INSTANCING_ON
		float3x4 objectToWorld = (float3x4)unity_Builtins0Array[unity_BaseInstanceID + instanceId].unity_ObjectToWorldArray;
	#else
		float3x4 objectToWorld = unity_ObjectToWorld;
	#endif
	
	float3x3 adjoint = float3x3(cross(objectToWorld[1].xyz, objectToWorld[2].xyz), cross(objectToWorld[2].xyz, objectToWorld[0].xyz), cross(objectToWorld[0].xyz, objectToWorld[1].xyz));
	return normalize(mul(adjoint, normal));
}

float4 BilinearWeights(float2 uv, float2 textureSize)
{
	float2 localUv = frac(uv * textureSize - 0.5 + rcp(512.0));
	float4 weights = localUv.xxyy * float4(-1, 1, 1, -1) + float4(1, 0, 0, 1);
	return weights.zzww * weights.xyyx;
}

float3 GetLighting(float3 normal, float3 worldPosition, float4 screenPosition)
{
	// Directional light
	float NdotL = saturate(dot(normal, SunDirection));
	float3 lighting = NdotL * SunColor;
	float fade = saturate(screenPosition.w * SunShadowFadeScale + SunShadowFadeOffset);
	float3 shadowPosition = MultiplyPoint3x4((float3x4) WorldToSunShadow, worldPosition);
	
	// Shadow
	if (NdotL && fade && all(saturate(shadowPosition.xy) == shadowPosition.xy))
		lighting *= lerp(1.0, SunShadow.SampleCmpLevelZero(LinearClampCompareSampler, shadowPosition.xy, shadowPosition.z), fade);
	
	// Point Lights
	uint lightCount = 0;
	uint2 clusterCoord = (uint2) (screenPosition.xy / TileSize);
	uint tileDepth = (uint) (log2(screenPosition.w) * LinearToLogScale + LinearToLogOffset);
	
	uint linearClusterCoord = clusterCoord.y * TileCountX + clusterCoord.x;
	
	uint2 zRange = BitUnpack(LightDepthMinMax[tileDepth], 16, uint2(0, 16));
	
	// Find min/max light we need to consider when shading slice Z
	uint zStart = WaveActiveMin(zRange.x) >> 5u;
	uint zEnd = WaveActiveMax(zRange.y) >> 5u;
	
	for (uint i = zStart; i <= zEnd; i++)
	{
		uint mask = VisibleLightBits[linearClusterCoord * LightIndexCount + i];
		
		// Restrict to lights within Z-range
		uint startIndex = 32u * i;
		
		uint2 range;
		range.x = clamp(zRange.x, startIndex, startIndex + 32u);
		range.y = clamp(zRange.y + 1u, range.x, startIndex + 32u);

		uint numBits = range.y - range.x;
		uint rangeMask = numBits == 32u ? UintMax : ((1u << numBits) - 1u) << (range.x - startIndex);
		mask = WaveActiveBitOr(mask & rangeMask);
	
		while (mask != 0u)
		{
			uint bitIndex = firstbitlow(mask);
			Light light = PointLights[startIndex + bitIndex];
	
			// Attenuation
			float3 lightVector = light.position - worldPosition;
			float distanceSquared = dot(lightVector, lightVector);
			float attenuation = saturate(1.0h - distanceSquared * light.rcpRangeSquared);
		
			float3 L = lightVector * rsqrt(distanceSquared);
			attenuation = Sq(attenuation * saturate(dot(light.forward, L) * light.angleScale + light.angleOffset));
		
			lighting += saturate(dot(normal, L)) * light.color * attenuation;
			mask &= ~(1u << bitIndex);
			
			lightCount++;
		}
	}
	
	//lighting = lightCount >> uint3(0, 1, 2) & 1;
	
	return lighting;
}

#endif