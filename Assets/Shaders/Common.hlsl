#ifndef COMMON_INCLUDED
#define COMMON_INCLUDED

SamplerComparisonState sampler_DirectionalShadows;
Texture2D<float> _DirectionalShadows;
Buffer<uint> _LightClusterList;
Texture3D<uint2> _LightClusterIndices;

cbuffer UnityPerFrame
{
	float3 _AmbientLight, _FogColor, _SunDirection, _SunColor;
	float _FogStartDistance, _FogEndDistance, _FogEnabled, _Time;
};

cbuffer UnityPerCamera
{
	matrix unity_MatrixVP, _WorldToShadow;
};

cbuffer UnityPerDraw
{
	float3x4 unity_ObjectToWorld, unity_WorldToObject;
	float4 unity_LODFade; // x is the fade value ranging within [0,1]. y is x quantized into 16 levels
	float4 unity_WorldTransformParams; // w is usually 1.0, or -1.0 for odd-negative scale transforms
};

float3 MultiplyPoint3x4(float3x4 mat, float3 p)
{
	return p.x * mat._m00_m10_m20 + (p.y * mat._m01_m11_m21 + (p.z * mat._m02_m12_m22 + mat._m03_m13_m23));
}

float4 MultiplyPoint(float4x4 mat, float3 p)
{
	return p.x * mat._m00_m10_m20_m30 + (p.y * mat._m01_m11_m21_m31 + (p.z * mat._m02_m12_m22_m32 + mat._m03_m13_m23_m33));
}

float3 ObjectToWorld(float3 position)
{
	return MultiplyPoint3x4(unity_ObjectToWorld, position);
}

float4 WorldToClip(float3 position)
{
	return MultiplyPoint(unity_MatrixVP, position);
}

float4 ObjectToClip(float3 position)
{
	return WorldToClip(ObjectToWorld(position));
}

float3 ObjectToWorldNormal(float3 normal)
{
	// Source: https://www.shadertoy.com/view/3s33zj
	float3x4 m = unity_ObjectToWorld;
	float3x3 adjoint = float3x3(cross(m[1].xyz, m[2].xyz), cross(m[2].xyz, m[0].xyz), cross(m[0].xyz, m[1].xyz));
	return normalize(mul(adjoint, normal));
}

float4 BilinearWeights(float2 uv, float2 textureSize)
{
	float2 localUv = frac(uv * textureSize - 0.5 + rcp(512.0));
	float4 weights = localUv.xxyy * float4(-1, 1, 1, -1) + float4(1, 0, 0, 1);
	return weights.zzww * weights.xyyx;
}

#endif