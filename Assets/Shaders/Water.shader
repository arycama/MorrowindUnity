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
		Offset 1, 0

		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma target 5.0

			#include "Water.hlsl"
			ENDHLSL
		}
	}
}