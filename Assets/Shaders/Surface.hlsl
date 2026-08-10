#include "Common.hlsl"

struct VertexInput
{
	uint instanceId : SV_InstanceID;
	float3 position : POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD;
	float3 color : COLOR;
};

struct FragmentInput
{
	float4 position : SV_Position;
	float3 worldPosition : POSITION1;
	float2 uv : TEXCOORD;
	float3 normal : NORMAL;
	float3 color : COLOR;
};

struct FragmentOutput
{
	#ifdef GBUFFER
		GbufferOutput gbuffer;
	#endif
	
	#ifdef FORWARD
		float4 color : SV_Target;
	#endif
};

Texture2D _MainTex, _EmissionMap;
SamplerState sampler_MainTex;

cbuffer UnityPerMaterial
{
	float4 _Color, _MainTex_ST;
	float3 _EmissionColor;
	float _Cutoff, _SrcBlend;
};

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.worldPosition = ObjectToWorld(input.position, input.instanceId);
	output.position = WorldToClipPosition(output.worldPosition);
	output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
	output.normal = ObjectToWorldNormal(input.normal, input.instanceId);
	output.color = AmbientLight * GammaToLinear(input.color) + _EmissionColor;
	return output;
}

FragmentOutput Fragment(FragmentInput input)
{
	FragmentOutput output;

	float4 color = _MainTex.Sample(sampler_MainTex, input.uv);
	float3 normal = normalize(input.normal);
	
	#ifdef SHADOW
		clip(color.a - 0.5);
	#endif
	
	#ifdef GBUFFER
		output.gbuffer = OutputGbuffer(color.rgb, normal, input.color * color.rgb);
	#endif
	
	#ifdef FORWARD
		float viewDistance = distance(ViewPosition, input.worldPosition);
		bool isPremultiplied = _SrcBlend == 1.0;
		output.color = GetLuminanceAndFog(color, input.color, normal, input.position.xy, input.position.w, viewDistance, isPremultiplied, input.worldPosition);
	#endif
	
	return output;
}