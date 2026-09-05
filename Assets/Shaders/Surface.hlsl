#include "Common.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Utility.hlsl"

struct VertexInput
{
	uint instanceId : SV_InstanceID;
	float3 position : POSITION;
	
	#if defined(GBUFFER) || defined(FORWARD)
		float3 normal : NORMAL;
		float3 color : COLOR;
	#endif
	
	#if defined(GBUFFER) || defined(FORWARD) || (defined(SHADOW) && defined(_ALPHABLEND_ON))
		float2 uv : TEXCOORD;
	#endif
};

struct FragmentInput
{
	float4 position : SV_Position;
	
	#if defined(GBUFFER) || defined(FORWARD)
		float3 viewPosition : POSITION1;
		float3 normal : NORMAL;
		float3 color : COLOR;
	#endif
	
	#ifdef SHADOW
		float3 objectPosition : POSITION2;
	#endif
	
	#if defined(GBUFFER) || defined(FORWARD) || (defined(SHADOW) && defined(_ALPHABLEND_ON))
		float2 uv : TEXCOORD;
	#endif
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
float4 _MainTex_TexelSize;

cbuffer UnityPerMaterial
{
	float4 _Color, _MainTex_ST;
	float3 _EmissionColor;
	float _Cutoff, _SrcBlend;
};

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	
	#ifdef SHADOW
		output.objectPosition = input.position;
	#endif
	
	output.position = ObjectToClipPosition(input.position, input.instanceId);

	#if defined(GBUFFER) || defined(FORWARD)
		output.viewPosition = ObjectToViewPosition(input.position, input.instanceId);
		output.normal = ObjectToViewNormal(input.normal, input.instanceId);
		output.color = AmbientLight * GammaToLinear(input.color) + _EmissionColor;
	#endif
	
	#if defined(GBUFFER) || defined(FORWARD) || (defined(SHADOW) && defined(_ALPHABLEND_ON))
		output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
	#endif
	
	return output;
}

FragmentOutput Fragment(FragmentInput input)
{
	FragmentOutput output;

	#if defined(GBUFFER) || defined(FORWARD) || (defined(SHADOW) && defined(_ALPHABLEND_ON))
		float4 color = _MainTex.Sample(sampler_MainTex, input.uv) * _Color;
	#endif
	
	#if defined(SHADOW) && defined(_ALPHABLEND_ON)
		float threshold = HashedAlphaThresholdCore(input.objectPosition, false);
		clip(color.a - threshold);
	#endif
	
	#ifdef GBUFFER
		float3 emissive = input.color * color.rgb;
		//if (ViewPosition.y < WaterHeight)
		//	emissive = lerp(emissive, emissive * UnderwaterColor, UnderwaterColorWeight);
	
		output.gbuffer = OutputGbuffer(color.rgb, input.normal, emissive, -normalize(input.viewPosition), input.position.xy);
	#endif
	
	#ifdef FORWARD
		output.color = GetLuminanceAndFog(color, input.color, normalize(input.normal), input.position.xy, input.viewPosition);
	#endif
	
	return output;
}