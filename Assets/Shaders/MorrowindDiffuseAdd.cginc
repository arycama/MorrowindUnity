#ifdef __INTELLISENSE__
	#define POINT
#endif

#include "UnityLightingCommon.cginc"
#include "UnityCG.cginc"
#include "AutoLight.cginc"

sampler2D _MainTex;
float4 _MainTex_ST;

fixed4 _Color;
fixed _Cutoff;

struct appdata
{
	UNITY_VERTEX_INPUT_INSTANCE_ID
    float4 vertex : POSITION;
    half3 normal : NORMAL;
    float2 uv : TEXCOORD;
};

struct v2f
{
	UNITY_POSITION(pos);
	float3 posWorld : POSITION1;

	float2 tex : TEXCOORD;
    fixed3 diffuse : COLOR1;

	SHADOW_COORDS(3)
	UNITY_FOG_COORDS(4)
};

v2f vert(appdata v)
{
	UNITY_SETUP_INSTANCE_ID(v);

	v2f o;
	o.posWorld = mul(unity_ObjectToWorld, v.vertex);
	o.pos = UnityWorldToClipPos(o.posWorld);

	o.tex = TRANSFORM_TEX(v.uv, _MainTex);

    half3 worldNormal = UnityObjectToWorldNormal(v.normal);
    half3 lightDirection = normalize(_WorldSpaceLightPos0.xyz - o.posWorld);
    half incidence = saturate(dot(worldNormal, lightDirection));

	#ifdef POINT
		float3 lightCoord = mul(unity_WorldToLight, float4(o.posWorld, 1));
		fixed attenuation = saturate(1 / (3 * dot(lightCoord, lightCoord)));
		incidence *= attenuation;
	#endif

	o.diffuse = _LightColor0.rgb * incidence;

	UNITY_TRANSFER_FOG(o, o.pos);
	TRANSFER_SHADOW(o);

	return o;
}


float4 frag(v2f i) : SV_Target
{
	fixed4 color = tex2D(_MainTex, i.tex);

	half shadow = UNITY_SHADOW_ATTENUATION(i, i.posWorld);

    color.rgb *= i.diffuse * shadow;

	UNITY_APPLY_FOG(i.fogCoord, color);

    return color;
}