Shader"Clouds" 
{
	Properties 
	{
		_MainTex("Cloud Texture", 2D) = "clear" {}
		_Color("Sky Color", Color) = (0.5, 0.5, 0.75, 1)

		_FadeTexture ("Fade Texture", 2D) = "white" {}
		_CloudSpeed ("Cloud Speed", Float) = 1

		_HeightFactor("Height Factor", Float) = 1
		_LerpFactor ("Lerp Factor", Float) = 0
	}

	SubShader 
	{
		Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
		Cull Off 
		ZWrite Off

		Pass 
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			sampler2D _MainTex, _FadeTexture;

			cbuffer UnityPerMaterial
			{
				float4 _MainTex_ST, _Color;
				float _HeightFactor, _CloudSpeed, _LerpFactor;
			};

			#include "UnityCG.cginc"

			struct v2f
			{
				float4 pos : SV_POSITION;
				float3 uv : TEXCOORD;
				float height : TEXCOORD1;
			};

			v2f vert(appdata_base v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.vertex;
				o.height = pow(v.vertex.y, _HeightFactor);
				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				float2 uv = (i.uv.xz / i.uv.y) * _MainTex_ST.xy + _MainTex_ST.zw * _CloudSpeed * _Time.y;

				float4 clouds = tex2D(_MainTex, uv);
				float4 fadeTex = tex2D(_FadeTexture, uv);

				// Fade between the two textures based on transition factor
				float4 color = lerp(clouds, fadeTex, _LerpFactor);

				// Cloud texture is premultiplied by fog texture
				color.rgb *= unity_FogColor.rgb;

				// Blend the sky color with the cloud texture
				color.rgb = lerp(_Color.rgb, color.rgb, color.a);

				// Finally blend the sky with the fog color, depending on distance
				color.rgb = lerp(unity_FogColor.rgb, color.rgb, i.height);

				return color;
			}
		
			ENDCG
		}
	}
}