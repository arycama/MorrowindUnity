Shader "Terrain"
{
	Properties
	{
		_Control("Control", 2D) = "clear" {}
		_MainTex("Tex", 2DArray) = "" {}
	}

	SubShader
	{
		HLSLINCLUDE
		#pragma vertex Vertex
		#pragma fragment Fragment
		#pragma use_dxc
		#pragma require waveMath
		#pragma multi_compile_instancing
		ENDHLSL

		Pass
		{
			Tags { "LightMode" = "Gbuffer" }

			HLSLPROGRAM
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
			#define SHADOW
			#include "Terrain.hlsl"
			ENDHLSL
		}
	}
}