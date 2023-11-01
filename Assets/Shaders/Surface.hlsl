#include "Common.hlsl"

struct VertexInput
{
	uint instanceID : SV_InstanceID;
	float3 position : POSITION;
	
	#if !defined(UNITY_PASS_SHADOWCASTER) || defined(_ALPHABLEND_ON)
		float2 uv : TEXCOORD;
	#endif
	
	#ifndef UNITY_PASS_SHADOWCASTER
		float3 normal : NORMAL;
		float3 color : COLOR;
	#endif
};

struct FragmentInput
{
	float4 position : SV_Position;
	
	#if !defined(UNITY_PASS_SHADOWCASTER) || defined(_ALPHABLEND_ON)
		float2 uv : TEXCOORD;
	#endif
	
	#ifndef UNITY_PASS_SHADOWCASTER
		float3 worldPosition : POSITION1;
		float3 normal : NORMAL;
		float3 color : COLOR;
	#endif
};

cbuffer UnityPerMaterial
{
	float4 _Color, _MainTex_ST;
	float3 _EmissionColor;
	float _Cutoff;
};

Texture2D _MainTex, _EmissionMap;

FragmentInput Vertex(VertexInput input)
{
	float3 worldPosition = ObjectToWorld(input.position, input.instanceID);
	
	FragmentInput output;
	output.position = WorldToClip(worldPosition);
	
	#if !defined(UNITY_PASS_SHADOWCASTER) || defined(_ALPHABLEND_ON)
		output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
	#endif
	
	#ifndef UNITY_PASS_SHADOWCASTER
		output.worldPosition = worldPosition;
		output.normal = ObjectToWorldNormal(input.normal, input.instanceID);
		output.color = _AmbientLightColor * input.color + _EmissionColor;
	#endif
	
	return output;
}

#ifdef UNITY_PASS_SHADOWCASTER
	#ifdef _ALPHABLEND_ON
		void Fragment(FragmentInput input)
	#else
		void Fragment()
	#endif
#else
	float4 Fragment(FragmentInput input) : SV_Target
#endif
{
	#if !defined(UNITY_PASS_SHADOWCASTER) || defined(_ALPHABLEND_ON)
		float4 color = _MainTex.Sample(_TrilinearRepeatAniso16Sampler, input.uv);
	#endif
	
	#if defined(UNITY_PASS_SHADOWCASTER) 
		#ifdef _ALPHABLEND_ON
			clip(color.a - 0.5);
			//clip(color.a - InterleavedGradientNoise(input.position.xy, 0));
			//clip(color.a - _BlueNoise1D[uint2(input.position.xy) % 128]);
		#endif
	#else
		float3 normal = normalize(input.normal);
		float3 lighting = GetLighting(normal, input.worldPosition);

		lighting += input.color;
		color.rgb *= lighting;
		color.rgb = ApplyFog(color.rgb, input.worldPosition, InterleavedGradientNoise(input.position.xy, 0));
	
		return color;
	#endif
}