#include "UnityLightingCommon.cginc"
#include "UnityCG.cginc"
#include "AutoLight.cginc"

sampler2D _MainTex;
float4 _MainTex_ST;

fixed4 _Color;
fixed _Cutoff;

fixed3 _EmissionColor;
sampler2D _EmissionMap;

struct appdata
{
	UNITY_VERTEX_INPUT_INSTANCE_ID
    float4 vertex : POSITION;
    half3 normal : NORMAL;
    float2 uv : TEXCOORD;
	fixed3 color : COLOR;
};

struct v2f
{
	UNITY_POSITION(pos);
	float3 posWorld : POSITION1;

	float2 tex : TEXCOORD0;
    fixed3 diffuse : TEXCOORD1;
	fixed3 ambient : TEXCOOR2;

	SHADOW_COORDS(4)
	UNITY_FOG_COORDS(5)
};

v2f vert(appdata v)
{
	UNITY_SETUP_INSTANCE_ID(v);

    v2f o;
    o.posWorld = mul(unity_ObjectToWorld, v.vertex);
	o.pos = UnityWorldToClipPos(o.posWorld);
    
	o.tex = TRANSFORM_TEX(v.uv, _MainTex);
	o.ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * v.color;

	half3 worldNormal = UnityObjectToWorldNormal(v.normal);
    half incidence = saturate(dot(worldNormal, _WorldSpaceLightPos0.xyz));
    o.diffuse = _LightColor0.rgb * incidence;

    UNITY_TRANSFER_FOG(o, o.pos);
	TRANSFER_SHADOW(o);

    return o;
}

float4 frag(v2f i) : SV_Target
{
	fixed4 albedo = tex2D(_MainTex, i.tex);

    half shadow = UNITY_SHADOW_ATTENUATION(i, i.posWorld);

	fixed4 color = albedo;
	color.rgb *= i.ambient + i.diffuse * shadow;

	color.rgb += albedo.rgb * _EmissionColor;// tex2D(_EmissionMap, i.tex).rgb;

	UNITY_APPLY_FOG(i.fogCoord, color);

    return color;
}