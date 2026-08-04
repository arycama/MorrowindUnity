#ifndef COMMON_INCLUDED
#define COMMON_INCLUDED

#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/MatrixUtils.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Packing.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Samplers.hlsl"

cbuffer EnvironmentData
{
	float3 AmbientLight;
	float FogScale;
	
	float3 FogColor;
	float FogOffset;
	
	float Time;
	float FogStart;
	float FogEnd;
	float FrameDataPadding;
};

cbuffer ViewData
{
	matrix WorldToClip;
	matrix ViewToClip;
	matrix WorldToView;
	matrix PixelToClip;
	matrix ScreenToWorld; 
	matrix WorldToPreviousScreen;
	
	float LinearDepthScale, LinearDepthOffset, Near, Far;
	
	float2 ViewSize;
	float2 RcpViewSize;
	
	float3 ViewPosition;
	float ViewDataPadding;
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
	float RcpTileSize;
	float RcpBinWidth;
};

Texture2D<float> SunShadow;
StructuredBuffer<Light> PointLights;
StructuredBuffer<uint> LightDepthMinMax;
Texture2DArray<uint> VisibleLightBits;

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

float4 WorldToPreviousScreenPosition(float3 position)
{
	return MultiplyPoint(WorldToPreviousScreen, position);
}

float4 ScreenToPreviousScreenPosition(float2 uv, float depth)
{
	float3 worldPosition = MultiplyPointProj(ScreenToWorld, float3(uv, depth)).xyz;
	return WorldToPreviousScreenPosition(worldPosition);
}

float LinearToDeviceDepth(float eyeDepth)
{
	return rcp(eyeDepth) * ViewToClip._m23 + ViewToClip._m22;
}

uint3 GetClusterIndex(float3 screenPosition)
{
	return float3(screenPosition.xy * RcpTileSize, screenPosition.z * RcpBinWidth);
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
	
	// Flat bit array iterator scalarized on entity with Z-Bin masked words
	float3 cluster = GetClusterIndex(float3(screenPosition.xy, screenPosition.w));
	uint2 lightRange = BitUnpack(LightDepthMinMax[cluster.z], 16, uint2(0, 16));
	uint2 mergedRange = uint2(WaveActiveMin(lightRange.x), WaveActiveMax(lightRange.y)) >> 5u;
	half3 luminance = 0.0h;
	
	uint lightCount = 0;
	
	// Read range of words of visibility bits
	for (uint i = mergedRange.x; i <= mergedRange.y; i++)
	{
		// Load bit mask data per lane
		uint tileMask = VisibleLightBits[uint3(cluster.xy, i)];
		
		// Mask by zbin mask
		uint localMin = clamp((int) lightRange.x - (int) (32 * i), 0, 31);
		uint maskWidth = clamp((int) lightRange.y - (int) lightRange.x + 1, 0, 32);
		
		// BitFieldMask op needs manual 32 size wrap support
		uint depthMask = maskWidth == 32u ? UintMax : ((1u << maskWidth) - 1u) << localMin;
		
		// Compact world bitmask over all lanes in wavefront
		uint mask = WaveActiveBitOr(tileMask & depthMask);
		//lightCount++;
		
		while (mask != 0u)
		{
			uint bitIndex = firstbitlow(mask);
			uint lightIndex = 32u * i + bitIndex;
			mask &= ~(1u << bitIndex);
			
			Light light = PointLights[lightIndex];
			lightCount++;
	
			// Attenuation
			float3 lightVector = light.position - worldPosition;
			float distanceSquared = dot(lightVector, lightVector);
			float attenuation = saturate(1.0h - distanceSquared * light.rcpRangeSquared);
		
			float3 L = lightVector * rsqrt(distanceSquared);
			attenuation *= saturate(dot(light.forward, L) * light.angleScale + light.angleOffset);
			attenuation = Sq(attenuation);
			
			//lighting += saturate(dot(normal, L)) * attenuation * light.color;
			lighting += (attenuation > 0) * light.color;
		}
	}
	
	//lighting = (lightCount >> uint3(0, 1, 2) & 1);
	
	return lighting;
}

#endif