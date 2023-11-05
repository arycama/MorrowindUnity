Shader"Effects/Box Blur"
{
	Properties
	{
		[IntRange] _Size("Size", Range(0, 8)) = 2
		_Blur ("Blur", Int) = 4
		_Recip("Recip", Float) = 0.0625
		_Offset("Offset", Float) = -7.5
	}

	SubShader
	{
		Blend SrcAlpha OneMinusSrcAlpha
		Tags{"Queue"="Overlay"}

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
				float _Recip, _Offset;
				uint _Blur;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD;
				float4 color : COLOR;
			};
			
			v2f vert(appdata_full v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = float2(v.texcoord.x, 1 - v.texcoord.y);
				o.color = v.color;
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				float4 col = 0;
				for(uint x = 0; x < _Blur; x++)
				{
					float2 offset = float2(x + _Offset, 0) * _GrabTexture_TexelSize;
					col += tex2D(_GrabTexture, i.uv + offset);
				}

				return col * _Recip * i.color;
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
				float _Recip, _Offset;
				uint _Blur;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD;
				float4 color : COLOR;
			};
			
			v2f vert(appdata_full v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = float2(v.texcoord.x, 1 - v.texcoord.y);
				o.color = v.color;
				return o;
			}
			
			float4 frag (v2f i) : SV_Target
			{
				float4 col = 0;
				for(uint y = 0; y < _Blur; y++)
				{
					float2 offset = float2(0, y + _Offset) * _GrabTexture_TexelSize;
					col += tex2D(_GrabTexture, i.uv + offset);
				}

				return col * _Recip * i.color;
			}

			ENDCG
		}
	}

	CustomEditor "BoxBlurShaderGUI"
}