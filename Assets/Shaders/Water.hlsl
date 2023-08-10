#include "Common.hlsl"

struct VertexInput
{
	uint instanceID : SV_InstanceID;
	float3 position : POSITION;
};

struct FragmentInput
{
	float4 position : SV_Position;
	float3 worldPosition : POSITION1;
	float2 uv : TEXCOORD;
};

Texture2D<float3> _SceneTexture;
Texture2D<float> _DepthTexture;
Texture2D _MainTex, _EmissionMap;

cbuffer UnityPerMaterial
{
	float4 _MainTex_ST;
	float3 _Albedo, _Extinction;
	float _Alpha, _Fade, _Tiling;
};

FragmentInput Vertex(VertexInput input)
{
	#ifdef INSTANCING_ON
		float4x4 localToWorld = unity_Builtins0Array[unity_BaseInstanceID + input.instanceID].unity_ObjectToWorldArray;
		float4x4 worldToObject = unity_Builtins0Array[unity_BaseInstanceID + input.instanceID].unity_WorldToObjectArray;
	#else
		float4x4 localToWorld = unity_ObjectToWorld;
		float4x4 worldToObject = unity_WorldToObject;
	#endif
	
	FragmentInput output;
	output.worldPosition = ObjectToWorld(input.position, input.instanceID);
	output.position = WorldToClip(output.worldPosition);
	output.uv = output.worldPosition.xz * _Tiling;
	return output;
}

float3 Fragment(FragmentInput input) : SV_Target
{
	float depth = _DepthTexture[input.position.xy];
	float3 backgroundPositionWS = MultiplyPointProj(_InvViewProjectionMatrix, float3(input.position.xy / _ScreenParams.xy * 2.0 - 1.0, depth));
	
	
	float difference = max(0.0, distance(backgroundPositionWS, input.worldPosition));
	
	float3 transmittance = exp(-difference * _Extinction);
	
	float3 color = _MainTex.Sample(_LinearRepeatSampler, input.uv);
	color = lerp(_Albedo, color, _Alpha); // 
	float3 lighting = GetLighting(float3(0, 1, 0), input.worldPosition) + _AmbientLightColor;
	color.rgb *= lighting;
	
	float3 scene = _SceneTexture[input.position.xy];
	
	// Need to remove fog from background
	if (_FogEnabled)
	{
		float4 backgroundFog = SampleVolumetricLighting(backgroundPositionWS);
		if(backgroundFog.a)
			scene = max(0.0, scene - backgroundFog.rgb) * rcp(backgroundFog.a);
	}
	
	color.rgb = lerp(color.rgb, scene, transmittance);
	color.rgb = ApplyFog(color.rgb, input.worldPosition, InterleavedGradientNoise(input.position.xy, 0));
	return color;
}