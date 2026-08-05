Shader "Water" 
{
	Properties 
	{
		Albedo("Albedo", Color) = (1, 1, 1, 1)
		[HDR] Extinction("Extinction", Color) = (1, 1, 1, 1)
		_Alpha ("Alpha", Range(0, 1)) = 0.75
		_MainTex ("Fallback texture", 2D) = "black" {}
		_Fade("Blend parameter", Float) = 0.15
		_Tiling("Tiling", Float) = 1024
	}

	SubShader
	{
		Tags { "Queue" = "Transparent" }

		Pass
		{
			Tags { "LightMode" = "Forward" }

			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma use_dxc
			#pragma require waveMath
			#pragma multi_compile_instancing
			#pragma multi_compile _ VOLUMETRIC_LIGHT_ON
			#pragma multi_compile _ SHADOWS_ON
            #pragma multi_compile _ POINT_LIGHTS_ON
			#define FORWARD
			#include "Water.hlsl"
			ENDHLSL
		}
	}
}