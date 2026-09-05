Shader "Surface"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo", 2D) = "white" {}

		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

		_EmissionColor("Emission Color", Color) = (0,0,0)
		_EmissionMap("Emission", 2D) = "white" {}

		_AmbientColor("Ambient Color", Color) = (1, 1, 1, 1)

		// Blending state
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("__src", Float) = 1.0
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("__dst", Float) = 0.0

		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("__zt", Float) = 4.0
		_ZWrite ("__zw", Float) = 1.0

		_InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0
	}

	SubShader
	{
		Pass
		{
            Tags { "LightMode" = "GBuffer" }

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma multi_compile_instancing
			#define GBUFFER
			#include "Surface.hlsl"
			ENDHLSL
		}

		Pass
		{
            Tags { "LightMode" = "Forward" }

			Blend [_SrcBlend] [_DstBlend]
			ZTest [_ZTest]
			ZWrite [_ZWrite]

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma require WaveMath
			#pragma multi_compile_instancing
			#pragma multi_compile _ VOLUMETRIC_LIGHT_ON
			#pragma multi_compile _ SHADOWS_ON
            #pragma multi_compile _ POINT_LIGHTS_ON
			#define FORWARD
			#include "Surface.hlsl"
			ENDHLSL
		}

		Pass
		{
			Colormask 0
			ZClip [ZClip]

            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
			#pragma multi_compile_instancing
			#pragma multi_compile _ _ALPHABLEND_ON
			#define SHADOW
			#include "Surface.hlsl"
			ENDHLSL
		}

		Pass
        {
            Name "RaytracedLuminance"

			Tags{ "LightMode" = "RayTracing" }

            HLSLPROGRAM
            #pragma raytracing Raytracing
			#pragma multi_compile _ _ALPHABLEND_ON
			#pragma max_recursion_depth 2
            #include "SurfaceRaytracingLuminance.hlsl"
            ENDHLSL
        }

		Pass
        {
            Name "RaytracedTransmittance"

			Tags{ "LightMode" = "RayTracing" }

            HLSLPROGRAM
            #pragma raytracing Raytracing
			#pragma multi_compile _ _ALPHABLEND_ON
			#pragma max_recursion_depth 2
            #include "SurfaceRaytracing.hlsl"
            ENDHLSL
        }
	}
}