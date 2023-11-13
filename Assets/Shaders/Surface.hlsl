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
		output.color = _AmbientLightColor * input.color + _EmissionColor;
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
		float4 color = _MainTex.Sample(_TrilinearRepeatAniso16Sampler, input.uv);
	#endif
	
	#ifdef _ALPHATEST_ON
		clip(color.a - _Cutoff);
	#endif
	
	FragmentOutput output;
	
	#if defined(UNITY_PASS_SHADOWCASTER) 
		#ifdef _ALPHABLEND_ON
			//clip(color.a - 0.5);
			//clip(color.a - InterleavedGradientNoise(input.position.xy, 0));
			clip(color.a - _BlueNoise1D[uint2(input.position.xy) % 128]);
		#endif
	#else
		float3 normal = normalize(input.normal);
		float3 lighting = GetLighting(normal, input.worldPosition);

		lighting += input.color;
		color.rgb *= lighting;
		color.rgb = ApplyFog(color.rgb, input.worldPosition, InterleavedGradientNoise(input.position.xy, 0));
		output.color = color;
	
		#ifdef MOTION_VECTORS_ON
	//if (unity_MotionVectorsParams.y)
	//{
	//	float2 nonJitteredPosition = input.position.xy / _ScreenParams.xy; // Convert to NDC (0 to 1)
	//	nonJitteredPosition = 2.0 * nonJitteredPosition - 1.0; // Convert to clip space (-1 to 1)
	//	nonJitteredPosition.y = -nonJitteredPosition.y;
		
	//	float3 worldPos = MultiplyPointProj(_InvVPMatrix, float3(nonJitteredPosition, input.position.z));
		
	//	float4 nonClip = WorldToClipNonJittered(worldPos);
		
	//	//nonClip = input.nonJitteredPositionCS;
	//	nonClip.xy /= nonClip.w;
	//	nonClip.y = -nonClip.y;
	//	nonClip.xy = 0.5 * nonClip.xy + 0.5;
	//	nonJitteredPosition = nonClip.xy;
		
	//	nonJitteredPosition.xy = (((input.nonJitteredPositionCS.xy / input.nonJitteredPositionCS.w * 0.5 + 0.5) * _ScreenParams.xy) - _Jitter) / _ScreenParams.xy;
	//	nonJitteredPosition.y = 1 - nonJitteredPosition.y;
		
	//	//nonJitteredPosition -= (2.0 * _Jitter / _ScreenParams.xy); // Apply jitter offset
			
	//	//nonJitteredPosition /= input.position.w; // Redo perspectiev divide
	//	//nonJitteredPosition = nonJitteredPosition * 0.5 + 0.5; // Convert to NDC (0 to 1)
			
	//	float4 prevPosCS = input.previousPositionCS;
		
		
	//	// Flip due to matrix stupidity
	//	prevPosCS.y = -prevPosCS.y;
	//	output.velocity = nonJitteredPosition - (PerspectiveDivide(prevPosCS).xy * 0.5 + 0.5);
	//}
	//else
	//{
	//	output.velocity = 0.0;
	//}
		
	output.velocity = unity_MotionVectorsParams.y ? MotionVectorFragment(input.nonJitteredPositionCS, input.previousPositionCS) : 0.0;
		#endif
	#endif
	
	return output;
}