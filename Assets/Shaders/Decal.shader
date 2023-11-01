Shader "Decal" 
{
	Properties 
	{
		_Color("Albedo", Color) = (1, 1, 1, 1)
		_MainTex ("Texture", 2D) = "black" {}
	}

	SubShader
	{
		Tags { "Queue"="Transparent" }

		Pass
		{
			Blend One SrcAlpha
			ZWrite Off

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma target 5.0

			#include "Decal.hlsl"
			ENDHLSL
		}
	}
}