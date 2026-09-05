Shader "Terrain"
{
	Properties
	{
		[NoScaleOffset] _Control("Control", 2D) = "clear" {}
		_MainTex("Tex", 2DArray) = "" {}
	}

	SubShader
	{
		Pass
		{
			Tags { "LightMode" = "Terrain" }

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#define GBUFFER
			#include "Terrain.hlsl"
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
			#define SHADOW
			#include "Terrain.hlsl"
			ENDHLSL
		}

		Pass
        {
            Name "RaytracedLuminance"

			Tags{ "LightMode" = "RayTracing" }

            HLSLPROGRAM
            #pragma raytracing Raytracing
			#pragma max_recursion_depth 2
            #include "TerrainRaytracingLuminance.hlsl"
            ENDHLSL
        }

		Pass
        {
            Name "RaytracedTransmittance"

			Tags{ "LightMode" = "RayTracing" }

            HLSLPROGRAM
            #pragma raytracing Raytracing
			#pragma max_recursion_depth 2
            #include "TerrainRaytracing.hlsl"
            ENDHLSL
        }
	}
}