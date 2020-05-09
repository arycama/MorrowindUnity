#include "UnityCG.cginc"

#if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
	#define UNITY_STANDARD_USE_DITHER_MASK
#endif

// Need to output UVs in shadow caster, since we need to sample texture and do clip/dithering based on it
#if defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
	#define UNITY_STANDARD_USE_SHADOW_UVS
#endif

// Has a non-empty shadow caster output struct (it's an error to have empty structs on some platforms...)
#if !defined(V2F_SHADOW_CASTER_NOPOS_IS_EMPTY) || defined(UNITY_STANDARD_USE_SHADOW_UVS)
	#define UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
#endif

half4       _Color;
half        _Cutoff;
sampler2D   _MainTex;
float4      _MainTex_ST;
sampler3D   _DitherMaskLOD;

struct VertexInput
{
	UNITY_VERTEX_INPUT_INSTANCE_ID
	float4 vertex   : POSITION;
	float3 normal   : NORMAL;

	#ifdef UNITY_STANDARD_USE_SHADOW_UVS
		float2 uv0      : TEXCOORD0;
	#endif
};

#ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
	struct VertexOutputShadowCaster
	{
		V2F_SHADOW_CASTER_NOPOS
		#if defined(UNITY_STANDARD_USE_SHADOW_UVS)
			float2 tex : TEXCOORD1;
		#endif
	};
#endif

void vertShadowCaster (VertexInput v, out float4 opos : SV_POSITION
	#ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
	, out VertexOutputShadowCaster o
	#endif
)
{
	UNITY_SETUP_INSTANCE_ID(v);
	TRANSFER_SHADOW_CASTER_NOPOS(o,opos)
	#if defined(UNITY_STANDARD_USE_SHADOW_UVS)
		o.tex = TRANSFORM_TEX(v.uv0, _MainTex);
	#endif
}

half4 fragShadowCaster (UNITY_POSITION(vpos)
#ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
	, VertexOutputShadowCaster i
#endif
) : SV_Target
{
	#if defined(UNITY_STANDARD_USE_SHADOW_UVS)
		half alpha = tex2D(_MainTex, i.tex).a * _Color.a;

		#if defined(_ALPHATEST_ON)
			clip (alpha - _Cutoff);
		#endif

		#if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
			#if defined(UNITY_STANDARD_USE_DITHER_MASK)

				// Use dither mask for alpha blended shadows, based on pixel position xy
				// and alpha level. Our dither texture is 4x4x16.
				half alphaRef = tex3D(_DitherMaskLOD, float3(vpos.xy*0.25,alpha*0.9375)).a;
				clip (alphaRef - 0.01);
			#else
				clip (alpha - _Cutoff);
			#endif
		#endif
	#endif

	SHADOW_CASTER_FRAGMENT(i)
}