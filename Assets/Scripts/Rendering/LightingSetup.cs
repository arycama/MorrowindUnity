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

    public void Render(CommandBuffer commandBuffer, CullingResults cullingResults, ScriptableRenderContext context, Camera camera)
    {
        var directionalLightList = ListPool<DirectionalLightData>.Get();
        var directionalShadowRequests = ListPool<ShadowRequest>.Get();
        var directionalShadowMatrices = ListPool<Matrix4x4>.Get();
        var directionalShadowTexelSizes = ListPool<Vector4>.Get();
        var pointLightList = ListPool<PointLightData>.Get();
        var pointShadowRequests = ListPool<ShadowRequest>.Get();
        var pointShadowTexelSizes = ListPool<Vector4>.Get();

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
                var worldToLight = lightToWorld.inverse;

                {
                    Vector3 minValue = Vector3.positiveInfinity, maxValue = Vector3.negativeInfinity;
                    for (var z = 0; z < 2; z++)
                    {
                        for (var y = 0; y < 2; y++)
                        {
                            for (var x = 0; x < 2; x++)
                            {
                                var worldPoint = camera.ViewportToWorldPoint(new(x, y, z == 0 ? camera.nearClipPlane : camera.farClipPlane));
                                var localPoint = worldToLight.MultiplyPoint3x4(worldPoint);
                                minValue = Vector3.Min(minValue, localPoint);
                                maxValue = Vector3.Max(maxValue, localPoint);
                            }
                        }
                    }

                    var viewCenter = 0.5f * (maxValue + minValue);
                    var viewExtents = 0.5f * (maxValue - minValue);
                    var worldCenter = lightToWorld * viewCenter;
                   // lightToWorld = Matrix4x4.TRS(worldCenter, lightRotation, Vector3.one);
                   // worldToLight = lightToWorld.inverse;
                }

                if (light.shadows != LightShadows.None && cullingResults.GetShadowCasterBounds(i, out var bounds))
                {
                    Matrix4x4 viewMatrix, projectionMatrix;
                    ShadowSplitData shadowSplitData;
                    for (var j = 0; j < settings.ShadowCascades; j++)
                    {
                        if (settings.CloseFit)
                        {
                            viewMatrix = lightToWorld.inverse;

                            CalculateShadowBounds(camera, viewMatrix, j, out var currentMin, out var currentMax);

                            // LSCSM: To avoid redundant overlap, calculate the start of the previous shadow cascade, and shrink the current cascade to avoid overlap
                            if (settings.OverlapFix && j > 0)
                            {
                                CalculateShadowBounds(camera, viewMatrix, j - 1, out var previousMin, out var previousMax);

                                // In degenerate cases, such as the camera looking directly down with the light directly above, the previous frustum may be entirely inside the new frustum
                                // In this case, it's not possible to remove overlap as it would result in an 0 area cascade.
                                if (previousMin.x < currentMin.x || previousMax.x > currentMax.x)
                                {
                                    currentMin.x = Mathf.Max(currentMin.x, previousMax.x);
                                    currentMax.x = Mathf.Min(currentMax.x, previousMin.x);
                                }

                                if (previousMin.y < currentMin.y || previousMax.y > currentMax.y)
                                {
                                    currentMin.y = Mathf.Max(currentMin.y, previousMax.y);
                                    currentMax.y = Mathf.Min(currentMax.y, previousMin.y);
                                }
                            }

                            projectionMatrix = Matrix4x4.Ortho(currentMin.x, currentMax.x, currentMin.y, currentMax.y, currentMin.z, currentMax.z);
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

                        var shadowMatrix = (projectionMatrix * viewMatrix * worldToLight.inverse).ConvertToAtlasMatrix();
                        directionalShadowMatrices.Add(shadowMatrix);

                        var width = projectionMatrix.OrthoWidth();
                        var height = projectionMatrix.OrthoHeight();
                        var near = projectionMatrix.OrthoNear();
                        var far = projectionMatrix.OrthoFar();
                        directionalShadowTexelSizes.Add(new (width, height, near, far));
                    }

                    if (cascadeCount > 0)
                        shadowIndex = directionalShadowRequests.Count - cascadeCount;
                }

                var directionalLightData = new DirectionalLightData((Vector4)light.color.linear * light.intensity, shadowIndex, -light.transform.forward, cascadeCount, worldToLight);
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

        // Render Shadows
        commandBuffer.SetGlobalDepthBias(settings.ShadowBias, settings.ShadowSlopeBias);

        if (directionalShadowRequests.Count > 0)
        {
            // Process directional shadows
            commandBuffer.BeginSample("Directional Shadows");
            commandBuffer.SetGlobalFloat("_ZClip", 0);

            // Setup shadow map for directional shadows
            var directionalShadowsDescriptor = new RenderTextureDescriptor(settings.DirectionalShadowResolution, settings.DirectionalShadowResolution, RenderTextureFormat.Shadowmap, 16)
            {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = directionalShadowRequests.Count,
            };

            commandBuffer.GetTemporaryRT(directionalShadowsId, directionalShadowsDescriptor);
            commandBuffer.SetRenderTarget(directionalShadowsId, 0, CubemapFace.Unknown, -1);
            commandBuffer.ClearRenderTarget(true, false, Color.clear);

            for (var i = 0; i < directionalShadowRequests.Count; i++)
            {
                var shadowRequest = directionalShadowRequests[i];
                commandBuffer.BeginSample(cascadeStrings.GetString(i % 4));
                commandBuffer.SetRenderTarget(directionalShadowsId, 0, CubemapFace.Unknown, i);

                commandBuffer.SetViewProjectionMatrices(shadowRequest.ViewMatrix, shadowRequest.ProjectionMatrix);
                commandBuffer.BeginSample("Render Shadows");
                context.ExecuteCommandBuffer(commandBuffer);
                commandBuffer.Clear();

                var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, shadowRequest.VisibleLightIndex) { splitData = shadowRequest.ShadowSplitData };
                context.DrawShadows(ref shadowDrawingSettings);

                commandBuffer.EndSample("Render Shadows");
                commandBuffer.EndSample(cascadeStrings.GetString(i % 4));
            }

            commandBuffer.SetGlobalFloat("_ZClip", 1);
            commandBuffer.EndSample("Directional Shadows");
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

            commandBuffer.BeginSample("Point Shadows");
            commandBuffer.GetTemporaryRT(pointShadowsId, pointShadowsDescriptor);
            commandBuffer.SetRenderTarget(pointShadowsId, 0, CubemapFace.Unknown, -1);
            commandBuffer.ClearRenderTarget(true, false, Color.clear);

            for (var i = 0; i < pointShadowRequests.Count; i++)
            {
                commandBuffer.BeginSample($"Light {i / 6}");
                commandBuffer.BeginSample($"Face {i % 6}");

                var shadowRequest = pointShadowRequests[i];
                if (!shadowRequest.IsValid)
                    continue;

                commandBuffer.SetRenderTarget(pointShadowsId, 0, CubemapFace.Unknown, i);

                commandBuffer.SetViewProjectionMatrices(shadowRequest.ViewMatrix, shadowRequest.ProjectionMatrix);
                commandBuffer.BeginSample("Render Shadows");
                context.ExecuteCommandBuffer(commandBuffer);
                commandBuffer.Clear();

                var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, shadowRequest.VisibleLightIndex) { splitData = shadowRequest.ShadowSplitData };
                context.DrawShadows(ref shadowDrawingSettings);

                commandBuffer.EndSample("Render Shadows");
                commandBuffer.EndSample($"Light {i % 6}");
                commandBuffer.EndSample($"Light {i / 6}");
            }

            commandBuffer.EndSample("Point Shadows");
        }

        commandBuffer.SetGlobalDepthBias(0f, 0f);

        // Set directional light data
        commandBuffer.ExpandAndSetComputeBufferData(ref directionalLightBuffer, directionalLightList);
        commandBuffer.SetGlobalBuffer("_DirectionalLights", directionalLightBuffer);
        commandBuffer.SetGlobalInt("_DirectionalLightCount", directionalLightList.Count);
        ListPool<DirectionalLightData>.Release(directionalLightList);

        commandBuffer.SetGlobalTexture(directionalShadowsId, directionalShadowRequests.Count > 0 ? directionalShadowsId : emptyArray);
        ListPool<ShadowRequest>.Release(directionalShadowRequests);

        // Update directional shadow matrices
        commandBuffer.ExpandAndSetComputeBufferData(ref directionalMatrixBuffer, directionalShadowMatrices);
        commandBuffer.SetGlobalBuffer("_DirectionalMatrices", directionalMatrixBuffer);
        ListPool<Matrix4x4>.Release(directionalShadowMatrices);

        // Update directional shadow texel sizes
        commandBuffer.ExpandAndSetComputeBufferData(ref directionalTexelSizeBuffer, directionalShadowTexelSizes);
        commandBuffer.SetBufferData(directionalTexelSizeBuffer, directionalShadowTexelSizes);
        commandBuffer.SetGlobalBuffer("_DirectionalShadowTexelSizes", directionalTexelSizeBuffer);
        ListPool<Vector4>.Release(directionalShadowTexelSizes);

        // Set point light data
        commandBuffer.ExpandAndSetComputeBufferData(ref pointLightBuffer, pointLightList);
        commandBuffer.SetGlobalBuffer("_PointLights", pointLightBuffer);
        commandBuffer.SetGlobalInt("_PointLightCount", pointLightList.Count);
        ListPool<PointLightData>.Release(pointLightList);

        commandBuffer.SetGlobalTexture(pointShadowsId, pointShadowRequests.Count > 0 ? pointShadowsId : emptyCubemapArray);
        ListPool<ShadowRequest>.Release(pointShadowRequests);

        commandBuffer.ExpandAndSetComputeBufferData(ref pointTexelSizeBuffer, pointShadowTexelSizes);
        commandBuffer.SetGlobalBuffer("_PointShadowTexelSizes", pointTexelSizeBuffer);

        ListPool<Vector4>.Release(pointShadowTexelSizes);

        commandBuffer.SetGlobalInt("_PcfSamples", settings.PcfSamples);
        commandBuffer.SetGlobalFloat("_PcfRadius", settings.PcfRadius);
        commandBuffer.SetGlobalInt("_BlockerSamples", settings.BlockerSamples);
        commandBuffer.SetGlobalFloat("_BlockerRadius", settings.BlockerRadius);
        commandBuffer.SetGlobalFloat("_PcssSoftness", settings.PcssSoftness);
    }

    private void CalculateShadowBounds(Camera camera, Matrix4x4 viewMatrix, int j, out Vector3 minValue, out Vector3 maxValue)
    {
        var cascadeStart = j == 0 ? camera.nearClipPlane : (settings.ShadowDistance - camera.nearClipPlane) * settings.ShadowCascadeSplits[j - 1];
        var cascadeEnd = (j == settings.ShadowCascades - 1) ? settings.ShadowDistance : (settings.ShadowDistance - camera.nearClipPlane) * settings.ShadowCascadeSplits[j];

        // Transform camera bounds to light space
        minValue = Vector3.positiveInfinity;
        maxValue = Vector3.negativeInfinity;
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
    }
}
