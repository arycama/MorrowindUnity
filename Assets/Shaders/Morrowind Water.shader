Shader"Morrowind/Water" 
{
	Properties 
	{
		_Alpha ("Alpha", Range(0, 1)) = 0.75
		_MainTex ("Fallback texture", 2D) = "black" {}
		_Fade("Blend parameter", Float) = 0.15
		_Tiling("Tiling", Float) = 1024
	}

	SubShader
	{
		Tags {"Queue"="Transparent" "RenderType"="Transparent"}
		Offset 1, 0

		CGPROGRAM
		#pragma surface surf Lambert vertex:vert fullforwardshadows alpha:fade

		#include "UnityCG.cginc"
		#include "Autolight.cginc"

		float _Alpha, _Fade, _Tiling;
		sampler2D _MainTex;
		sampler2D_float _CameraDepthTexture;

		struct Input
		{
			float4 screenPos;
			float2 texcoord;
		};

		void vert(inout appdata_full v, out Input o)
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);
			float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
			o.texcoord = worldPos.xz * _Tiling;
		}

		void surf(Input IN, inout SurfaceOutput o)
		{
			o.Albedo = tex2D(_MainTex, IN.texcoord);
			o.Alpha = _Alpha;

			//#ifdef SHADOWS_SCREEN
			//	half depth = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(IN.screenPos));
			//	depth = LinearEyeDepth(depth);
			//	half edgeBlendFactors = saturate((depth - IN.screenPos.w) * _Fade);
			//	o.Alpha *= edgeBlendFactors;
			//#endif
		}

		ENDCG
	}
}