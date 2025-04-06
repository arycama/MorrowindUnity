Shader"Morrowind/Terrain"
{
	Properties
	{
		_Control("Control", 2D) = "clear" {}
		_MainTex("Tex", 2DArray) = "" {}
	}

	SubShader
	{
		Tags{"RenderType"="Opaque"}

		Pass
		{
			Tags{"LightMode"="Vertex"}

			CGPROGRAM
			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag

			#include "MorrowindTerrainVertex.cginc"

			ENDCG
		}

		Pass
		{
			Tags{"LightMode" = "ForwardBase"}

			CGPROGRAM
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag

			#pragma multi_compile_fog
			#pragma multi_compile_fwdbase
			#pragma multi_compile_instancing

			#include "MorrowindTerrainBase.cginc"
			ENDCG
		}
		
		Pass
		{
			Tags{"LightMode" = "ForwardAdd"}

			Blend One One
			ZWrite Off

			CGPROGRAM
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag

			#pragma multi_compile_fog
			#pragma multi_compile_fwdadd_fullshadows
			#pragma multi_compile_instancing

			#include "MorrowindTerrainAdd.cginc"
			ENDCG
		}

		Pass
		{
			Colormask 0
			Tags { "LightMode" = "ShadowCaster" }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile_instancing
			#pragma multi_compile_shadowcaster

			#include "UnityCG.cginc"

			struct v2f 
			{
				V2F_SHADOW_CASTER;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			v2f vert(appdata_base v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				SHADOW_CASTER_FRAGMENT(i)
			}

			ENDCG
		}
	}
}