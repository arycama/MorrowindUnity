#ifdef __INTELLISENSE__
	#define MOTION_VECTORS_ON
#endif

#include "Common.hlsl"

struct VertexInput
{
	uint instanceID : SV_InstanceID;
	float3 position : POSITION;
	
	#if !defined(UNITY_PASS_SHADOWCASTER) || defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON)
		float2 uv : TEXCOORD;
	#endif
	
	#ifdef MOTION_VECTORS_ON
		float3 previousPosition : TEXCOORD4;
	#endif
	
	#ifndef UNITY_PASS_SHADOWCASTER
		float3 normal : NORMAL;
		float3 color : COLOR;
	#endif
};

struct FragmentInput
{
	float4 position : SV_Position;
	
#if !defined(UNITY_PASS_SHADOWCASTER) || defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON)
		float2 uv : TEXCOORD;
	#endif
	
	#ifndef UNITY_PASS_SHADOWCASTER
		float3 worldPosition : POSITION1;
		float3 normal : NORMAL;
		float3 color : COLOR;
	#endif
	
	#ifdef MOTION_VECTORS_ON
		float4 nonJitteredPositionCS : POSITION2;
		float4 previousPositionCS : POSITION3;
	#endif
};

struct FragmentOutput
{
	#ifndef UNITY_PASS_SHADOWCASTER
		#ifdef _ALPHABLEND_ON
			float4 color : SV_Target0;
		#else
			float3 color : SV_Target0;
		#endif
	#endif
	
	#ifdef MOTION_VECTORS_ON
		float2 velocity : SV_Target1;
	#endif
};

cbuffer UnityPerMaterial
{
	float4 _Color, _MainTex_ST;
	float3 _EmissionColor;
	float _Cutoff;
};

Texture2D _MainTex, _EmissionMap;

FragmentInput Vertex(VertexInput input)
{
	float3 worldPosition = ObjectToWorld(input.position, input.instanceID);
	
	FragmentInput output;
	output.position = WorldToClip(worldPosition);
	
	#if !defined(UNITY_PASS_SHADOWCASTER) || defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON)
		output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
	#endif
	
	#ifndef UNITY_PASS_SHADOWCASTER
		output.worldPosition = worldPosition;
		output.normal = ObjectToWorldNormal(input.normal, input.instanceID);
	output.color = (_AmbientLightColor * input.color + _EmissionColor) * _Exposure;
	#endif
	
	#ifdef MOTION_VECTORS_ON
		output.nonJitteredPositionCS = WorldToClipNonJittered(worldPosition);
		output.previousPositionCS = WorldToClipPrevious(MultiplyPoint3x4(unity_MatrixPreviousM, unity_MotionVectorsParams.x ? input.previousPosition : input.position));
	#endif
	
	return output;
}

FragmentOutput Fragment(FragmentInput input)
{
	#if !defined(UNITY_PASS_SHADOWCASTER)
		input.uv = UnjitterTextureUV(input.uv);
	#endif
	
	#if !defined(UNITY_PASS_SHADOWCASTER) || defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON)
		float4 color = _MainTex.SampleBias(_TrilinearRepeatAniso16Sampler, input.uv, _MipBias);
	#endif
	
	#ifdef _ALPHATEST_ON
		clip(color.a - _Cutoff);
	#endif
	
	FragmentOutput output;
	
	#if defined(UNITY_PASS_SHADOWCASTER) 
		#ifdef _ALPHABLEND_ON
			clip(color.a - InterleavedGradientNoise(input.position.xy, 0));
		#endif
	#else
		float3 normal = normalize(input.normal);
		float3 lighting = GetLighting(normal, input.worldPosition, input.position.xy, input.position.w, color.rgb, 0.0, 1.0);
		lighting += input.color * color.rgb;
		
		#ifndef _ALPHABLEND_ON
		if (!_AoEnabled)
		#endif
			lighting = ApplyFog(lighting, input.position.xy, input.position.w);
			
		output.color.rgb = lighting;
		
		#ifdef _ALPHABLEND_ON
			output.color.a = color.a;
		#endif
	
		#ifdef MOTION_VECTORS_ON
			output.velocity = unity_MotionVectorsParams.y ? MotionVectorFragment(input.nonJitteredPositionCS, input.previousPositionCS) : 0.0;
		#endif
	#endif
	
	return output;
}