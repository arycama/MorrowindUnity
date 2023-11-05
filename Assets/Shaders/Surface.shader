Shader"Surface"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo", 2D) = "white" {}

		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

		_EmissionColor("Emission Color", Color) = (0,0,0)
		_EmissionMap("Emission", 2D) = "white" {}

		_Ambient("Ambient", Color) = (1, 1, 1, 1)
		_Specular("Specular", Color) = (1, 1, 1, 1)
		_Glossiness("Glossiness", Float) = 0.0

		// Blending state
		[Toggle] _AlphaTest("Alpha Test", Float) = 0
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1.0
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0.0

		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4.0
		_ZWrite ("Z Write", Float) = 1.0

		_InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0
	}

	SubShader
	{
		Pass
		{
			Name "Base Pass"

			Stencil
            {
                Ref 1
                Pass Replace
            }

			Blend [_SrcBlend] [_DstBlend]
			ZTest [_ZTest]
			ZWrite [_ZWrite]

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma target 5.0

			#pragma multi_compile_instancing
			#pragma multi_compile _ _ALPHATEST_ON _ALPHABLEND_ON

			#include "Surface.hlsl"
			ENDHLSL
		}

		Pass
		{
			Name "Motion Vectors"
            Tags { "LightMode" = "MotionVectors" }

			Stencil
            {
                Ref 3
                Pass Replace
            }

			HLSLPROGRAM
			#pragma vertex Vertex
			#pragma fragment Fragment
			#pragma target 5.0

			#pragma multi_compile_instancing
			#pragma multi_compile _ _ALPHATEST_ON

			#define MOTION_VECTORS_ON

			#include "Surface.hlsl"
			ENDHLSL
		}

		Pass
		{
			Name "Shadow Caster"

			Colormask 0
			ZClip[_ZClip]

            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
			#pragma target 5.0

			#pragma multi_compile_instancing
			#pragma multi_compile _ _ALPHATEST_ON _ALPHABLEND_ON

			#include "Surface.hlsl"
			ENDHLSL
		}
	}
}