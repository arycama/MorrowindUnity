Shader "Terrain"
{
	Properties
	{
		_Control("Control", 2D) = "clear" {}
		_MainTex("Tex", 2DArray) = "" {}
	}

	SubShader
	{
		Pass
		{
			Name "Base"

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma target 5.0
			#pragma multi_compile_instancing
			#include "Terrain.hlsl"
			ENDHLSL
		}

		Pass
		{
			Name "Shadow"

			Colormask 0
			ZClip [_ZClip]

			Tags { "LightMode" = "ShadowCaster" }

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma multi_compile_instancing
			#include "Terrain.hlsl"
			ENDHLSL
		}
	}
}