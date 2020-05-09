#include "UnityLightingCommon.cginc"
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
    float2 tex : TEXCOORD0;
    fixed3 lighting : TEXCOORD1;
	UNITY_FOG_COORDS(5)

	float3 blend : TEXCOORD6;
	nointerpolation int3 indices : TEXCOORD7;
	nointerpolation int textureIndex :  TEXCOORD8;
};

v2f vert(appdata v)
{
	UNITY_SETUP_INSTANCE_ID(v);

    v2f o;
    float3 posWorld = mul(unity_ObjectToWorld, v.vertex);
    float3 viewPos = UnityWorldToViewPos(posWorld);
	o.pos = UnityWorldToClipPos(posWorld);
    
	o.tex = TRANSFORM_TEX(v.uv, _MainTex);

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
	float3 albedo = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(i.tex.xy, i.indices.x)) * i.blend.x;
	albedo += UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(i.tex.xy, i.indices.y)) * i.blend.y;
	albedo +=  UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(i.tex.xy, i.indices.z)) * i.blend.z;

    albedo *= i.lighting;

	UNITY_APPLY_FOG(i.fogCoord, albedo);
    return albedo;
}