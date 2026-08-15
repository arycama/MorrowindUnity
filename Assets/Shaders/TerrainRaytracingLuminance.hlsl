#include "Common.hlsl"
#include "Packages/com.arycama.customrenderpipeline/ShaderLibrary/Raytracing.hlsl"

Texture2D _Control;
Texture2DArray<float3> _MainTex;

cbuffer UnityPerMaterial
{
	float4 _MainTex_ST;
};

float4 _Control_TexelSize;

[shader("closesthit")]
void Raytracing(inout RayColorPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
{
	uint index = PrimitiveIndex();
	uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(index);
	
	float4 uv0 = UnityRayTracingFetchVertexAttribute4(triangleIndices.x, kVertexAttributeTexCoord0);
	float4 uv1 = UnityRayTracingFetchVertexAttribute4(triangleIndices.y, kVertexAttributeTexCoord0);
	float4 uv2 = UnityRayTracingFetchVertexAttribute4(triangleIndices.z, kVertexAttributeTexCoord0);
	float4 uv = BarycentricInterpolate(uv0, uv1, uv2, attribs.barycentrics.x, attribs.barycentrics.y);
	
	float3 normal0 = UnityRayTracingFetchVertexAttribute3(triangleIndices.x, kVertexAttributeNormal);
	float3 normal1 = UnityRayTracingFetchVertexAttribute3(triangleIndices.y, kVertexAttributeNormal);
	float3 normal2 = UnityRayTracingFetchVertexAttribute3(triangleIndices.z, kVertexAttributeNormal);
	float3 normal = BarycentricInterpolate(normal0, normal1, normal2, attribs.barycentrics.x, attribs.barycentrics.y);
	
	float4 color0 = UnityRayTracingFetchVertexAttribute4(triangleIndices.x, kVertexAttributeColor);
	float4 color1 = UnityRayTracingFetchVertexAttribute4(triangleIndices.x, kVertexAttributeColor);
	float4 color2 = UnityRayTracingFetchVertexAttribute4(triangleIndices.x, kVertexAttributeColor);
	float4 vertexColor = BarycentricInterpolate(color0, color1, color2, attribs.barycentrics.x, attribs.barycentrics.y);
	
	float3 viewNormal = WorldToViewNormal(normal);
	
	float4 terrainData = _Control.Gather(LinearClampSampler, uv.xy) * 255.0;
	float4 weights = BilinearWeights(uv.xy, _Control_TexelSize.zw);
	
	uv.zw = uv.zw * _MainTex_ST.xy + _MainTex_ST.zw;
	
	float3 color = _MainTex.SampleLevel(LinearRepeatSampler, float3(uv.zw, terrainData.x), 0.0) * weights.x;
	color += _MainTex.SampleLevel(LinearRepeatSampler, float3(uv.zw, terrainData.y), 0.0) * weights.y;
	color += _MainTex.SampleLevel(LinearRepeatSampler, float3(uv.zw, terrainData.z), 0.0) * weights.z;
	color += _MainTex.SampleLevel(LinearRepeatSampler, float3(uv.zw, terrainData.w), 0.0) * weights.w;
	color *= GammaToLinear(vertexColor.rgb);
	
	color.rgb *= AmbientLight + saturate(dot(viewNormal, SunDirection)) * SunColor;
	payload.color = color.rgb;
}