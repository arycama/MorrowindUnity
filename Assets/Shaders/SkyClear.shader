Shader "Hidden/SkyClear"
{
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

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#include "SkyClear.hlsl"
			ENDHLSL
		}
	}
}