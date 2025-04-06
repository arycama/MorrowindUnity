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
	float4 tex : TEXCOORD0;
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
    
	o.tex = float4(v.uv.xy, TRANSFORM_TEX(v.uv.zw, _MainTex));

    float3 viewNormal = mul((float3x3) unity_MatrixV, v.normal);
    o.lighting = UNITY_LIGHTMODEL_AMBIENT.rgb;

    for (uint i = 0; i < 4; i++)
    {
        float3 lightVector = unity_LightPosition[i].xyz - viewPos * unity_LightPosition[i].w;
        float3 lightDirection = normalize(lightVector);
        float incidence = saturate(dot(viewNormal, lightDirection));
        float sqrDistance = dot(lightVector, lightVector);
        float attenuation = saturate(unity_LightAtten[i].w / (sqrDistance * 3));

        o.lighting += unity_LightColor[i].rgb * incidence * attenuation;
    }

    o.lighting *= v.color;

    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
}

float4 BilinearWeights(float2 uv, float2 textureSize)
{
	float2 localUv = frac(uv * textureSize - 0.5 + rcp(512.0));
	float4 weights = localUv.xxyy * float4(-1, 1, 1, -1) + float4(1, 0, 0, 1);
	return weights.zzww * weights.xyyx;
}

fixed3 frag(v2f i) : SV_Target
{
	float4 terrainData = _Control.Gather(sampler_Control, i.tex.xy) * 255.0;
	float4 weights = BilinearWeights(i.tex.xy, _Control_TexelSize.zw);
	
	fixed3 albedo = _MainTex.Sample(sampler_MainTex, float3(i.tex.zw, terrainData.x)) * weights.x;
	albedo += _MainTex.Sample(sampler_MainTex, float3(i.tex.zw, terrainData.y)) * weights.y;
	albedo += _MainTex.Sample(sampler_MainTex, float3(i.tex.zw, terrainData.z)) * weights.z;
	albedo += _MainTex.Sample(sampler_MainTex, float3(i.tex.zw, terrainData.w)) * weights.w;

    albedo *= i.lighting;

	UNITY_APPLY_FOG(i.fogCoord, albedo);
    return albedo;
}