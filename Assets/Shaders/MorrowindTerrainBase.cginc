﻿#include "UnityLightingCommon.cginc"
#include "UnityCG.cginc"
#include "AutoLight.cginc"

sampler2D _Control;
float4 _Control_ST, _MainTex_ST;
sampler2D _Blend;

UNITY_DECLARE_TEX2DARRAY(_MainTex);

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
	fixed3 ambient : TEXCOORD2;

	SHADOW_COORDS(4)
	UNITY_FOG_COORDS(5)

	float3 blend : TEXCOORD6;
	nointerpolation int3 indices : TEXCOORD7;
	nointerpolation int textureIndex :  TEXCOORD8;
};

v2f vert(appdata v)
{
	UNITY_SETUP_INSTANCE_ID(v);

    v2f o;
    o.posWorld = mul(unity_ObjectToWorld, v.vertex);
	o.pos = UnityWorldToClipPos(o.posWorld);
    
	o.tex = TRANSFORM_TEX(v.uv, _MainTex);

    half incidence = dot(v.normal, _WorldSpaceLightPos0.xyz);
    o.diffuse =_LightColor0.rgb * saturate(incidence) * v.color;
	o.ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * v.color;

    UNITY_TRANSFER_FOG(o, o.pos);
	TRANSFER_SHADOW(o);

	o.textureIndex = tex2Dlod(_Control, float4(v.uv * _Control_ST.xy + _Control_ST.zw, 0, 0)).a * 256;

    return o;
}

[maxvertexcount(3)]
void geom (triangle v2f i[3], inout TriangleStream<v2f> stream) 
{
	int3 indices = int3(i[0].textureIndex, i[1].textureIndex, i[2].textureIndex);

	i[0].indices = indices;
	i[1].indices = indices;
	i[2].indices = indices;

    i[0].blend = float3(1, 0, 0);
    i[1].blend = float3(0, 1, 0);
    i[2].blend = float3(0, 0, 1);

	stream.Append(i[0]);
	stream.Append(i[1]);
	stream.Append(i[2]);
}

fixed3 frag(v2f i) : SV_Target
{
	fixed3 albedo = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(i.tex.xy, i.indices.x)) * i.blend.x;
	albedo += UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(i.tex.xy, i.indices.y)) * i.blend.y;
	albedo +=  UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(i.tex.xy, i.indices.z)) * i.blend.z;

	half shadow = UNITY_SHADOW_ATTENUATION(i, i.posWorld);
    albedo *= i.ambient + i.diffuse * shadow;

	UNITY_APPLY_FOG(i.fogCoord, albedo);
    return albedo;
}