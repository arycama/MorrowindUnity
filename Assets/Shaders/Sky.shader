﻿Shader "Sky"
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
        Cull Off
        ZWrite Off
		ZTest Always

		Pass
		{
			Stencil
            {
                Ref 0
				Comp Equal
            }

            Tags { "LightMode" = "Sky" }

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#include "Sky.hlsl"
			ENDHLSL
		}
	}
}