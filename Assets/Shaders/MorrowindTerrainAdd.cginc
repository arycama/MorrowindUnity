#include "UnityLightingCommon.cginc"
#include "UnityCG.cginc"
#include "AutoLight.cginc"

float4 _Control_ST, _MainTex_ST, _Control_TexelSize;
Texture2D _Control;
Texture2DArray _MainTex;
SamplerState sampler_Control, sampler_MainTex;

struct appdata
{
	UNITY_VERTEX_INPUT_INSTANCE_ID
    float4 vertex : POSITION;
    half3 normal : NORMAL;
    float4 uv : TEXCOORD;
    fixed3 color : COLOR;
};

struct v2f
{
	UNITY_POSITION(pos);
	float3 posWorld : POSITION1;

	float4 tex : TEXCOORD0;
    float3 diffuse : TEXCOORD1;

	SHADOW_COORDS(4)
	UNITY_FOG_COORDS(5)
};

v2f vert(appdata v)
{
	UNITY_SETUP_INSTANCE_ID(v);

    v2f o;
    o.posWorld = mul(unity_ObjectToWorld, v.vertex);
	o.pos = UnityWorldToClipPos(o.posWorld);
    
	o.tex = float4(v.uv.xy, TRANSFORM_TEX(v.uv.zw, _MainTex));

    half3 lightDirection = normalize(_WorldSpaceLightPos0.xyz - o.posWorld);
    half incidence = saturate(dot(v.normal, lightDirection));

	#ifdef POINT
		float3 lightCoord = mul(unity_WorldToLight, float4(o.posWorld, 1));
		fixed attenuation = saturate(1 / (3 * dot(lightCoord, lightCoord)));
		incidence *= attenuation;
	#endif

    o.diffuse = v.color * _LightColor0.rgb * incidence;

    UNITY_TRANSFER_FOG(o, o.pos);
	TRANSFER_SHADOW(o);

    return o;
}

float4 BilinearWeights(float2 uv, float2 textureSize)
{
	float2 localUv = frac(uv * textureSize - 0.5 + rcp(512.0));
	float4 weights = localUv.xxyy * float4(-1, 1, 1, -1) + float4(1, 0, 0, 1);
	return weights.zzww * weights.xyyx;
}

float3 frag(v2f i) : SV_Target
{
	float4 terrainData = _Control.Gather(sampler_Control, i.tex.xy) * 255.0;
	float4 weights = BilinearWeights(i.tex.xy, _Control_TexelSize.zw);
	
	fixed3 color = _MainTex.Sample(sampler_MainTex, float3(i.tex.zw, terrainData.x)) * weights.x;
	color += _MainTex.Sample(sampler_MainTex, float3(i.tex.zw, terrainData.y)) * weights.y;
	color += _MainTex.Sample(sampler_MainTex, float3(i.tex.zw, terrainData.z)) * weights.z;
	color += _MainTex.Sample(sampler_MainTex, float3(i.tex.zw, terrainData.w)) * weights.w;

	half shadow = UNITY_SHADOW_ATTENUATION(i, i.posWorld);

    color *= i.diffuse * shadow;

	UNITY_APPLY_FOG(i.fogCoord, color);
    return color;
}