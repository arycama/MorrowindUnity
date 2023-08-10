﻿#ifndef COMMON_INCLUDED
#define COMMON_INCLUDED

struct DirectionalLight
{
	float3 color;
	int shadowIndex;
	float3 direction;
	int cascadeCount;
};

struct PointLight
{
	float3 position;
	float range;
	float3 color;
	uint shadowIndex;
	uint visibleFaces;
	float near;
	float far;
	float padding;
};

Buffer<uint> _LightClusterList;
SamplerComparisonState _LinearClampCompareSampler;
SamplerState _LinearClampSampler, _LinearRepeatSampler, _PointClampSampler;
StructuredBuffer<DirectionalLight> _DirectionalLights;
StructuredBuffer<matrix> _DirectionalMatrices;
StructuredBuffer<PointLight> _PointLights;
Texture2D<float> _BlueNoise1D;
Texture2DArray<float> _DirectionalShadows;
Texture3D<float4> _VolumetricLighting;
Texture3D<uint2> _LightClusterIndices;
TextureCubeArray<float> _PointShadows;

uint _TileSize;
float _ClusterScale;
float _ClusterBias;

float4 _Time, _ProjectionParams, _ZBufferParams, _ScreenParams;
float3 _AmbientLightColor, _WorldSpaceCameraPos, _FogColor;
float _FogStartDistance, _FogEndDistance, _FogEnabled, _VolumeWidth, _VolumeHeight, _VolumeSlices, _VolumeDepth, _NonLinearDepth;
matrix _InvViewProjectionMatrix, _PreviousViewProjectionMatrix, unity_MatrixVP;
uint _DirectionalLightCount, _FrameCount, _PointLightCount, unity_BaseInstanceID;

cbuffer UnityPerDraw
{
	matrix unity_ObjectToWorld, unity_WorldToObject, unity_MatrixPreviousM, unity_MatrixPreviousMI;
	float4 unity_MotionVectorsParams;
};

cbuffer UnityInstancing_PerDraw0
{
	struct
	{
		matrix unity_ObjectToWorldArray, unity_WorldToObjectArray;
	}
	
	unity_Builtins0Array[2];
};

float Sq(float x)
{
	return x * x;
}

// Remaps a value from one range to another
float1 Remap(float1 v, float1 pMin, float1 pMax = 1.0, float1 nMin = 0.0, float1 nMax = 1.0) { return nMin + (v - pMin) * rcp(pMax - pMin) * (nMax - nMin); }
float2 Remap(float2 v, float2 pMin, float2 pMax = 1.0, float2 nMin = 0.0, float2 nMax = 1.0) { return nMin + (v - pMin) * rcp(pMax - pMin) * (nMax - nMin); }
float3 Remap(float3 v, float3 pMin, float3 pMax = 1.0, float3 nMin = 0.0, float3 nMax = 1.0) { return nMin + (v - pMin) * rcp(pMax - pMin) * (nMax - nMin); }
float4 Remap(float4 v, float4 pMin, float4 pMax = 1.0, float4 nMin = 0.0, float4 nMax = 1.0) { return nMin + (v - pMin) * rcp(pMax - pMin) * (nMax - nMin); }

bool IntersectRayPlane(float3 rayOrigin, float3 rayDirection, float3 planePosition, float3 planeNormal, out float t)
{
	bool res = false;
	t = -1.0;

	float denom = dot(planeNormal, rayDirection);
	if (abs(denom) > 1e-5)
	{
		float3 d = planePosition - rayOrigin;
		t = dot(d, planeNormal) / denom;
		res = (t >= 0);
	}

	return res;
}

bool IntersectRayPlane(float3 rayOrigin, float3 rayDirection, float3 planePosition, float3 planeNormal)
{
	float t;
	return IntersectRayPlane(rayOrigin, rayDirection, planePosition, planeNormal, t);
}


//From  Next Generation Post Processing in Call of Duty: Advanced Warfare [Jimenez 2014]
// http://advances.floattimerendering.com/s2014/index.html
float InterleavedGradientNoise(float2 pixCoord, int frameCount)
{
	const float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
	float2 frameMagicScale = float2(2.083, 4.867);
	pixCoord += frameCount * frameMagicScale;
	return frac(magic.z * frac(dot(pixCoord, magic.xy)));
}

float2 ApplyScaleOffset(float2 uv, float4 scaleOffset)
{
	return uv * scaleOffset.xy + scaleOffset.zw;
}

float4 LinearEyeDepth(float4 depth, float4 zBufferParam)
{
	return 1.0 / (zBufferParam.z * depth + zBufferParam.w);
}

float Max2(float2 x) { return max(x.x, x.y); }
float Max3(float3 x) { return max(x.x, max(x.y, x.z)); }
float Max4(float4 x) { return Max2(max(x.xy, x.zw)); }

float Min2(float2 x) { return min(x.x, x.y); }
float Min3(float3 x) { return min(x.x, min(x.y, x.z)); }
float Min4(float4 x) { return Min2(min(x.xy, x.zw)); }

// Normalize if bool is set to true
float3 ConditionalNormalize(float3 input, bool doNormalize) { return doNormalize ? normalize(input) : input; }

// Divides a 4-component vector by it's w component
float4 PerspectiveDivide(float4 input) { return float4(input.xyz * rcp(input.w), input.w); }

const static float3x3 Identity3x3 = float3x3(1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0);

// Fast matrix muls (3 mads)
float4 MultiplyPoint(float3 p, float4x4 mat) { return p.x * mat[0] + (p.y * mat[1] + (p.z * mat[2] + mat[3])); }
float4 MultiplyPoint(float4x4 mat, float3 p) { return MultiplyPoint(p, transpose(mat)); }
float4 MultiplyPointProj(float4x4 mat, float3 p) { return PerspectiveDivide(MultiplyPoint(p, transpose(mat))); }

// 3x4, for non-projection matrices
float3 MultiplyPoint3x4(float3 p, float4x3 mat) { return p.x * mat[0] + (p.y * mat[1] + (p.z * mat[2] + mat[3])); }
float3 MultiplyPoint3x4(float4x4 mat, float3 p) { return MultiplyPoint3x4(p, transpose((float3x4) mat)); }
float3 MultiplyPoint3x4(float3x4 mat, float3 p) { return MultiplyPoint3x4(p, transpose(mat)); }

float3 MultiplyVector(float3 v, float3x3 mat, bool doNormalize) { return ConditionalNormalize(v.x * mat[0] + v.y * mat[1] + v.z * mat[2], doNormalize); }
float3 MultiplyVector(float3 v, float4x4 mat, bool doNormalize) { return MultiplyVector(v, (float3x3)mat, doNormalize); }
float3 MultiplyVector(float3x3 mat, float3 v, bool doNormalize) { return MultiplyVector(v, transpose(mat), doNormalize); }
float3 MultiplyVector(float4x4 mat, float3 v, bool doNormalize) { return MultiplyVector((float3x3) mat, v, doNormalize); }
float3 MultiplyVector(float3x4 mat, float3 v, bool doNormalize) { return MultiplyVector((float3x3) mat, v, doNormalize); }

float3 ObjectToWorld(float3 position, uint instanceID)
{
#ifdef INSTANCING_ON
	float3x4 objectToWorld = unity_Builtins0Array[unity_BaseInstanceID + instanceID].unity_ObjectToWorldArray;
#else
	float3x4 objectToWorld = unity_ObjectToWorld;
#endif
	
	return MultiplyPoint3x4(objectToWorld, position);
}

float4 WorldToClip(float3 position)
{
	return MultiplyPoint(unity_MatrixVP, position);
}

float4 ObjectToClip(float3 position, uint instanceID)
{
	return WorldToClip(ObjectToWorld(position, instanceID));
}

float3 ObjectToWorldNormal(float3 normal, uint instanceID, bool doNormalize = false)
{
#ifdef INSTANCING_ON
	float3x4 worldToObject = unity_Builtins0Array[unity_BaseInstanceID + instanceID].unity_WorldToObjectArray;
#else
	float3x4 worldToObject = unity_WorldToObject;
#endif
	
	return MultiplyVector(normal, worldToObject, doNormalize);
}

float EyeToDeviceDepth(float eyeDepth)
{
	return (1.0 - eyeDepth * _ZBufferParams.w) * rcp(eyeDepth * _ZBufferParams.z);
}

float3 PixelToWorld(float3 position)
{
	float3 positionNDC = float3(position.xy / _ScreenParams.xy * 2 - 1, position.z);
	return MultiplyPointProj(_InvViewProjectionMatrix, positionNDC).xyz;
}

float Remap01ToHalfTexelCoord(float coord, float size)
{
	const float start = 0.5 * rcp(size);
	const float len = 1.0 - rcp(size);
	return coord * len + start;
}

float2 Remap01ToHalfTexelCoord(float2 coord, float2 size)
{
	const float2 start = 0.5 * rcp(size);
	const float2 len = 1.0 - rcp(size);
	return coord * len + start;
}

float3 Remap01ToHalfTexelCoord(float3 coord, float3 size)
{
	const float3 start = 0.5 * rcp(size);
	const float3 len = 1.0 - rcp(size);
	return coord * len + start;
}

// Converts a value between 0 and 1 to a device depth value where 0 is far and 1 is near in both cases.
float Linear01ToDeviceDepth(float z)
{
	float n = _ProjectionParams.y;
	float f = _ProjectionParams.z;
	return n * (1.0 - z) / (n + z * (f - n));
}

float GetDeviceDepth(float normalizedDepth)
{
	if (_NonLinearDepth)
	{
		// Non-linear depth distribution
		float near = _ProjectionParams.y, far = _ProjectionParams.z;
		float linearDepth = near * pow(far / near, normalizedDepth);
		return EyeToDeviceDepth(linearDepth);
	}
	else
	{
		return Linear01ToDeviceDepth(normalizedDepth);
	}
}

uint _LightDebug;

float3 GetLighting(float3 normal, float3 worldPosition, bool isVolumetric = false)
{
	// Directional lights
	float3 lighting = 0.0;
	
	for (uint i = 0; i < _DirectionalLightCount; i++)
	{
		DirectionalLight light = _DirectionalLights[i];
		
		float shadow = 1.0;
		
		if(light.shadowIndex != ~0u)
		{
			for (uint j = 0; j < light.cascadeCount; j++)
			{
				// find the first cascade which is not out of bounds
				matrix shadowMatrix = _DirectionalMatrices[light.shadowIndex + j];
				float3 positionLS = MultiplyPoint3x4(shadowMatrix, worldPosition);
				if (any(saturate(positionLS) != positionLS))
					continue;
			
				shadow = _DirectionalShadows.SampleCmpLevelZero(_LinearClampCompareSampler, float3(positionLS.xy, light.shadowIndex + j), positionLS.z);
				break;
			}
		}
		
		if(shadow > 0.0)
			lighting += (isVolumetric ? 1.0 : saturate(dot(normal, light.direction))) * light.color * shadow;
	}
	
	float4 positionCS = PerspectiveDivide(WorldToClip(worldPosition));
	positionCS.xy = (positionCS.xy * 0.5 + 0.5) * _ScreenParams.xy;
	
	uint3 clusterIndex;
	clusterIndex.xy = floor(positionCS.xy) / 16;//_TileSize;
	clusterIndex.z = log2(positionCS.w) * _ClusterScale + _ClusterBias;
	
	uint2 lightOffsetAndCount = _LightClusterIndices[clusterIndex];
	uint startOffset = lightOffsetAndCount.x;
	uint lightCount = lightOffsetAndCount.y;
	
	_LightDebug = lightCount;
	
	// Point lights
	for (i = 0; i < lightCount; i++)
	//for (i = 0; i < _PointLightCount; i++)
	{
		int index = _LightClusterList[startOffset + i];
		//PointLight light = _PointLights[i];
		PointLight light = _PointLights[index];
		
		float3 lightVector = light.position - worldPosition;
		float sqrLightDist = dot(lightVector, lightVector);
		float rcpLightDist = rsqrt(sqrLightDist);
		float lightDist = rcpLightDist * sqrLightDist;
		float3 L = lightVector * rcpLightDist;
		
		if(lightDist > light.range)
			continue;
		
		float shadow = 1.0;
		if (light.shadowIndex != ~0u)
		{
			uint visibleFaces = light.visibleFaces;
			float dominantAxis = Max3(abs(lightVector * float3(-1, 1, -1)));
			float depth = rcp(dominantAxis) * light.far + light.near;
			shadow = _PointShadows.SampleCmpLevelZero(_LinearClampCompareSampler, float4(lightVector * float3(-1, 1, -1), light.shadowIndex), depth);
		}
		
		if (shadow > 0.0)
		{
			float attenuation = saturate(Remap(light.range * rcp(3.0) * rcpLightDist, rcp(3.0)));
			lighting += (isVolumetric ? 1.0 : saturate(dot(normal, L))) * light.color * attenuation * shadow;
		}
	}
	
	return lighting;
}

float GetVolumetricUv(float linearDepth)
{
	float near = _ProjectionParams.y, far = _ProjectionParams.z;
	
	if (_NonLinearDepth)
	{
		return (log(linearDepth) * (_VolumeSlices / log(far / near)) - _VolumeSlices * log(near) / log(far / near)) / _VolumeSlices;
	}
	else
	{
		return Remap(linearDepth, near, far);
	}
}

float4 SampleVolumetricLighting(float3 worldPosition)
{
	float4 positionCS = PerspectiveDivide(WorldToClip(worldPosition));
	positionCS.xy = 0.5 * positionCS.xy + 0.5;
	float normalizedDepth = GetVolumetricUv(positionCS.w);
	float3 volumeUv = float3(positionCS.xy, normalizedDepth);
	
	return _VolumetricLighting.SampleLevel(_LinearClampSampler, volumeUv, 0.0);
}

float3 ApplyFog(float3 color, float3 worldPosition, float dither)
{
	if (!_FogEnabled)
		return color;
	
	float4 volumetricLighting = SampleVolumetricLighting(worldPosition);
	return color * volumetricLighting.a + volumetricLighting.rgb;
	
	// Todo: compute on CPU

	float3 sunColor = 0.0;
	for (uint i = 0; i < _DirectionalLightCount; i++)
		sunColor += _DirectionalLights[i].color;
	
	// Calculate extinction coefficient from color and distance
	// TODO: Assume fog is white, and fogcolor is actually lighting.. might not work for interiors though? Could do some kind of... dirlight/fog color or something
	float3 albedo = _FogColor / (sunColor + _AmbientLightColor);
	float samples = 64;
	
	float3 rayStart = _WorldSpaceCameraPos;
	float3 ray = worldPosition - rayStart;
	float3 rayStep = ray / samples;
	float ds = length(rayStep);
	
	float4 result = float2(0.0, 1.0).xxxy;
	for (float i = dither; i < samples; i++)
	{
		// Treat the extinction as a spatially varying coefficient, based on linear fog.
		float rayDist = ds * i;
		
		float3 position = rayStep * i + rayStart;
		
		// Point lights
		float3 lighting = _AmbientLightColor + GetLighting(0.0, position, true);
		
		float extinction = 0.0;
		if (rayDist > _FogStartDistance && rayDist < _FogEndDistance)
			extinction = rcp(_FogEndDistance - rayDist);
		
		float3 luminance = albedo * extinction * lighting;
		
		float transmittance = exp(-extinction * ds);
		float3 integScatt = luminance * (1.0 - transmittance) / max(extinction, 1e-7);
		
		result.rgb += integScatt * result.a;
		result.a *= transmittance;
	}
	
	return color * result.a + result.rgb;
}

#endif