#include "Common.hlsl"
#include "RaytracingCommon.hlsl"

struct VertexInput
{
	uint instanceId : SV_InstanceID;
	float3 position : POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD;
};

struct FragmentInput
{
	float4 position : SV_Position;
	float3 viewPosition : POSITION1;
	float2 uv : TEXCOORD;
	float3 normal : NORMAL;
};

Texture2D<float3> _MainTex, FadeTexture, ScreenSpaceSpecular, ScreenSpaceRefraction;
SamplerState sampler_MainTex;

cbuffer UnityPerMaterial
{
	float Alpha, Scale, Fade;
	float3 Extinction, Albedo;
};

FragmentInput Vertex(VertexInput input)
{
	FragmentInput output;
	output.viewPosition = WorldToViewPosition(input.position - ViewPosition);
	output.position = ViewToClipPosition(output.viewPosition);
	output.uv = input.position.xz * Scale / 64.0 / 3;
	output.normal = WorldToViewNormal(float3(0.0, 1.0, 0.0));
	return output;
}

float4 Fragment(FragmentInput input) : SV_Target
{
	float3 current = _MainTex.Sample(sampler_MainTex, input.uv).rgb;
	float3 next = FadeTexture.Sample(sampler_MainTex, input.uv).rgb;
	float4 color = float4(lerp(current, next, frac(Fade)), Alpha);
	float3 normal =	normalize(input.normal);
	
	color.rgb *= AmbientLight + GetLuminance(normal, input.viewPosition, input.position.xy);
	
	#ifdef RAYTRACED_REFRACTION
		float3 refraction = ScreenSpaceRefraction[input.position.xy];
		color.rgb = refraction;
		color.a = 1.0;
	#endif
	
	#ifdef RAYTRACED_SPECULAR
		float3 V = normalize(-input.viewPosition);
		float NdotV = dot(normal, V);
		float fresnelTerm = pow(1.0 - NdotV, 5.0);
		float3 reflection = ScreenSpaceSpecular[input.position.xy];
		color.rgb = lerp(color.rgb, reflection, fresnelTerm);
		color.a = lerp(color.a, 1.0, fresnelTerm);
	#endif
	
	#ifdef VOLUMETRIC_LIGHT_ON
		float3 volumetricUv = float3(input.position.xy / ViewSize, input.viewPosition.z / MaxDepth);
		float4 volumetricLight = VolumetricLight.Sample(LinearClampSampler, volumetricUv);
		float3 fogLuminance = volumetricLight.rgb;
		float fogTransmittance = volumetricLight.a;
	#else
		float fogOpacity = FogFactor(input.viewPosition);
		float3 fogColor = FogColor;
		float3 fogLuminance = fogColor * fogOpacity;
		float fogTransmittance = 1.0 - fogOpacity;
	#endif
		
	color.rgb = color.rgb * fogTransmittance + fogLuminance;
	
	return color;
}