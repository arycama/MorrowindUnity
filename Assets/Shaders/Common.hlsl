#ifndef COMMON_INCLUDED
#define COMMON_INCLUDED

#ifdef GBUFFER
	const static bool IsGbufferPass = true;
#else
	const static bool IsGbufferPass = false;
#endif

#ifdef SHADOW
	const static bool IsShadowPass = true;
#else
	const static bool IsShadowPass = false;
#endif

#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Geometry.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/MatrixUtils.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Packing.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Samplers.hlsl"

struct GbufferOutput
{
	float4 albedoMetallic : SV_Target0;
	float4 normalOcclusionRoughness : SV_Target1;
	float4 emission : SV_Target2;
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

cbuffer EnvironmentData
{
	float3 AmbientLight;
	float FogScale;
	
	float3 FogColor;
	float FogOffset;
	
	float Time;
	float FogStart;
	float FogEnd;
	float FogDensity;
};

cbuffer ViewData
{
	matrix WorldToClip;
	matrix ViewToClip;
	matrix WorldToView;
	matrix PixelToClip;
	matrix ScreenToWorld; 
	matrix WorldToPreviousScreen;
	matrix UiOverlayMatrix;
	
	float LinearDepthScale, LinearDepthOffset, Near, Far;
	
	float2 ViewSize;
	float2 RcpViewSize;
	
	float3 ViewPosition;
	float ViewDataPadding;
	
	float4 FrustumCorners[3];
};

cbuffer CascadeData
{
	matrix WorldToShadowClip;
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

cbuffer VolumetricLightingData
{
	float3 VolumeSize;
	float MaxDepth;
};

StructuredBuffer<Light> PointLights;
StructuredBuffer<uint> LightDepthMinMax;
Texture2D<float> SunShadow;
Texture2DArray<uint> VisibleLightBits;
Texture3D<float4> VolumetricLighting;

float3 UnderwaterColor;
float UnderwaterColorWeight;

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

float3x4 GetObjectToWorld(uint instanceId)
{
	#ifdef INSTANCING_ON
		return (float3x4)unity_Builtins0Array[unity_BaseInstanceID + instanceId].unity_ObjectToWorldArray;
	#else
		return unity_ObjectToWorld;
	#endif
}

float3 ObjectToWorldPosition(float3 position, uint instanceId)
{
	float3x4 objectToWorld = GetObjectToWorld(instanceId);
	return MultiplyPoint3x4(objectToWorld, position);
}

float3 WorldToViewPosition(float3 position)
{
	return MultiplyPoint3x4(WorldToView, position);
}

float3 ObjectToViewPosition(float3 position, uint instanceId)
{
	float3 worldPosition = ObjectToWorldPosition(position, instanceId);
	return WorldToViewPosition(worldPosition);
}

float4 ViewToClipPosition(float3 position)
{
	return MultiplyPoint(ViewToClip, position);
}

float4 WorldToClipPosition(float3 position)
{
	#ifdef SHADOW
		return MultiplyPoint(WorldToShadowClip, position);
	#else
		return MultiplyPoint(WorldToClip, position);
	#endif
}

float4 ObjectToClipPosition(float3 position, uint instanceId)
{
	float3 worldPosition = ObjectToWorldPosition(position, instanceId);
	return WorldToClipPosition(worldPosition);
}

float3 WorldToViewNormal(float3 normal)
{
	return MultiplyVector(WorldToView, normal);
}

float3 ObjectToViewNormal(float3 normal, uint instanceId)
{
	float3x4 objectToWorld = GetObjectToWorld(instanceId);
	float3 worldNormal = normalize(MultiplyVector(objectToWorld, normal));
	return WorldToViewNormal(worldNormal);
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

float LinearEyeDepth(float depth)
{
	return rcp(LinearDepthScale * depth + LinearDepthOffset);
}

float LinearToDeviceDepth(float eyeDepth)
{
	return rcp(eyeDepth) * ViewToClip._m23 + ViewToClip._m22;
}

GbufferOutput OutputGbuffer(float3 albedo, float3 normal, float3 emission)
{
	GbufferOutput gbuffer;
	gbuffer.albedoMetallic = float4(albedo, 0.0);
	gbuffer.normalOcclusionRoughness = float4(normal * 0.5 + 0.5, 1.0);
	gbuffer.emission = float4(emission, 0.0);
	return gbuffer;
}

uint3 GetClusterIndex(float3 screenPosition)
{
	return float3(screenPosition.xy * RcpTileSize, screenPosition.z * RcpBinWidth);
}

float3 GetLuminance(float3 normal, float3 viewPosition, float2 screenPosition, out float3 illuminance)
{
	// Directional light
	illuminance = SunColor;
	
	// Shadow
	#ifdef SHADOWS_ON
		float fade = saturate(viewPosition.z * SunShadowFadeScale + SunShadowFadeOffset);
		float3 shadowPosition = MultiplyPoint3x4((float3x4) WorldToSunShadow, viewPosition);
		if (fade && all(saturate(shadowPosition.xy) == shadowPosition.xy))
			illuminance *= lerp(1.0, SunShadow.SampleCmpLevelZero(LinearClampCompareSampler, shadowPosition.xy, shadowPosition.z), fade);
	#endif
	
	float NdotL = saturate(dot(normal, SunDirection));
	float3 luminance = illuminance * NdotL;
	
	#ifdef POINT_LIGHTS_ON
		// Flat bit array iterator scalarized on entity with Z-Bin masked words
		float3 cluster = GetClusterIndex(float3(screenPosition, viewPosition.z));
		uint2 lightRange = BitUnpack(LightDepthMinMax[cluster.z], 16, uint2(0, 16));
		uint2 mergedRange = uint2(WaveActiveMin(lightRange.x), WaveActiveMax(lightRange.y)) >> 5u;
	
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
			while (mask != 0u)
			{
				uint bitIndex = firstbitlow(mask);
				uint lightIndex = 32u * i + bitIndex;
				mask &= ~(1u << bitIndex);
			
				Light light = PointLights[lightIndex];
	
				// Attenuation
				float3 lightVector = light.position - viewPosition;
				float distanceSquared = dot(lightVector, lightVector);
				float attenuation = saturate(1.0h - distanceSquared * light.rcpRangeSquared);
		
				float3 L = lightVector * rsqrt(distanceSquared);
				attenuation *= saturate(dot(light.forward, L) * light.angleScale + light.angleOffset);
				attenuation = Sq(attenuation);
			
				illuminance += attenuation * light.color;
				luminance += saturate(dot(normal, L)) * attenuation * light.color;
			}
		}
	#endif
	
	return luminance;
}

float3 GetLuminance(float3 normal, float3 viewPosition, float2 screenPosition)
{
	float3 illuminance;
	return GetLuminance(normal, viewPosition, screenPosition, illuminance);
}

float3 GetFrustumCorner(uint id)
{
	return FrustumCorners[id];
}

float FogFactor(float3 viewPosition)
{
	//return saturate(viewPosition.z * FogScale + FogOffset);
	return saturate(length(viewPosition) * FogScale + FogOffset);
}

float4 GetLuminanceAndFog(float4 color, float3 ambient, float3 normal, float2 screenPosition, float3 viewPosition)
{
	color.rgb *= ambient + GetLuminance(normal, viewPosition, screenPosition);
	
	if (ViewPosition.y < 0)
		color.rgb = lerp(color.rgb, color.rgb * UnderwaterColor, UnderwaterColorWeight);
	
	// Fog
	#ifdef VOLUMETRIC_LIGHT_ON
		float3 volumetricUv = float3(screenPosition / ViewSize, viewPosition.z / MaxDepth);
		volumetricUv.y = 1 - volumetricUv.y;
	
		float4 volumetricLight = VolumetricLighting.Sample(LinearClampSampler, volumetricUv);
		float3 fogLuminance = volumetricLight.rgb;
		float fogTransmittance = volumetricLight.a;
	#else
		float fogOpacity = FogFactor(viewPosition);
		float3 fogColor = FogColor;
		float3 fogLuminance = fogColor * fogOpacity;
		float fogTransmittance = 1.0 - fogOpacity;
	#endif
		
	color.rgb = color.rgb * fogTransmittance + fogLuminance;
	
	return color;
}

float3 GammaToLinear(float3 color)
{
	return select(color <= 0.04045, color * rcp(12.92), pow((color + 0.055) * rcp(1.055), 2.4));
}

#endif