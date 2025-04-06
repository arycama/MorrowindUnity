Shader "Water" 
{
	Properties 
	{
		_Alpha ("Alpha", Range(0, 1)) = 0.75
		_MainTex ("Fallback texture", 2D) = "black" {}
		_Fade("Blend parameter", Float) = 0.15
		_Tiling("Tiling", Float) = 1024
	}

	SubShader
	{
		Tags { "Queue"="Transparent" }

		Pass
		{
			Name "Base"

			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma multi_compile_instancing
			#include "Water.hlsl"
			ENDHLSL
		}
	}
}