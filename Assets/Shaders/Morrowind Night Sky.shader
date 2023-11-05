Shader "Morrowind/Night Sky"
{
	Properties
	{
		_Color ("Sky Color", Color) = (1, 1, 1, 1)
		_MainTex ("Texture", 2D) = "white" {}
		_FadeTexture ("Fade Texture", 2D) = "white" {}
		_CloudSpeed ("Cloud Speed", Float) = 1
		_SunColor ("Sun Color", Color) = (1, 1, 1, 1)
		_SunSize ("Sun Size", Float) = 1
		_LerpFactor ("Lerp Factor", Float) = 0
	}

	SubShader
	{
		Tags { "Queue"="Background" "PreviewType"="Skybox" }
		ZWrite Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				fixed fog : TEXCOORD2;
			};

			sampler2D _MainTex, _FadeTexture;

			cbuffer UnityPerMaterial
			{
				float4 _MainTex_ST;
				float4 _Color;
				float _CloudSpeed, _TimeOfDay, _LerpFactor;
			};
			
			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(_WorldSpaceCameraPos.xyz + v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.pos.z /= o.pos.w;

				o.fog = v.vertex.y * 0.005 - 0.5;

				return o;
			}

			//(end-z)/(end-start) = z * (-1/(end-start)) + (end/(end-start))
			
			float4 frag (v2f i) : SV_Target
			{
				float4 color = tex2D(_MainTex, i.uv);
				//float4 fadeTex = tex2D(_FadeTexture, i.uv);
				
				// Fade between the two textures based on transition factor
				//color = lerp(color, fadeTex, _LerpFactor);

				//color.rgb = lerp(unity_FogColor, color.rgb, i.fog);

				return color;
			}

			ENDCG
		}
	}
}