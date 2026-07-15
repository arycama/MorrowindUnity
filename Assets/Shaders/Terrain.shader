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
			#pragma use_dxc
			#pragma multi_compile_instancing
			#include "Terrain.hlsl"
			ENDHLSL
		}

		Pass
		{
			Name "Shadow"

			Colormask 0
			ZClip [ZClip]

			Tags { "LightMode" = "ShadowCaster" }

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma use_dxc
			#pragma multi_compile_instancing
			#include "Terrain.hlsl"
			ENDHLSL
		}
	}
}