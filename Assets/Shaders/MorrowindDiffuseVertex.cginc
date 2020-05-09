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
	float2 tex : TEXCOORD0;
    fixed3 lighting : TEXCOORD1;
	UNITY_FOG_COORDS(5)
};

v2f vert(appdata v)
{
	UNITY_SETUP_INSTANCE_ID(v);

    v2f o;
    float3 posWorld = mul(unity_ObjectToWorld, v.vertex);
    float3 viewPos = UnityWorldToViewPos(posWorld);
	o.pos = UnityWorldToClipPos(posWorld);
    
	o.tex = TRANSFORM_TEX(v.uv, _MainTex);

	// Vert lights are in view space (Why?), so convert normal to view space too
    float3 viewNormal = mul((float3x3) unity_MatrixMV, v.normal);
    o.lighting = UNITY_LIGHTMODEL_AMBIENT.rgb * v.color;

    for (uint i = 0; i < 4; i++)
    {
        float3 lightVector = unity_LightPosition[i].xyz - viewPos * unity_LightPosition[i].w;
        float3 lightDirection = normalize(lightVector);
        float incidence = saturate(dot(viewNormal, lightDirection));
        float sqrDistance = dot(lightVector, lightVector);
        float attenuation = saturate(unity_LightAtten[i].w / (sqrDistance * 3));

        o.lighting += unity_LightColor[i].rgb * incidence * attenuation;
    }

    UNITY_TRANSFER_FOG(o, o.pos);

    return o;
}

float4 frag(v2f i) : SV_Target
{
	fixed4 albedo = tex2D(_MainTex, i.tex);

	fixed4 color = albedo;
	color.rgb *= i.lighting;

	color.rgb += albedo.rgb * _EmissionColor;// tex2D(_EmissionMap, i.tex).rgb;

	UNITY_APPLY_FOG(i.fogCoord, color);

    return color;
}