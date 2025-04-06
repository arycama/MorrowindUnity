Shader"Morrowind/Diffuse"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo", 2D) = "white" {}

		_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

		_EmissionColor("Emission Color", Color) = (0,0,0)
		_EmissionMap("Emission", 2D) = "white" {}

		_AmbientColor("Ambient Color", Color) = (1, 1, 1, 1)

		// Blending state
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("__src", Float) = 1.0
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("__dst", Float) = 0.0

		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("__zt", Float) = 4.0
		_ZWrite ("__zw", Float) = 1.0

		_InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0
	}

	SubShader
	{
		Tags{"RenderType"="Opaque"}

		Pass
		{
			Tags{"LightMode"="Vertex"}
			Blend [_SrcBlend] [_DstBlend]
			ZTest [_ZTest]
			ZWrite [_ZWrite]

			CGPROGRAM
			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			#pragma vertex vert
			#pragma fragment frag

			#include "MorrowindDiffuseVertex.cginc"

			ENDCG
		}

		Pass
		{
			Tags { "LightMode" = "ForwardBase" }
			Blend [_SrcBlend] [_DstBlend]
			ZTest [_ZTest]
			ZWrite [_ZWrite]

			CGPROGRAM
			#pragma multi_compile_fog
			#pragma multi_compile_fwdbase
			#pragma multi_compile_instancing

			#pragma vertex vert
			#pragma fragment frag

			#include "MorrowindDiffuseBase.cginc"

			ENDCG
		}

		Pass
		{
			Tags { "LightMode" = "ForwardAdd" }
			Blend [_SrcBlend] One
			ZWrite Off

			CGPROGRAM

			#pragma multi_compile_fog
			#pragma multi_compile_fwdadd_fullshadows
			#pragma multi_compile_instancing

			#pragma vertex vert
			#pragma fragment frag

			#include "MorrowindDiffuseAdd.cginc"
			ENDCG
		}

		Pass
		{
			Colormask 0
            Tags { "LightMode" = "ShadowCaster" }

            CGPROGRAM
			#pragma multi_compile_instancing
			#pragma multi_compile_shadowcaster

			#pragma multi_compile _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON

            #pragma vertex vertShadowCaster
            #pragma fragment fragShadowCaster

			#include "MorrowindShadow.cginc"
			ENDCG
		}
	}
}