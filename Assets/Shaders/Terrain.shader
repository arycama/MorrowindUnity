Shader"Terrain"
{
	Properties
	{
		_Control("Control", 2D) = "clear" {}
		_MainTex("Tex", 2DArray) = "" {}
	}

	SubShader
	{
		Tags
		{
			"Queue"="Geometry-100"
		}

		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma target 5.0

			#include "Terrain.hlsl"
			ENDHLSL
		}

		Pass
		{
			Colormask 0
			ZClip[_ZClip]

			Tags { "LightMode" = "ShadowCaster" }

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma target 5.0

			#include "Terrain.hlsl"
			ENDHLSL
		}
	}
}