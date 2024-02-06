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
	FragmentInput output;
	output.worldPosition = ObjectToWorld(input.position, input.instanceID);
	output.position = WorldToClip(output.worldPosition);
	output.uv = output.worldPosition.xz * _Tiling;
	return output;
}

float3 Fragment(FragmentInput input) : SV_Target
{
	float3 positionWS = input.worldPosition;
	float linearWaterDepth = input.position.w;

	float underwaterDepth = LinearEyeDepth(_CameraDepth[input.position.xy], _ZBufferParams);
	float underwaterDistance = underwaterDepth - linearWaterDepth;
	
	// Clamp underwater depth if sampling a non-underwater pixel
	//if (underwaterDistance <= 0.0)
	//{
	//	underwaterDepth = _CameraDepth[input.position.xy];
	//	underwaterDistance = max(0.0, LinearEyeDepth(underwaterDepth, _ZBufferParams) - linearWaterDepth);
	//}
	
	float3 V = normalize(positionWS - _WorldSpaceCameraPos);
	underwaterDistance /= dot(V, -_WorldToView._m20_m21_m22);
	
	float2 noise = _BlueNoise2D[input.position.xy % 128];
	float3 channelMask = floor(noise.y * 3.0) == float3(0.0, 1.0, 2.0);
	float xi = noise.x;
	
	float t = -log(1.0 - xi * (1.0 - exp(-dot(_Extinction, channelMask) * underwaterDistance))) / dot(_Extinction, channelMask);
	float3 tr = exp(_Extinction * t) / _Extinction - rcp(_Extinction * exp(_Extinction * (underwaterDistance - t)));
	float weight = rcp(dot(rcp(tr), 1.0 / 3.0));
	float3 P = input.worldPosition + V * t;
	
	float3 luminance = 0.0;
	
	for (uint i = 0; i < min(_DirectionalLightCount, 4); i++)
	{
		float shadow = GetShadow(P, i);
		if (!shadow)
			continue;
		
		DirectionalLight light = _DirectionalLights[i];
		
		float shadowDistance = max(0.0, input.worldPosition.y - P.y) / max(1e-6, saturate(light.direction.y));
		luminance += light.color * _Exposure * shadow * exp(-_Extinction * (shadowDistance + t)) * weight;
	}
	
	float4 positionCS = PerspectiveDivide(WorldToClip(P));
	positionCS.xy = (positionCS.xy * 0.5 + 0.5) * _ScaledResolution.xy;
	
	uint3 clusterIndex;
	clusterIndex.xy = floor(positionCS.xy) / _TileSize;
	clusterIndex.z = log2(positionCS.w) * _ClusterScale + _ClusterBias;
	
	uint2 lightOffsetAndCount = _LightClusterIndices[clusterIndex];
	uint startOffset = lightOffsetAndCount.x;
	uint lightCount = lightOffsetAndCount.y;
	
	// Point lights
	for (i = 0; i < min(128, lightCount); i++)
	{
		int index = _LightClusterList[startOffset + i];
		PointLight light = _PointLights[index];
		
		float3 lightVector = light.position - P;
		float sqrLightDist = dot(lightVector, lightVector);
		if (sqrLightDist > Sq(light.range))
			continue;
		
		float shadow = 1.0;
		if (light.shadowIndex != ~0u)
		{
			uint visibleFaces = light.visibleFaces;
			float dominantAxis = Max3(abs(lightVector * float3(-1, 1, -1)));
			float depth = rcp(dominantAxis) * light.far + light.near;
			shadow = _PointShadows.SampleCmpLevelZero(_LinearClampCompareSampler, float4(lightVector * float3(-1, 1, -1), light.shadowIndex), depth);
		}
		
		if (!shadow)
			continue;
		
		float rcpLightDist = rsqrt(sqrLightDist);
		float attenuation = Remap(light.range * rcp(3.0) * rcpLightDist, rcp(3.0));
		
		float3 L = lightVector * rcpLightDist;
		float shadowDistance = max(0.0, input.worldPosition.y - P.y) / max(1e-6, saturate(L.y));
		luminance += light.color * _Exposure * attenuation * shadow * exp(-_Extinction * (shadowDistance + t)) * weight;
	}
	
	luminance *= _Extinction;
	
	// Ambient 
	float3 finalTransmittance = exp(-underwaterDistance * _Extinction);
	luminance += _AmbientLightColor * _Exposure * (1.0 - finalTransmittance);
	luminance *= _Albedo;
	luminance = IsInfOrNaN(luminance) ? 0.0 : luminance;
	
	float3 scene = _SceneTexture[input.position.xy];
	
	// Need to remove fog from background
	float4 backgroundFog = SampleVolumetricLighting(input.position.xy, underwaterDepth);
	if (backgroundFog.a)
		scene = saturate((scene - backgroundFog.rgb) * rcp(backgroundFog.a));
	
	luminance += scene * exp(-_Extinction * underwaterDistance);
	
	//float3 color = _MainTex.Sample(_LinearRepeatSampler, input.uv).rgb;
	//color = lerp(_Albedo, color, _Alpha); // 
	//float3 lighting = GetLighting(float3(0, 1, 0), input.worldPosition) + _AmbientLightColor * _Exposure;
	//color.rgb *= lighting;
	
	float3 color = luminance;
	color.rgb = ApplyFog(color.rgb, input.position.xy, input.position.w);
	return color;
}