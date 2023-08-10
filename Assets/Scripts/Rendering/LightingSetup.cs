using System;
using System.Security.AccessControl;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

public class LightingSetup
{
    private static readonly int directionalShadowsId = Shader.PropertyToID("_DirectionalShadows");
    private static readonly int pointShadowsId = Shader.PropertyToID("_PointShadows");

    private readonly ShadowSettings settings; 

    private ComputeBuffer directionalLightBuffer; // Can't be readonly as we resize if needed.
    private ComputeBuffer pointLightBuffer; // Can't be readonly as we resize if needed.
    private ComputeBuffer directionalMatrixBuffer;

    private RenderTexture emptyArray, emptyCubemapArray;

    public LightingSetup(ShadowSettings settings)
    {
        this.settings = settings;
        directionalLightBuffer = new ComputeBuffer(1, UnsafeUtility.SizeOf<DirectionalLightData>());
        pointLightBuffer = new ComputeBuffer(1, UnsafeUtility.SizeOf<PointLightData>());
        directionalMatrixBuffer = new ComputeBuffer(1, UnsafeUtility.SizeOf<Matrix4x4>());

        emptyArray = new RenderTexture(1, 1, 0, RenderTextureFormat.Shadowmap)
        {
            dimension = TextureDimension.Tex2DArray,
            hideFlags = HideFlags.HideAndDontSave,
            volumeDepth = 1,
        };

        emptyCubemapArray = new RenderTexture(1, 1, 0, RenderTextureFormat.Shadowmap)
        {
            dimension = TextureDimension.CubeArray,
            hideFlags = HideFlags.HideAndDontSave,
            volumeDepth = 6,
        };
    }

    public void Release()
    {
        directionalLightBuffer.Release();
        pointLightBuffer.Release();
        directionalMatrixBuffer.Release();
        emptyArray.Release();
        emptyCubemapArray.Release();
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m)
    {
        if (SystemInfo.usesReversedZBuffer)
            m.SetRow(2, -m.GetRow(2));

        m.SetRow(0, 0.5f * (m.GetRow(0) + m.GetRow(3)));
        m.SetRow(1, 0.5f * (m.GetRow(1) + m.GetRow(3)));
        m.SetRow(2, 0.5f * (m.GetRow(2) + m.GetRow(3)));
        return m;
    }

    public void Render(CommandBuffer commandBuffer, CullingResults cullingResults, ScriptableRenderContext context)
    {
        var directionalLightList = ListPool<DirectionalLightData>.Get();
        var directionalShadowRequests = ListPool<ShadowRequest>.Get();
        var directionalShadowMatrices = ListPool<Matrix4x4>.Get();
        var pointLightList = ListPool<PointLightData>.Get();
        var pointShadowRequests = ListPool<ShadowRequest>.Get();

        // Setup lights/shadows
        for (var i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            var visibleLight = cullingResults.visibleLights[i];
            var light = visibleLight.light;
            var cascadeCount = 0;
            var shadowIndex = -1;

            if (visibleLight.lightType == LightType.Directional)
            {
                if (light.shadows != LightShadows.None && cullingResults.GetShadowCasterBounds(i, out var bounds))
                {
                    for (var j = 0; j < settings.ShadowCascades; j++)
                    {
                        if (!cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(i, j, settings.ShadowCascades, settings.ShadowCascadeSplits, settings.DirectionalShadowResolution, light.shadowNearPlane, out var viewMatrix, out var projectionMatrix, out var shadowSplitData))
                            continue;

                        cascadeCount++;

                        var directionalShadowRequest = new ShadowRequest(true, i, viewMatrix, projectionMatrix, shadowSplitData, 0);
                        directionalShadowRequests.Add(directionalShadowRequest);

                        var shadowMatrix = ConvertToAtlasMatrix(projectionMatrix * viewMatrix);
                        directionalShadowMatrices.Add(shadowMatrix);
                    }

                    if (cascadeCount > 0)
                        shadowIndex = directionalShadowRequests.Count - cascadeCount;
                }

                var directionalLightData = new DirectionalLightData((Vector4)light.color.linear, shadowIndex, -light.transform.forward, cascadeCount);
                directionalLightList.Add(directionalLightData);
            }
            else if (visibleLight.lightType == LightType.Point)
            {
                var near = light.shadowNearPlane;
                var far = light.range;

                var visibleFaceMask = 0;
                var visibleFaceCount = 0;
                if (light.shadows != LightShadows.None && cullingResults.GetShadowCasterBounds(i, out var bounds))
                {
                    for (var j = 0; j < 6; j++)
                    {
                        var isValid = false;
                        if (cullingResults.ComputePointShadowMatricesAndCullingPrimitives(i, (CubemapFace)j, 0.0f, out var viewMatrix, out var projectionMatrix, out var shadowSplitData))
                        {
                            visibleFaceMask |= 1 << j;
                            visibleFaceCount++;
                            isValid = true;
                        }

                        // To undo unity's builtin inverted culling for point shadows, flip the y axis.
                        // Y also needs to be done in the shader
                        viewMatrix.SetRow(1, -viewMatrix.GetRow(1));

                        var shadowRequest = new ShadowRequest(isValid, i, viewMatrix, projectionMatrix, shadowSplitData, j);
                        pointShadowRequests.Add(shadowRequest);

                        near = projectionMatrix[2, 3] / (projectionMatrix[2, 2] - 1f);
                        far = projectionMatrix[2, 3] / (projectionMatrix[2, 2] + 1f);
                    }

                    if (visibleFaceCount > 0)
                        shadowIndex = (pointShadowRequests.Count - visibleFaceCount) / 6;
                }

                var pointLightData = new PointLightData(light.transform.position, light.range, (Vector4)light.color.linear, shadowIndex, visibleFaceMask, near, far);
                pointLightList.Add(pointLightData);
            }
        }

        // Render Shadows
        commandBuffer.SetGlobalDepthBias(settings.ShadowBias, settings.ShadowSlopeBias);

        if (directionalShadowRequests.Count > 0)
        {
            // Process directional shadows
            commandBuffer.SetGlobalFloat("_ZClip", 0);

            // Setup shadow map for directional shadows
            var directionalShadowsDescriptor = new RenderTextureDescriptor(settings.DirectionalShadowResolution, settings.DirectionalShadowResolution, RenderTextureFormat.Shadowmap, 16)
            {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = directionalShadowRequests.Count,
            };

            commandBuffer.GetTemporaryRT(directionalShadowsId, directionalShadowsDescriptor);

            for (var i = 0; i < directionalShadowRequests.Count; i++)
            {
                var shadowRequest = directionalShadowRequests[i];
                commandBuffer.SetRenderTarget(directionalShadowsId, 0, CubemapFace.Unknown, i);
                commandBuffer.ClearRenderTarget(true, false, Color.clear);

                commandBuffer.SetViewProjectionMatrices(shadowRequest.ViewMatrix, shadowRequest.ProjectionMatrix);
                context.ExecuteCommandBuffer(commandBuffer);
                commandBuffer.Clear();

                var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, shadowRequest.VisibleLightIndex) { splitData = shadowRequest.ShadowSplitData };
                context.DrawShadows(ref shadowDrawingSettings);
            }

            commandBuffer.SetGlobalFloat("_ZClip", 1);
        }


        // Process point shadows 
        // Setup shadow map for point shadows
        if (pointShadowRequests.Count > 0)
        {
            var pointShadowsDescriptor = new RenderTextureDescriptor(settings.PointShadowResolution, settings.PointShadowResolution, RenderTextureFormat.Shadowmap, 16)
            {
                dimension = TextureDimension.CubeArray,
                volumeDepth = pointShadowRequests.Count * 6,
            };

            commandBuffer.GetTemporaryRT(pointShadowsId, pointShadowsDescriptor);

            for (var i = 0; i < pointShadowRequests.Count; i++)
            {
                var shadowRequest = pointShadowRequests[i];
                if (!shadowRequest.IsValid)
                    continue;

                commandBuffer.SetRenderTarget(pointShadowsId, 0, CubemapFace.Unknown, i);
                commandBuffer.ClearRenderTarget(true, false, Color.clear);

                commandBuffer.SetViewProjectionMatrices(shadowRequest.ViewMatrix, shadowRequest.ProjectionMatrix);
                context.ExecuteCommandBuffer(commandBuffer);
                commandBuffer.Clear();

                var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, shadowRequest.VisibleLightIndex) { splitData = shadowRequest.ShadowSplitData };
                context.DrawShadows(ref shadowDrawingSettings);
            }
        }

        commandBuffer.SetGlobalDepthBias(0f, 0f);

        // Set directional light data
        if (directionalLightList.Count > directionalLightBuffer.count)
        {
            directionalLightBuffer.Release();
            directionalLightBuffer = new ComputeBuffer(directionalLightList.Count, UnsafeUtility.SizeOf<DirectionalLightData>());
        }

        commandBuffer.SetBufferData(directionalLightBuffer, directionalLightList);
        commandBuffer.SetGlobalBuffer("_DirectionalLights", directionalLightBuffer);
        commandBuffer.SetGlobalInt("_DirectionalLightCount", directionalLightList.Count);
        commandBuffer.SetGlobalTexture(directionalShadowsId, directionalShadowRequests.Count > 0 ? directionalShadowsId : emptyArray);

        ListPool<DirectionalLightData>.Release(directionalLightList);
        ListPool<ShadowRequest>.Release(directionalShadowRequests);

        // Update directional shadow matrices
        if (directionalMatrixBuffer.count < directionalShadowMatrices.Count)
        {
            directionalMatrixBuffer.Release();
            directionalMatrixBuffer = new ComputeBuffer(directionalShadowMatrices.Count, UnsafeUtility.SizeOf<Matrix4x4>());
        }

        commandBuffer.SetBufferData(directionalMatrixBuffer, directionalShadowMatrices);
        commandBuffer.SetGlobalBuffer("_DirectionalMatrices", directionalMatrixBuffer);
        ListPool<Matrix4x4>.Release(directionalShadowMatrices);

        // Set point light data
        if (pointLightList.Count >= pointLightBuffer.count)
        {
            pointLightBuffer.Release();
            pointLightBuffer = new ComputeBuffer(pointLightList.Count, UnsafeUtility.SizeOf<PointLightData>());
        }

        commandBuffer.SetBufferData(pointLightBuffer, pointLightList);
        commandBuffer.SetGlobalBuffer("_PointLights", pointLightBuffer);
        commandBuffer.SetGlobalInt("_PointLightCount", pointLightList.Count);
        commandBuffer.SetGlobalTexture(pointShadowsId, pointShadowRequests.Count > 0 ? pointShadowsId : emptyCubemapArray);

        ListPool<ShadowRequest>.Release(pointShadowRequests);
        ListPool<PointLightData>.Release(pointLightList);
    }
}
