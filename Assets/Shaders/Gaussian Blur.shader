Shader"Effects/Gaussian Blur"
{
	Properties
	{
		[IntRange] _Blur ("Blur Radius", Range(1, 32)) = 4
		[IntRange] _Sigma ("Sigma", Range(1, 8)) = 3
	}

	SubShader
	{
		GrabPass { }

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			sampler2D _GrabTexture;
			float2 _GrabTexture_TexelSize;

			cbuffer UnityPerMaterial
			{
				uint _Blur, _Sigma;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD;
			};
			
			v2f vert(appdata_full v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = float2(v.texcoord.x, 1 - v.texcoord.y);
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				float sum = 0;
				float4 col = 0;

				uint length = floor(_Blur * _Sigma * 0.5) * 2 + 1;
				
				for (uint x = 0; x < length; x++)
				{
					float2 uvOffset = float2(x - floor(length * 0.5), 0);
					float2 pos = i.uv + uvOffset * _GrabTexture_TexelSize;
					float weight = exp(-(pow(uvOffset.x, 2) / (2 * pow(_Blur, 2)))) / sqrt(UNITY_TWO_PI * pow(_Blur, 2));

					col += tex2D(_GrabTexture, pos) * weight;
					sum += weight;
				}

				return col / sum;
			}

			ENDCG
		}

		GrabPass { }

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			sampler2D _GrabTexture;
			float2 _GrabTexture_TexelSize;

			cbuffer UnityPerMaterial
			{
				uint _Blur, _Sigma;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD;
			};
			
			v2f vert(appdata_full v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = float2(v.texcoord.x, 1 - v.texcoord.y);
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				float sum = 0;
				float4 col = 0;

				uint length = floor(_Blur * _Sigma * 0.5) * 2 + 1;
				
				for (uint y = 0; y < length; y++)
				{
					float2 uvOffset = float2(0, y - floor(length * 0.5));
					float2 pos = i.uv + uvOffset * _GrabTexture_TexelSize;
					float weight = exp(-(pow(uvOffset.y, 2) / (2 * pow(_Blur, 2)))) / sqrt(UNITY_TWO_PI * pow(_Blur, 2));

					col += tex2D(_GrabTexture, pos) * weight;
					sum += weight;
				}

				return col / sum;
			}

			ENDCG
		}
	}
}