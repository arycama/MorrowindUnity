#ifndef COMMON_INCLUDED
#define COMMON_INCLUDED

float4 _Time, _ProjectionParams, _ZBufferParams, _ScreenParams;
float3 _AmbientLightColor, _SunDirection, _SunColor, _WorldSpaceCameraPos, _FogColor;
float _FogStartDistance, _FogEndDistance, _FogEnabled, _SunShadowsOn;
matrix unity_MatrixVP, _WorldToShadow;
SamplerState _LinearClampSampler, _LinearRepeatSampler, _PointClampSampler;
SamplerComparisonState _LinearClampCompareSampler;
Texture2D<float> _DirectionalShadows, _BlueNoise1D;
Texture3D<float4> _VolumetricLighting;
matrix _InvViewProjectionMatrix, _PreviousViewProjectionMatrix;

// Vol fog
float _VolumeWidth, _VolumeHeight, _VolumeSlices, _VolumeDepth, _NonLinearDepth;
uint unity_BaseInstanceID, _FrameCount;

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

struct PointLight
{
	float3 position;
	float range;
	float3 color;
	uint shadowIndex;
};

StructuredBuffer<PointLight> _PointLights;
uint _PointLightCount;

float Sq(float x)
{
	return x * x;
}

// Remaps a value from one range to another
float1 Remap(float1 v, float1 pMin, float1 pMax = 1.0, float1 nMin = 0.0, float1 nMax = 1.0) { return nMin + (v - pMin) * rcp(pMax - pMin) * (nMax - nMin); }
float2 Remap(float2 v, float2 pMin, float2 pMax = 1.0, float2 nMin = 0.0, float2 nMax = 1.0) { return nMin + (v - pMin) * rcp(pMax - pMin) * (nMax - nMin); }
float3 Remap(float3 v, float3 pMin, float3 pMax = 1.0, float3 nMin = 0.0, float3 nMax = 1.0) { return nMin + (v - pMin) * rcp(pMax - pMin) * (nMax - nMin); }
float4 Remap(float4 v, float4 pMin, float4 pMax = 1.0, float4 nMin = 0.0, float4 nMax = 1.0) { return nMin + (v - pMin) * rcp(pMax - pMin) * (nMax - nMin); }

float3 ObjectToWorld(float3 position, uint instanceID)
{
#ifdef INSTANCING_ON
		float4x4 objectToWorld = unity_Builtins0Array[unity_BaseInstanceID + instanceID].unity_ObjectToWorldArray;
#else
	float4x4 objectToWorld = unity_ObjectToWorld;
#endif
	
	return mul(objectToWorld, float4(position, 1.0)).xyz;
}

float4 WorldToClip(float3 position)
{
	return mul(unity_MatrixVP, float4(position, 1.0));
}

float4 ObjectToClip(float3 position, uint instanceID)
{
	return WorldToClip(ObjectToWorld(position, instanceID));
}

float3 ObjectToWorldNormal(float3 normal, uint instanceID)
{
#ifdef INSTANCING_ON
		float4x4 worldToObject = unity_Builtins0Array[unity_BaseInstanceID + instanceID].unity_WorldToObjectArray;
#else
	float4x4 worldToObject = unity_WorldToObject;
#endif
	
	return mul(normal, (float3x3) worldToObject);
}

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

float GetDirectionalShadow(float3 worldPosition)
{
	// Interesct with ground plane
	//if (IntersectRayPlane(worldPosition, _SunDirection, 0, float3(0, 1, 0)))
	//	return 0.0;
	
	float3 shadowPosition = mul(_WorldToShadow, float4(worldPosition, 1.0)).xyz;
	if (_SunShadowsOn && all(saturate(shadowPosition) == shadowPosition))
		return _DirectionalShadows.SampleCmpLevelZero(_LinearClampCompareSampler, shadowPosition.xy, shadowPosition.z);
	
	return 1.0;
}

float3 GetLighting(float3 normal, float3 worldPosition)
{
	float3 lighting = saturate(dot(normal, _SunDirection)) * _SunColor;
	lighting *= GetDirectionalShadow(worldPosition);
	
	// Point lights
	for (uint i = 0; i < _PointLightCount; i++)
	{
		PointLight pointLight = _PointLights[i];
		
		float3 unnormalizedLightVector = (pointLight.position - worldPosition) / 69.99125109;
		float sqrDist = dot(unnormalizedLightVector, unnormalizedLightVector);
		float rcpDist = rcp(sqrDist);
		float3 L = unnormalizedLightVector * rcpDist;
		float attenuation = rcpDist;
		attenuation = min(rcp(Sq(0.01)), attenuation);
		attenuation *= Sq(saturate(1.0 - Sq(sqrDist / Sq(pointLight.range / 69.99125109))));
		lighting += saturate(dot(normal, L)) * pointLight.color * attenuation;
	}
	
	return lighting;
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

float4 PerspectiveDivide(float4 input)
{
	return float4(input.xyz * rcp(input.w), input.w);
}

float4 MultiplyPoint(float3 p, float4x4 mat)
{
	return p.x * mat[0] + (p.y * mat[1] + (p.z * mat[2] + mat[3]));
}

float4 MultiplyPointProj(float4x4 mat, float3 p)
{
	return PerspectiveDivide(mul(mat, float4(p, 1.0)));
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
	positionCS.y = 1 - positionCS.y;
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
	
	// Calculate extinction coefficient from color and distance
	// TODO: Assume fog is white, and fogcolor is actually lighting.. might not work for interiors though? Could do some kind of... dirlight/fog color or something
	float3 albedo = _FogColor;//	saturate(_FogColor / _SunColor);
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
		float3 lighting = 0.0;
		for (uint j = 0; j < _PointLightCount; j++)
		{
			PointLight pointLight = _PointLights[j];
		
			float3 unnormalizedLightVector = (pointLight.position - position) / 69.99125109;
			float sqrDist = dot(unnormalizedLightVector, unnormalizedLightVector);
			float rcpDist = rcp(sqrDist);
			float3 L = unnormalizedLightVector * rcpDist;
			float attenuation = rcpDist;
			attenuation = min(rcp(Sq(0.01)), attenuation);
			attenuation *= Sq(saturate(1.0 - Sq(sqrDist / Sq(pointLight.range / 69.99125109))));
			
			//if (sqrDist < Sq(pointLight.range / 69.99125109))
				lighting += pointLight.color * attenuation;
		}
		
		float extinction = 0.0;
		if (rayDist > _FogStartDistance && rayDist < _FogEndDistance)
			extinction = rcp(_FogEndDistance - rayDist);
		
		float atten = GetDirectionalShadow(position);
		float3 luminance = albedo * extinction * (atten + lighting);
		
		float transmittance = exp(-extinction * ds);
		float3 integScatt = luminance * (1.0 - transmittance) / max(extinction, 1e-7);
		
		result.rgb += integScatt * result.a;
		result.a *= transmittance;
	}
	
	return color * result.a + result.rgb;
}

#endif