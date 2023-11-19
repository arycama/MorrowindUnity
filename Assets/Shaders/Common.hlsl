#ifndef COMMON_INCLUDED
#define COMMON_INCLUDED

struct DirectionalLight
{
	float3 color;
	uint shadowIndex;
	float3 direction;
	uint cascadeCount;
	float3x4 worldToLight;
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

Buffer<float4> _DirectionalShadowTexelSizes, _PointShadowTexelSizes;
Buffer<uint> _LightClusterList;
SamplerComparisonState _PointClampCompareSampler, _LinearClampCompareSampler;
SamplerState _LinearClampSampler, _LinearRepeatSampler, _PointClampSampler, _TrilinearRepeatAniso16Sampler;
StructuredBuffer<DirectionalLight> _DirectionalLights;
StructuredBuffer<matrix> _DirectionalMatrices;
StructuredBuffer<PointLight> _PointLights;
Texture2D<float> _BlueNoise1D;
Texture2D<float2> _BlueNoise2D;
Texture2DArray<float> _DirectionalShadows;
Texture3D<float4> _VolumetricLighting;
Texture3D<uint2> _LightClusterIndices;
TextureCubeArray<float> _PointShadows;

float4 _Time, _ProjectionParams, _ZBufferParams, _ScreenParams;
float3 _AmbientLightColor, _WorldSpaceCameraPos, _FogColor, _WaterAlbedo, _WaterExtinction;
float2 _Jitter;
float _BlockerRadius, _ClusterBias, _ClusterScale, _FogStartDistance, _FogEndDistance, _FogEnabled, _PcfRadius, _PcssSoftness, _VolumeWidth, _VolumeHeight, _VolumeSlices, _VolumeDepth, _NonLinearDepth;
matrix _InvVPMatrix, _PreviousVPMatrix, unity_MatrixVP, _NonJitteredVPMatrix, unity_MatrixV;
uint _BlockerSamples, _DirectionalLightCount, _FrameCount, _PcfSamples, _PointLightCount, _TileSize, unity_BaseInstanceID;

bool _HasLastPositionData;
bool _ForceNoMotion;
float _MotionVectorDepthBias;

const static float Pi = radians(180.0);

cbuffer UnityPerDraw
{
	float3x4 unity_ObjectToWorld, unity_WorldToObject;
	float4 unity_LODFade; // x is the fade value ranging within [0,1]. y is x quantized into 16 levels
	float4 unity_WorldTransformParams; // w is usually 1.0, or -1.0 for odd-negative scale transforms
	
	// Velocity
	float3x4 unity_MatrixPreviousM, unity_MatrixPreviousMI;
	
	//X : Use last frame positions (right now skinned meshes are the only objects that use this
	//Y : Force No Motion
	//Z : Z bias value
	//W : Camera only
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

float LinearEyeDepth(float depth)
{
	return 1.0 / (_ZBufferParams.z * depth + _ZBufferParams.w);
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
	float3x4 objectToWorld = (float3x4)unity_Builtins0Array[unity_BaseInstanceID + instanceID].unity_ObjectToWorldArray;
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
	float3x4 worldToObject = (float3x4)unity_Builtins0Array[unity_BaseInstanceID + instanceID].unity_WorldToObjectArray;
#else
	float3x4 worldToObject = unity_WorldToObject;
#endif
	
	return MultiplyVector(normal, (float3x3) worldToObject, doNormalize);
}

float EyeToDeviceDepth(float eyeDepth)
{
	return (1.0 - eyeDepth * _ZBufferParams.w) * rcp(eyeDepth * _ZBufferParams.z);
}

float3 ClipToWorld(float3 position)
{
	return MultiplyPointProj(_InvVPMatrix, position).xyz;
}

float3 PixelToWorld(float3 position)
{
	return ClipToWorld(float3(position.xy / _ScreenParams.xy * 2 - 1, position.z));
}

float4 WorldToClipNonJittered(float3 position) { return MultiplyPoint(_NonJitteredVPMatrix, position); }
float4 WorldToClipPrevious(float3 position) { return MultiplyPoint(_PreviousVPMatrix, position); }

float2 MotionVectorFragment(float4 nonJitteredPositionCS, float4 previousPositionCS)
{
	return (PerspectiveDivide(nonJitteredPositionCS).xy * 0.5 + 0.5) - (PerspectiveDivide(previousPositionCS).xy * 0.5 + 0.5);
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

uint GetShadowCascade(uint lightIndex, float3 lightPosition, out float3 positionLS)
{
	DirectionalLight light = _DirectionalLights[lightIndex];
	
	for (uint j = 0; j < light.cascadeCount; j++)
	{
		// find the first cascade which is not out of bounds
		matrix shadowMatrix = _DirectionalMatrices[light.shadowIndex + j];
		positionLS = MultiplyPoint3x4(shadowMatrix, lightPosition);
		if (all(saturate(positionLS) == positionLS))
			return j;
	}
	
	return ~0u;
}

float GetShadow(float3 worldPosition, uint lightIndex)
{
	DirectionalLight light = _DirectionalLights[lightIndex];
	if (light.shadowIndex == ~0u)
		return 1.0;
	
	float4 positionCS = PerspectiveDivide(WorldToClip(worldPosition));
	positionCS.xy = (positionCS.xy * 0.5 + 0.5) * _ScreenParams.xy;
	
	float2 jitter = _BlueNoise2D[uint2(positionCS.xy) % 128];
	float3 lightPosition = MultiplyPoint3x4(light.worldToLight, worldPosition);

	// PCS filtering
	float occluderDepth = 0.0, occluderWeightSum = 0.0;
	float goldenAngle = Pi * (3.0 - sqrt(5.0));
	for (uint k = 0; k < _BlockerSamples; k++)
	{
		float r = sqrt(k + 0.5) / sqrt(_BlockerSamples);
		float theta = k * goldenAngle + (1.0 - jitter.x) * 2.0 * Pi;
		float3 offset = float3(r * cos(theta), r * sin(theta), 0.0) * _BlockerRadius;
		
		float3 shadowPosition;
		uint cascade = GetShadowCascade(lightIndex, lightPosition + offset, shadowPosition);
		if (cascade == ~0u)
			continue;
		
		float4 texelAndDepthSizes = _DirectionalShadowTexelSizes[light.shadowIndex + cascade];
		float shadowZ = _DirectionalShadows.SampleLevel(_LinearClampSampler, float3(shadowPosition.xy, light.shadowIndex + cascade), 0);
		float occluderZ = Remap(1.0 - shadowZ, 0.0, 1.0, texelAndDepthSizes.z, texelAndDepthSizes.w);
		if (occluderZ >= lightPosition.z)
			continue;
		
		float weight = 1.0 - r * 0;
		occluderDepth += occluderZ * weight;
		occluderWeightSum += weight;
	}

	// There are no occluders so early out (this saves filtering)
	if (!occluderWeightSum)
		return 1.0;
	
	occluderDepth /= occluderWeightSum;
	
	float radius = max(0.0, lightPosition.z - occluderDepth) / _PcssSoftness;
	
	// PCF filtering
	float shadow = 0.0;
	float weightSum = 0.0;
	for (k = 0; k < _PcfSamples; k++)
	{
		float r = sqrt(k + 0.5) / sqrt(_PcfSamples);
		float theta = k * goldenAngle + jitter.y * 2.0 * Pi;
		float3 offset = float3(r * cos(theta), r * sin(theta), 0.0) * radius;
		
		float3 shadowPosition;
		uint cascade = GetShadowCascade(lightIndex, lightPosition + offset, shadowPosition);
		if (cascade == ~0u)
			continue;
		
		float weight = 1.0 - r;
		shadow += _DirectionalShadows.SampleCmpLevelZero(_LinearClampCompareSampler, float3(shadowPosition.xy, light.shadowIndex + cascade), shadowPosition.z) * weight;
		weightSum += weight;
	}
	
	return weightSum ? shadow / weightSum : 1.0;
}

float3 GetLighting(float3 normal, float3 worldPosition, float2 pixelPosition, float eyeDepth, bool isVolumetric = false)
{
	// Directional lights
	float3 lighting = 0.0;
	for (uint i = 0; i < min(_DirectionalLightCount, 4); i++)
	{
		float shadow = GetShadow(worldPosition, i);
		if(!shadow)
			continue;
		
		DirectionalLight light = _DirectionalLights[i];
		lighting += (isVolumetric ? 1.0 : saturate(dot(normal, light.direction))) * light.color * shadow;
	}
	
	uint3 clusterIndex;
	clusterIndex.xy = floor(pixelPosition) / _TileSize;
	clusterIndex.z = log2(eyeDepth) * _ClusterScale + _ClusterBias;
	
	uint2 lightOffsetAndCount = _LightClusterIndices[clusterIndex];
	uint startOffset = lightOffsetAndCount.x;
	uint lightCount = lightOffsetAndCount.y;
	
	// Point lights
	for (i = 0; i < min(128, lightCount); i++)
	{
		int index = _LightClusterList[startOffset + i];
		PointLight light = _PointLights[index];
		
		float3 lightVector = light.position - worldPosition;
		float sqrLightDist = dot(lightVector, lightVector);
		if (sqrLightDist > Sq(light.range))
			continue;
		
		float shadow = 1.0;
		if (light.shadowIndex != ~0u)
		{
			uint visibleFaces = light.visibleFaces;
			float dominantAxis = Max3(abs(lightVector * float3(-1, 1, -1)));
			float depth = rcp(dominantAxis) * light.far + light.near;
			shadow = _PointShadows.SampleCmpLevelZero(_LinearClampCompareSampler, float4(lightVector * float3(-1, 1, -1), light.shadowIndex), depth);
			
			if (!shadow)
				continue;
		}
		
		sqrLightDist = max(Sq(0.01), sqrLightDist);
		float rcpLightDist = rsqrt(sqrLightDist);
		
		#if 0
			// Realistic soft inverse square falloff
			float attenuation = rcpLightDist * Sq(saturate(1.0 - Sq(sqrLightDist / Sq(light.range))));
		#else
			// Legacy falloff, matches original game better
			float attenuation = Remap(light.range * rcp(3.0) * rcpLightDist, rcp(3.0));
		#endif
		
		lighting += (isVolumetric ? 1.0 : saturate(dot(normal, lightVector) * rcpLightDist)) * light.color * attenuation * shadow;
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

float4 SampleVolumetricLighting(float2 pixelPosition, float eyeDepth)
{
	float normalizedDepth = GetVolumetricUv(eyeDepth);
	float3 volumeUv = float3(pixelPosition / _ScreenParams.xy, normalizedDepth);
	return _VolumetricLighting.SampleLevel(_LinearClampSampler, volumeUv, 0.0);
}

bool3 IsInfOrNaN(float3 x) { return (asuint(x) & 0x7FFFFFFF) >= 0x7F800000; }

float3 ApplyFog(float3 color, float2 pixelPosition, float eyeDepth)
{
	if (!_FogEnabled)
		return color;
	
	float4 volumetricLighting = SampleVolumetricLighting(pixelPosition, eyeDepth);
	return color * volumetricLighting.a + volumetricLighting.rgb;
}

float2 UnjitterTextureUV(float2 uv)
{
	return uv - ddx_fine(uv) * _Jitter.x - ddy_fine(uv) * _Jitter.y;
}

float Luminance(float3 color)
{
	return dot(color, float3(0.2126729, 0.7151522, 0.0721750));
}

#endif