Shader "Morrowind/Sky"
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
		//Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off

		Tags { "Queue"="Background" "PreviewType"="Skybox" }

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
				float4 color : TEXCOORD2;
			};

			sampler2D _MainTex, _FadeTexture;
			float4 _MainTex_ST;
			float4 _SkyColor;
			float _CloudSpeed, _TimeOfDay, _LerpFactor, _Alpha;
			
			v2f vert (appdata v)
			{
				v2f o;
				//o.pos = UnityObjectToClipPos(_WorldSpaceCameraPos.xyz + v.vertex);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex) + _CloudSpeed * _Time.y * 0.003;
				o.pos.z /= o.pos.w;

				o.color.a = v.vertex.y * 0.005 - 0.5;
				o.color.rgb = lerp(unity_FogColor, _SkyColor, o.color.a);

				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				float4 color = tex2D(_MainTex, i.uv);
				float4 fadeTex = tex2D(_FadeTexture, i.uv);

				// Fade between the two textures based on transition factor
				color = lerp(color, fadeTex, _LerpFactor);

				color.rgb = lerp(i.color.rgb, color.rgb * unity_FogColor, color.a * i.color.a);
				color.a = saturate(_SkyColor.a + color.a);

				return color;
			}

			ENDCG
		}
	}
}