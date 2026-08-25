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
			#pragma use_dxc
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
			#pragma use_dxc
			#define SHADOW
			#include "Terrain.hlsl"
			ENDHLSL
		}

		// Pass
  //       {
  //           Name "RaytracedTransmittance"
  //           Tags{ "LightMode" = "RaytracedTransmittance" }

  //           HLSLPROGRAM
  //           #pragma raytracing Raytracing
  //           #include "TerrainRaytracing.hlsl"
  //           ENDHLSL
  //       }

		// Pass
  //       {
  //           Name "RaytracedDiffuse"
  //           Tags{ "LightMode" = "RaytracedDiffuse" }

  //           HLSLPROGRAM
  //           #pragma raytracing Raytracing
  //           #include "TerrainRaytracingLuminance.hlsl"
  //           ENDHLSL
  //       }
	}
}