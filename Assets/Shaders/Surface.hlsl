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
	float3 viewPosition : POSITION1;
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
	output.viewPosition = ObjectToViewPosition(input.position, input.instanceId);
	output.position = ObjectToClipPosition(input.position, input.instanceId);
	output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
	output.normal = ObjectToViewNormal(input.normal, input.instanceId);
	output.color = AmbientLight * GammaToLinear(input.color) + _EmissionColor;
	return output;
}

FragmentOutput Fragment(FragmentInput input)
{
	FragmentOutput output;

	float4 color = _MainTex.Sample(sampler_MainTex, input.uv);
	
	#ifdef SHADOW
		clip(color.a - 0.5);
	#endif
	
	#ifdef GBUFFER
		float3 emissive = input.color * color.rgb;
		if (ViewPosition.y < 0)
			emissive = lerp(emissive, emissive * UnderwaterColor, UnderwaterColorWeight);
	
		emissive *= 1.0 - FogFactor(input.viewPosition);
		output.gbuffer = OutputGbuffer(color.rgb, input.normal, emissive);
	#endif
	
	#ifdef FORWARD
		output.color = GetLuminanceAndFog(color, input.color, normalize(input.normal), input.position.xy, input.viewPosition);
	#endif
	
	return output;
}