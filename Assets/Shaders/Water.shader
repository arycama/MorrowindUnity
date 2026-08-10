Shader "Water" 
{
	Properties 
	{
		Albedo("Albedo", Color) = (1, 1, 1, 1)
		[HDR] Extinction("Extinction", Color) = (1, 1, 1, 1)
		Alpha ("Alpha", Range(0, 1)) = 0.75
		Scale("Scale", Float) = 0.2105
		_MainTex ("Texture", 2D) = "black" {}
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