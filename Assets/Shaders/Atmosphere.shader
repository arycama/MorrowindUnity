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
			#pragma use_dxc
			#pragma multi_compile _ VOLUMETRIC_LIGHT_ON
			#include "Atmosphere.hlsl"
			ENDHLSL
		}
	}
}