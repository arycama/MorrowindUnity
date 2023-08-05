#ifndef COMMON_INCLUDED
#define COMMON_INCLUDED

float4 _Time, _FogColor;
float3 _Ambient, _SunDirection, _SunColor, _WorldSpaceCameraPos;
float _FogStartDistance, _FogEndDistance, _FogEnabled;
matrix unity_MatrixVP, _WorldToShadow;
SamplerState _LinearRepeatSampler, _PointClampSampler;
SamplerComparisonState _LinearClampCompareSampler;
Texture2D<float> _DirectionalShadows;

uint unity_BaseInstanceID;

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

float GetDirectionalShadow(float3 worldPosition)
{
	float4 shadowPosition = mul(_WorldToShadow, float4(worldPosition, 1.0));
	if (all(saturate(shadowPosition.xyz) == shadowPosition.xyz))
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

float3 ApplyFog(float3 color, float3 worldPosition, float dither)
{
	if (!_FogEnabled)
		return color;
	
	// Calculate extinction coefficient from color and distance
	
	float3 albedo = _FogColor.rgb;
	float threshold = 0.01;
	
	float samples = 32;
	
	float3 rayStart = _WorldSpaceCameraPos;
	float3 ray = worldPosition - rayStart;
	float3 rayStep = ray / samples;
	float ds = length(rayStep);
	
	float3 luminance = 0.0, shadowSum = 0.0, opticalDepth = 0.0;
	for (float i = dither; i < samples; i++)
	{
		float3 position = rayStep * i + rayStart;
		
		float atten = GetDirectionalShadow(position);
		
		// Treat the extinction as a spatially varying coefficient, based on linear fog.
		float rayDist = ds * i;
		
		if (rayDist > _FogStartDistance && rayDist < _FogEndDistance)
		{
			float extinction = rcp((_FogEndDistance - _FogStartDistance)) * exp(opticalDepth);
			luminance += extinction * atten * exp(-opticalDepth) * ds;
			opticalDepth += extinction * ds;
		}
		
		shadowSum += atten;
	}
	
	shadowSum /= samples;
	
	float viewDistance = distance(worldPosition, _WorldSpaceCameraPos);
	float fogOpacity = saturate((viewDistance - _FogStartDistance) / (_FogEndDistance - _FogStartDistance));
	
	luminance *= albedo;
	
	float3 fogTransmittance = 1.0 - fogOpacity;
	float3 fogLuminance = _FogColor * shadowSum * fogOpacity;
	//color *= fogTransmittance;
	//color += fogLuminance;
	
	color *= exp(-opticalDepth);
	color += luminance;
	
	return color;
}

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

#endif