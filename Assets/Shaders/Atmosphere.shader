Shader "Atmosphere"
{
	Properties
	{
		//_Color ("Sky Color", Color) = (1, 1, 1, 1)
		_MainTex ("Texture", 2D) = "white" {}
		_FadeTexture ("Fade Texture", 2D) = "white" {}
		_CloudSpeed ("Cloud Speed", Float) = 1
		_SunColor ("Sun Color", Color) = (1, 1, 1, 1)
		_SunSize ("Sun Size", Float) = 1
		_LerpFactor ("Lerp Factor", Float) = 0
	}

	SubShader
	{
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
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