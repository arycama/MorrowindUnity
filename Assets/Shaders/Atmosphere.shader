Shader "Atmosphere"
{
	SubShader
	{
		Pass
		{
			
			ZWrite Off
			ZClip Off

			Tags { "LightMode" = "Sky" }

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma multi_compile _ VOLUMETRIC_LIGHT_ON
			#pragma require WaveMath
			#include "Atmosphere.hlsl"
			ENDHLSL
		}
	}
}