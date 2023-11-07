using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

public class LightingSetup
{
    private static readonly Plane[] frustumPlanes = new Plane[6];

    private static readonly int directionalShadowsId = Shader.PropertyToID("_DirectionalShadows");
    private static readonly int pointShadowsId = Shader.PropertyToID("_PointShadows");
    private static readonly IndexedString cascadeStrings = new IndexedString("Cascade ");
    private static readonly IndexedString faceStrings = new IndexedString("Face ");

    private readonly ShadowSettings settings;

    private ComputeBuffer directionalLightBuffer; // Can't be readonly as we resize if needed.
    private ComputeBuffer pointLightBuffer; // Can't be readonly as we resize if needed.
    private ComputeBuffer directionalMatrixBuffer;
    private ComputeBuffer directionalTexelSizeBuffer;
    private ComputeBuffer pointTexelSizeBuffer;

    private RenderTexture emptyArray, emptyCubemapArray;

    public LightingSetup(ShadowSettings settings)
    {
        this.settings = settings;
        directionalLightBuffer = new ComputeBuffer(1, UnsafeUtility.SizeOf<DirectionalLightData>());
        pointLightBuffer = new ComputeBuffer(1, UnsafeUtility.SizeOf<PointLightData>());
        directionalMatrixBuffer = new ComputeBuffer(1, UnsafeUtility.SizeOf<Matrix4x4>());
        directionalTexelSizeBuffer = new ComputeBuffer(1, UnsafeUtility.SizeOf<Vector4>());
        pointTexelSizeBuffer = new ComputeBuffer(1, UnsafeUtility.SizeOf<Vector4>());

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
        directionalTexelSizeBuffer.Release();
        pointTexelSizeBuffer.Release();
    }

    public void Render(CommandBuffer command, CullingResults cullingResults, ScriptableRenderContext context, Camera camera)
    {
        var directionalLightList = ListPool<DirectionalLightData>.Get();
        var directionalShadowRequests = ListPool<ShadowRequest>.Get();
        var directionalShadowMatrices = ListPool<Matrix4x4>.Get();
        var directionalShadowTexelSizes = ListPool<Vector4>.Get();
        var pointLightList = ListPool<PointLightData>.Get();
        var pointShadowRequests = ListPool<ShadowRequest>.Get();
        var pointShadowTexelSizes = ListPool<Vector4>.Get();

        var cameraProjectionMatrix = camera.projectionMatrix;
        camera.ResetProjectionMatrix();

        // Setup lights/shadows
        for (var i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            var visibleLight = cullingResults.visibleLights[i];
            var light = visibleLight.light;
            var cascadeCount = 0;
            var shadowIndex = -1;

            if (visibleLight.lightType == LightType.Directional)
            {
                var lightRotation = visibleLight.localToWorldMatrix.rotation;
                var lightToWorld = Matrix4x4.Rotate(lightRotation);

                if (light.shadows != LightShadows.None && cullingResults.GetShadowCasterBounds(i, out var bounds))
                {
                    Matrix4x4 viewMatrix, projectionMatrix;
                    ShadowSplitData shadowSplitData;
                    for (var j = 0; j < settings.ShadowCascades; j++)
                    {
                        if (settings.CloseFit)
                        {
                            viewMatrix = lightToWorld.inverse;

                            var cascadeStart = j == 0 ? camera.nearClipPlane : (settings.ShadowDistance - camera.nearClipPlane) * settings.ShadowCascadeSplits[j - 1];
                            var cascadeEnd = (j == settings.ShadowCascades - 1) ? settings.ShadowDistance : (settings.ShadowDistance - camera.nearClipPlane) * settings.ShadowCascadeSplits[j];

                            // Transform camera bounds to light space
                            var minValue = Vector3.positiveInfinity;
                            var maxValue = Vector3.negativeInfinity;
                            for (var z = 0; z < 2; z++)
                            {
                                for (var y = 0; y < 2; y++)
                                {
                                    for (var x = 0; x < 2; x++)
                                    {
                                        var worldPoint = camera.ViewportToWorldPoint(new(x, y, z == 0 ? cascadeStart : cascadeEnd));
                                        var localPoint = viewMatrix.MultiplyPoint3x4(worldPoint);
                                        minValue = Vector3.Min(minValue, localPoint);
                                        maxValue = Vector3.Max(maxValue, localPoint);
                                    }
                                }
                            }

                            projectionMatrix = Matrix4x4.Ortho(minValue.x, maxValue.x, minValue.y, maxValue.y, minValue.z, maxValue.z);
                            viewMatrix.SetRow(2, -viewMatrix.GetRow(2));

                            // Calculate culling planes
                            var cullingPlanes = ListPool<Plane>.Get();

                            // First get the planes from the view projection matrix
                            var viewProjectionMatrix = projectionMatrix * viewMatrix;
                            GeometryUtility.CalculateFrustumPlanes(viewProjectionMatrix, frustumPlanes);
                            for (var k = 0; k < 6; k++)
                            {
                                // Skip near plane
                                if (k != 4)
                                    cullingPlanes.Add(frustumPlanes[k]);
                            }

                            // Now also add any main camera-frustum planes that are not facing away from the light
                            var lightDirection = -visibleLight.localToWorldMatrix.Forward();
                            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
                            for (var k = 0; k < 6; k++)
                            {
                                var plane = frustumPlanes[k];
                                if (Vector3.Dot(plane.normal, lightDirection) > 0.0f)
                                    cullingPlanes.Add(plane);
                            }

                            shadowSplitData = new ShadowSplitData()
                            {
                                cullingPlaneCount = cullingPlanes.Count,
                                shadowCascadeBlendCullingFactor = 1
                            };

                            for (var k = 0; k < cullingPlanes.Count; k++)
                            {
                                shadowSplitData.SetCullingPlane(k, cullingPlanes[k]);
                            }

                            ListPool<Plane>.Release(cullingPlanes);
                        }
                        else if (!cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(i, j, settings.ShadowCascades, settings.ShadowCascadeSplits, settings.DirectionalShadowResolution, light.shadowNearPlane, out viewMatrix, out projectionMatrix, out shadowSplitData))
                            continue;

                        cascadeCount++;
                        var directionalShadowRequest = new ShadowRequest(true, i, viewMatrix, projectionMatrix, shadowSplitData, 0);
                        directionalShadowRequests.Add(directionalShadowRequest);

                        var shadowMatrix = (projectionMatrix * viewMatrix * lightToWorld).ConvertToAtlasMatrix();
                        directionalShadowMatrices.Add(shadowMatrix);

                        var width = projectionMatrix.OrthoWidth();
                        var height = projectionMatrix.OrthoHeight();
                        var near = projectionMatrix.OrthoNear();
                        var far = projectionMatrix.OrthoFar();
                        directionalShadowTexelSizes.Add(new(width, height, near, far));
                    }

                    if (cascadeCount > 0)
                        shadowIndex = directionalShadowRequests.Count - cascadeCount;
                }

                var directionalLightData = new DirectionalLightData((Vector4)light.color.linear * light.intensity, shadowIndex, -light.transform.forward, cascadeCount, lightToWorld.inverse);
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

                var pointLightData = new PointLightData(light.transform.position, light.range, (Vector4)light.color.linear * light.intensity, shadowIndex, visibleFaceMask, near, far);
                pointLightList.Add(pointLightData);
            }
        }

        camera.projectionMatrix = cameraProjectionMatrix;

        // Render Shadows
        command.BeginSample("Render Shadows");
        command.SetGlobalDepthBias(settings.ShadowBias, settings.ShadowSlopeBias);

        if (directionalShadowRequests.Count > 0)
        {
            // Process directional shadows
            command.BeginSample("Directional Shadows");
            command.SetGlobalFloat("_ZClip", 0);

            // Setup shadow map for directional shadows
            var directionalShadowsDescriptor = new RenderTextureDescriptor(settings.DirectionalShadowResolution, settings.DirectionalShadowResolution, RenderTextureFormat.Shadowmap, 16)
            {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = directionalShadowRequests.Count,
            };

            command.GetTemporaryRT(directionalShadowsId, directionalShadowsDescriptor);
            command.SetRenderTarget(directionalShadowsId, 0, CubemapFace.Unknown, -1);
            command.ClearRenderTarget(true, false, Color.clear);

            for (var i = 0; i < directionalShadowRequests.Count; i++)
            {
                var shadowRequest = directionalShadowRequests[i];
                command.BeginSample(cascadeStrings.GetString(i % 4));
                command.SetRenderTarget(directionalShadowsId, 0, CubemapFace.Unknown, i);

                command.SetViewProjectionMatrices(shadowRequest.ViewMatrix, shadowRequest.ProjectionMatrix);
                command.BeginSample("Render Shadows");
                context.ExecuteCommandBuffer(command);
                command.Clear();

                var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, shadowRequest.VisibleLightIndex) { splitData = shadowRequest.ShadowSplitData };
                context.DrawShadows(ref shadowDrawingSettings);

                command.EndSample("Render Shadows");
                command.EndSample(cascadeStrings.GetString(i % 4));
            }

            command.SetGlobalFloat("_ZClip", 1);
            command.EndSample("Directional Shadows");
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

            command.BeginSample("Point Shadows");
            command.GetTemporaryRT(pointShadowsId, pointShadowsDescriptor);
            command.SetRenderTarget(pointShadowsId, 0, CubemapFace.Unknown, -1);
            command.ClearRenderTarget(true, false, Color.clear);

            for (var i = 0; i < pointShadowRequests.Count; i++)
            {
                command.BeginSample($"Light {i / 6}");
                command.BeginSample($"Face {i % 6}");

                var shadowRequest = pointShadowRequests[i];
                if (!shadowRequest.IsValid)
                    continue;

                command.SetRenderTarget(pointShadowsId, 0, CubemapFace.Unknown, i);

                command.SetViewProjectionMatrices(shadowRequest.ViewMatrix, shadowRequest.ProjectionMatrix);
                command.BeginSample("Render Shadows");
                context.ExecuteCommandBuffer(command);
                command.Clear();

                var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, shadowRequest.VisibleLightIndex) { splitData = shadowRequest.ShadowSplitData };
                context.DrawShadows(ref shadowDrawingSettings);

                command.EndSample("Render Shadows");
                command.EndSample($"Light {i % 6}");
                command.EndSample($"Light {i / 6}");
            }

            command.EndSample("Point Shadows");
        }

        command.SetGlobalDepthBias(0f, 0f);

        // Set directional light data
        command.ExpandAndSetComputeBufferData(ref directionalLightBuffer, directionalLightList);
        command.SetGlobalBuffer("_DirectionalLights", directionalLightBuffer);
        command.SetGlobalInt("_DirectionalLightCount", directionalLightList.Count);
        ListPool<DirectionalLightData>.Release(directionalLightList);

        command.SetGlobalTexture(directionalShadowsId, directionalShadowRequests.Count > 0 ? directionalShadowsId : emptyArray);
        ListPool<ShadowRequest>.Release(directionalShadowRequests);

        // Update directional shadow matrices
        command.ExpandAndSetComputeBufferData(ref directionalMatrixBuffer, directionalShadowMatrices);
        command.SetGlobalBuffer("_DirectionalMatrices", directionalMatrixBuffer);
        ListPool<Matrix4x4>.Release(directionalShadowMatrices);

        // Update directional shadow texel sizes
        command.ExpandAndSetComputeBufferData(ref directionalTexelSizeBuffer, directionalShadowTexelSizes);
        command.SetBufferData(directionalTexelSizeBuffer, directionalShadowTexelSizes);
        command.SetGlobalBuffer("_DirectionalShadowTexelSizes", directionalTexelSizeBuffer);
        ListPool<Vector4>.Release(directionalShadowTexelSizes);

        // Set point light data
        command.ExpandAndSetComputeBufferData(ref pointLightBuffer, pointLightList);
        command.SetGlobalBuffer("_PointLights", pointLightBuffer);
        command.SetGlobalInt("_PointLightCount", pointLightList.Count);
        ListPool<PointLightData>.Release(pointLightList);

        command.SetGlobalTexture(pointShadowsId, pointShadowRequests.Count > 0 ? pointShadowsId : emptyCubemapArray);
        ListPool<ShadowRequest>.Release(pointShadowRequests);

        command.ExpandAndSetComputeBufferData(ref pointTexelSizeBuffer, pointShadowTexelSizes);
        command.SetGlobalBuffer("_PointShadowTexelSizes", pointTexelSizeBuffer);

        ListPool<Vector4>.Release(pointShadowTexelSizes);

        command.SetGlobalInt("_PcfSamples", settings.PcfSamples);
        command.SetGlobalFloat("_PcfRadius", settings.PcfRadius);
        command.SetGlobalInt("_BlockerSamples", settings.BlockerSamples);
        command.SetGlobalFloat("_BlockerRadius", settings.BlockerRadius);
        command.SetGlobalFloat("_PcssSoftness", settings.PcssSoftness);
        command.EndSample("Render Shadows");
    }
}
