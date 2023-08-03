#ifndef COMMON_INCLUDED
#define COMMON_INCLUDED

float3 _Ambient, _FogColor, _SunDirection, _SunColor;
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

#endif