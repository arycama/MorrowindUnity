using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Arycama.CustomRenderPipeline;
using UnityEngine.Experimental.Rendering;
using CommandBufferPool = Arycama.CustomRenderPipeline.CommandBufferPool;
using System.Collections.Generic;

public class MorrowindRenderPipeline : CustomRenderPipeline
{
    private static readonly IndexedString blueNoise1DIds = new("STBN/Scalar/stbn_scalar_2Dx1Dx1D_128x128x64x1_");
    private static readonly IndexedString blueNoise2DIds = new("STBN/Vec2/stbn_vec2_2Dx1D_128x128x64_");

    private readonly MorrowindRenderPipelineAsset renderPipelineAsset;

    private readonly DynamicResolution dynamicResolution;
    private readonly CustomSampler frameTimeSampler;
    private readonly Recorder frameTimeRecorder;

    private readonly LightingSetup lightingSetup;
    private readonly ClusteredLightCulling clusteredLightCulling;
    private readonly VolumetricLighting volumetricLighting;
    private readonly CameraMotionVectors cameraMotionVectors;
    private readonly AmbientOcclusion ambientOcclusion;
    private readonly DepthOfField depthOfField;
    private readonly AutoExposure autoExposure;
    private readonly TemporalAA temporalAA;
    private readonly Bloom bloom;
    private readonly Tonemapping tonemapping;
    private readonly Material skyClearMaterial;

    private readonly RenderGraph renderGraph = new();

    public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset)
    {
        this.renderPipelineAsset = renderPipelineAsset;

        GraphicsSettings.lightsUseLinearIntensity = true;
        GraphicsSettings.lightsUseColorTemperature = true;
        GraphicsSettings.disableBuiltinCustomRenderTextureUpdate = true;
        GraphicsSettings.realtimeDirectRectangularAreaLights = true;

        lightingSetup = new(renderPipelineAsset.ShadowSettings, renderGraph);
        clusteredLightCulling = new(renderPipelineAsset.ClusteredLightingSettings, renderGraph);
        volumetricLighting = new(renderPipelineAsset.VolumetricLightingSettings, renderGraph);

        cameraMotionVectors = new(renderGraph);
        ambientOcclusion = new(renderPipelineAsset.AmbientOcclusionSettings, renderGraph);
        depthOfField = new(renderPipelineAsset.DepthOfFieldSettings, renderPipelineAsset.LensSettings, renderGraph);
        autoExposure = new AutoExposure(renderPipelineAsset.AutoExposureSettings, renderPipelineAsset.LensSettings, renderGraph);
        temporalAA = new(renderPipelineAsset.TemporalAASettings, renderGraph);
        bloom = new(renderPipelineAsset.BloomSettings, renderGraph);
        tonemapping = new(renderPipelineAsset.TonemappingSettings, renderPipelineAsset.BloomSettings, renderPipelineAsset.LensSettings, renderGraph);

        SupportedRenderingFeatures.active = new SupportedRenderingFeatures()
        {
            defaultMixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.None,
            editableMaterialRenderQueue = false,
            enlighten = false,
            lightmapBakeTypes = LightmapBakeType.Realtime,
            lightmapsModes = LightmapsMode.NonDirectional,
            lightProbeProxyVolumes = false,
            mixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.None,
            motionVectors = true,
            overridesEnvironmentLighting = false,
            overridesFog = false,
            overrideShadowmaskMessage = null,
            overridesLODBias = false,
            overridesMaximumLODLevel = false,
            overridesOtherLightingSettings = true,
            overridesRealtimeReflectionProbes = true,
            overridesShadowmask = true,
            particleSystemInstancing = true,
            receiveShadows = true,
            reflectionProbeModes = SupportedRenderingFeatures.ReflectionProbeModes.None,
            reflectionProbes = false,
            rendererPriority = false,
            rendererProbes = false,
            rendersUIOverlay = false,
            autoAmbientProbeBaking = false,
            autoDefaultReflectionProbeBaking = false,
            enlightenLightmapper = false,
            reflectionProbesBlendDistance = false,
        };

        dynamicResolution = new(renderPipelineAsset.DynamicResolutionSettings);
        frameTimeSampler = CustomSampler.Create("Frame Time", true);
        frameTimeRecorder = frameTimeSampler.GetRecorder();

        skyClearMaterial = new Material(Shader.Find("Hidden/SkyClear")) { hideFlags = HideFlags.HideAndDontSave };
    }

    protected override void Dispose(bool disposing)
    {
        volumetricLighting.Release();
        temporalAA.Release();
        dynamicResolution.Release();
        renderGraph.Release();
    }

    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        GraphicsSettings.useScriptableRenderPipelineBatching = renderPipelineAsset.EnableSrpBatcher;

        dynamicResolution.Update(frameTimeRecorder.gpuElapsedNanoseconds);

        foreach (var camera in cameras)
            RenderCamera(camera, context);

        var command = CommandBufferPool.Get("Render Camera");
        renderGraph.Execute(command, context);
        context.ExecuteCommandBuffer(command);
        CommandBufferPool.Release(command);

        context.Submit();
        renderGraph.ReleaseHandles();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras) { }

    private void RenderCamera(Camera camera, ScriptableRenderContext context)
    {
        camera.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;

        temporalAA.OnPreRender(camera, dynamicResolution.ScaleFactor, out var previousMatrix);

        if (!camera.TryGetCullingParameters(out var cullingParameters))
            return;

        var scaledWidth = (int)(camera.pixelWidth * dynamicResolution.ScaleFactor);
        var scaledHeight = (int)(camera.pixelHeight * dynamicResolution.ScaleFactor);

        BeginCameraRendering(context, camera);

        cullingParameters.shadowDistance = renderPipelineAsset.ShadowSettings.ShadowDistance;
        cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.DisablePerObjectCulling | CullingOptions.ShadowCasters;
        var cullingResults = context.Cull(ref cullingParameters);

        var lightingSetupResult = lightingSetup.Render(cullingResults, camera);

        // Camera setup
        var blueNoise1D = Resources.Load<Texture2D>(blueNoise1DIds.GetString(Time.renderedFrameCount % 64));
        var blueNoise2D = Resources.Load<Texture2D>(blueNoise2DIds.GetString(Time.renderedFrameCount % 64));
        var scaledResolution = new Vector4(scaledWidth, scaledHeight, 1.0f / scaledWidth, 1.0f / scaledHeight);

        using (var pass = renderGraph.AddRenderPass<GlobalRenderPass>("Setup Globals"))
        {
            var data = pass.SetRenderFunction<Pass0Data>((command, context, pass, data) => { context.SetupCameraProperties(data.camera); });
            data.camera = camera;
        }

        var invVpMatrix = (GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix).inverse;
        var ambientLightColor = RenderSettings.ambientLight.linear;

        var exposureBuffer = autoExposure.OnPreRender(camera);
        var clusteredLightCullingResult = clusteredLightCulling.Render(scaledWidth, scaledHeight, camera.nearClipPlane, camera.farClipPlane, lightingSetupResult, invVpMatrix);
        var volumetricLightingResult = volumetricLighting.Render(scaledWidth, scaledHeight, camera.farClipPlane, camera, clusteredLightCullingResult, lightingSetupResult, exposureBuffer, blueNoise1D, blueNoise2D, ambientLightColor, RenderSettings.fogColor.linear, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance, RenderSettings.fogDensity, RenderSettings.fog ? (float)RenderSettings.fogMode : 0.0f, previousMatrix, invVpMatrix);

        // Opaque
        var cameraTarget = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.B10G11R11_UFloatPack32);
        var cameraDepth = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.D32_SFloat_S8_UInt);
        using (var pass = renderGraph.AddRenderPass<ObjectRenderPass>("Render Opaque"))
        {
            pass.Initialize("SRPDefaultUnlit", context, cullingResults, camera, RenderQueueRange.opaque, SortingCriteria.CommonOpaque, PerObjectData.None, true);
            pass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Clear, RenderBufferStoreAction.Store);
            pass.WriteTexture("", cameraTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            pass.ReadTexture("_VolumetricLighting", volumetricLightingResult.volumetricLighting);

            pass.ReadTexture("_LightClusterIndices", clusteredLightCullingResult.lightClusterIndices);
            pass.ReadBuffer("_LightClusterList", clusteredLightCullingResult.lightList);

            pass.ReadTexture("_DirectionalShadows", lightingSetupResult.directionalShadows);
            pass.ReadTexture("_PointShadows", lightingSetupResult.pointShadows);
            pass.ReadBuffer("_DirectionalMatrices", lightingSetupResult.directionalMatrices);
            pass.ReadBuffer("_DirectionalLights", lightingSetupResult.directionalLights);
            pass.ReadBuffer("_PointLights", lightingSetupResult.pointLights);
            pass.ReadBuffer("_DirectionalShadowTexelSizes", lightingSetupResult.directionalShadowTexelSizes);

            var data = pass.SetRenderFunction<ObjectPassData>((command, context, pass, data) =>
            {
                pass.SetFloat(command, "_ClusterScale", data.clusteredLightCullingResult.clusterScale);
                pass.SetFloat(command, "_ClusterBias", data.clusteredLightCullingResult.clusterBias);
                pass.SetInt(command, "_TileSize", data.clusteredLightCullingResult.tileSize);

                pass.SetInt(command, "_DirectionalLightCount", data.lightingSetupResult.directionalLightCount);
                pass.SetInt(command, "_PointLightCount", data.lightingSetupResult.pointLightCount);

                pass.SetInt(command, "_PcfSamples", data.lightingSetupResult.pcfSamples);
                pass.SetFloat(command, "_PcfRadius", data.lightingSetupResult.pcfRadius);
                pass.SetInt(command, "_BlockerSamples", data.lightingSetupResult.blockerSamples);
                pass.SetFloat(command, "_BlockerRadius", data.lightingSetupResult.blockerRadius);
                pass.SetFloat(command, "_PcssSoftness", data.lightingSetupResult.pcssSoftness);

                pass.SetFloat(command, "_NonLinearDepth", data.volumetricLightingResult.nonLinearDepth);
                pass.SetFloat(command, "_VolumeWidth", data.volumetricLightingResult.volumeWidth);
                pass.SetFloat(command, "_VolumeHeight", data.volumetricLightingResult.volumeHeight);
                pass.SetFloat(command, "_VolumeSlices", data.volumetricLightingResult.volumeSlices);

                pass.SetConstantBuffer(command, "Exposure", data.exposureBuffer);

                pass.SetTexture(command, "_BlueNoise2D", data.blueNoise2D);

                pass.SetVector(command, "_Jitter", data.jitter);
                pass.SetVector(command, "_AmbientLightColor", data.ambientLightColor);
                pass.SetFloat(command, "_AoEnabled", data.aoEnabled);
                pass.SetVector(command, "_ScaledResolution", data.scaledResolution);
            });

            data.clusteredLightCullingResult = clusteredLightCullingResult;
            data.lightingSetupResult = lightingSetupResult;
            data.volumetricLightingResult = volumetricLightingResult;
            data.exposureBuffer = exposureBuffer;
            data.jitter = temporalAA.Jitter;
            data.ambientLightColor = ambientLightColor;
            data.aoEnabled = renderPipelineAsset.AmbientOcclusionSettings.Strength > 0.0f ? 1.0f : 0.0f;
            data.scaledResolution = scaledResolution;
            data.blueNoise2D = blueNoise2D;
        }

        // Motion Vectors
        var motionVectors = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.R16G16_SFloat);
        using (var pass = renderGraph.AddRenderPass<ObjectRenderPass>("Render Motion Vectors"))
        {
            pass.Initialize("MotionVectors", context, cullingResults, camera, RenderQueueRange.opaque, SortingCriteria.CommonOpaque, PerObjectData.MotionVectors);
            pass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.WriteTexture("", motionVectors, RenderBufferLoadAction.Clear, RenderBufferStoreAction.Store, Color.clear);
            pass.ReadTexture("_VolumetricLighting", volumetricLightingResult.volumetricLighting);

            pass.ReadTexture("_LightClusterIndices", clusteredLightCullingResult.lightClusterIndices);
            pass.ReadBuffer("_LightClusterList", clusteredLightCullingResult.lightList);

            pass.ReadTexture("_DirectionalShadows", lightingSetupResult.directionalShadows);
            pass.ReadTexture("_PointShadows", lightingSetupResult.pointShadows);
            pass.ReadBuffer("_DirectionalMatrices", lightingSetupResult.directionalMatrices);
            pass.ReadBuffer("_DirectionalLights", lightingSetupResult.directionalLights);
            pass.ReadBuffer("_PointLights", lightingSetupResult.pointLights);
            pass.ReadBuffer("_DirectionalShadowTexelSizes", lightingSetupResult.directionalShadowTexelSizes);

            var data = pass.SetRenderFunction<MotionVectorsPassData>((command, context, pass, data) =>
            {
                pass.SetFloat(command, "_ClusterScale", data.objectPassData.clusteredLightCullingResult.clusterScale);
                pass.SetFloat(command, "_ClusterBias", data.objectPassData.clusteredLightCullingResult.clusterBias);
                pass.SetInt(command, "_TileSize", data.objectPassData.clusteredLightCullingResult.tileSize);

                pass.SetInt(command, "_DirectionalLightCount", data.objectPassData.lightingSetupResult.directionalLightCount);
                pass.SetInt(command, "_PointLightCount", data.objectPassData.lightingSetupResult.pointLightCount);

                pass.SetInt(command, "_PcfSamples", data.objectPassData.lightingSetupResult.pcfSamples);
                pass.SetFloat(command, "_PcfRadius", data.objectPassData.lightingSetupResult.pcfRadius);
                pass.SetInt(command, "_BlockerSamples", data.objectPassData.lightingSetupResult.blockerSamples);
                pass.SetFloat(command, "_BlockerRadius", data.objectPassData.lightingSetupResult.blockerRadius);
                pass.SetFloat(command, "_PcssSoftness", data.objectPassData.lightingSetupResult.pcssSoftness);

                pass.SetFloat(command, "_NonLinearDepth", data.objectPassData.volumetricLightingResult.nonLinearDepth);
                pass.SetFloat(command, "_VolumeWidth", data.objectPassData.volumetricLightingResult.volumeWidth);
                pass.SetFloat(command, "_VolumeHeight", data.objectPassData.volumetricLightingResult.volumeHeight);
                pass.SetFloat(command, "_VolumeSlices", data.objectPassData.volumetricLightingResult.volumeSlices);

                pass.SetConstantBuffer(command, "Exposure", data.objectPassData.exposureBuffer);

                pass.SetTexture(command, "_BlueNoise2D", data.objectPassData.blueNoise2D);

                pass.SetVector(command, "_Jitter", data.objectPassData.jitter);
                pass.SetVector(command, "_AmbientLightColor", data.objectPassData.ambientLightColor);
                pass.SetFloat(command, "_AoEnabled", data.objectPassData.aoEnabled);
                pass.SetVector(command, "_ScaledResolution", data.objectPassData.scaledResolution);
                pass.SetMatrix(command, "_NonJitteredVPMatrix", data.nonJitteredVpMatrix);
                pass.SetMatrix(command, "_PreviousVPMatrix", data.previousVpMatrix);
            });

            data.objectPassData.clusteredLightCullingResult = clusteredLightCullingResult;
            data.objectPassData.lightingSetupResult = lightingSetupResult;
            data.objectPassData.volumetricLightingResult = volumetricLightingResult;
            data.objectPassData.exposureBuffer = exposureBuffer;
            data.objectPassData.jitter = temporalAA.Jitter;
            data.objectPassData.ambientLightColor = ambientLightColor;
            data.objectPassData.aoEnabled = renderPipelineAsset.AmbientOcclusionSettings.Strength > 0.0f ? 1.0f : 0.0f;
            data.objectPassData.scaledResolution = scaledResolution;
            data.nonJitteredVpMatrix = camera.nonJitteredProjectionMatrix;
            data.previousVpMatrix = previousMatrix;
            data.objectPassData.blueNoise2D = blueNoise2D;
        }

        // Sky clear color
        using (var pass = renderGraph.AddRenderPass<FullscreenRenderPass>("Clear Color"))
        {
            pass.Material = skyClearMaterial;
            pass.Index = 0;
            pass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 1.0f, RenderTargetFlags.ReadOnlyDepth);
            pass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.ReadTexture("_VolumetricLighting", volumetricLightingResult.volumetricLighting);

            var data = pass.SetRenderFunction<SkyPassData>((command, context, pass, data) =>
            {
                pass.SetFloat(command, "_NonLinearDepth", data.volumetricLightingResult.nonLinearDepth);
                pass.SetFloat(command, "_VolumeWidth", data.volumetricLightingResult.volumeWidth);
                pass.SetFloat(command, "_VolumeHeight", data.volumetricLightingResult.volumeHeight);
                pass.SetFloat(command, "_VolumeSlices", data.volumetricLightingResult.volumeSlices);
                pass.SetVector(command, "_ScaledResolution", data.scaledResolution);
            });

            data.volumetricLightingResult = volumetricLightingResult;
            data.scaledResolution = scaledResolution;
        }

        // Sky
        using (var pass = renderGraph.AddRenderPass<ObjectRenderPass>("Render Sky"))
        {
            pass.Initialize("Sky", context, cullingResults, camera, RenderQueueRange.all);
            pass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 1.0f, RenderTargetFlags.ReadOnlyDepth);
            pass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.ReadTexture("_VolumetricLighting", volumetricLightingResult.volumetricLighting);

            var data = pass.SetRenderFunction<SkyPassData>((command, context, pass, data) =>
            {
                pass.SetFloat(command, "_NonLinearDepth", data.volumetricLightingResult.nonLinearDepth);
                pass.SetFloat(command, "_VolumeWidth", data.volumetricLightingResult.volumeWidth);
                pass.SetFloat(command, "_VolumeHeight", data.volumetricLightingResult.volumeHeight);
                pass.SetFloat(command, "_VolumeSlices", data.volumetricLightingResult.volumeSlices);

                pass.SetConstantBuffer(command, "Exposure", data.exposureBuffer);
                pass.SetVector(command, "_ScaledResolution", data.scaledResolution);
            });

            data.volumetricLightingResult = volumetricLightingResult;
            data.exposureBuffer = exposureBuffer;
            data.scaledResolution = scaledResolution;
        }

        // Before transparent post processing
        cameraMotionVectors.Render(motionVectors, cameraDepth, scaledWidth, scaledHeight, camera.nonJitteredProjectionMatrix, previousMatrix, invVpMatrix);
        ambientOcclusion.Render(camera, cameraDepth, cameraTarget, dynamicResolution.ScaleFactor, volumetricLightingResult, blueNoise2D, invVpMatrix);

        // Copy scene texture
        var sceneTexture = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.B10G11R11_UFloatPack32);
        using (var pass = renderGraph.AddRenderPass<GlobalRenderPass>("Copy Scene Texture"))
        {
            pass.ReadTexture("", cameraTarget);
            pass.WriteTexture("", sceneTexture);

            var data = pass.SetRenderFunction<Pass1Data>((command, context, pass, data) => { command.CopyTexture(data.cameraTarget, data.sceneTexture); });

            data.cameraTarget = cameraTarget;
            data.sceneTexture = sceneTexture;
        }

        // Transparents
        using (var pass = renderGraph.AddRenderPass<ObjectRenderPass>("Render Transparent"))
        {
            pass.Initialize("SRPDefaultUnlit", context, cullingResults, camera, RenderQueueRange.transparent, SortingCriteria.CommonTransparent);
            pass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 1.0f, RenderTargetFlags.ReadOnlyDepth);
            pass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.ReadTexture("_SceneTexture", sceneTexture);
            pass.ReadTexture("_CameraDepth", cameraDepth);

            pass.ReadTexture("_VolumetricLighting", volumetricLightingResult.volumetricLighting);

            pass.ReadTexture("_LightClusterIndices", clusteredLightCullingResult.lightClusterIndices);
            pass.ReadBuffer("_LightClusterList", clusteredLightCullingResult.lightList);

            pass.ReadTexture("_DirectionalShadows", lightingSetupResult.directionalShadows);
            pass.ReadTexture("_PointShadows", lightingSetupResult.pointShadows);
            pass.ReadBuffer("_DirectionalMatrices", lightingSetupResult.directionalMatrices);
            pass.ReadBuffer("_DirectionalLights", lightingSetupResult.directionalLights);
            pass.ReadBuffer("_PointLights", lightingSetupResult.pointLights);
            pass.ReadBuffer("_DirectionalShadowTexelSizes", lightingSetupResult.directionalShadowTexelSizes);

            var data = pass.SetRenderFunction<ObjectPassData>((command, context, pass, data) =>
            {
                pass.SetFloat(command, "_ClusterScale", data.clusteredLightCullingResult.clusterScale);
                pass.SetFloat(command, "_ClusterBias", data.clusteredLightCullingResult.clusterBias);
                pass.SetInt(command, "_TileSize", data.clusteredLightCullingResult.tileSize);

                pass.SetInt(command, "_DirectionalLightCount", data.lightingSetupResult.directionalLightCount);
                pass.SetInt(command, "_PointLightCount", data.lightingSetupResult.pointLightCount);

                pass.SetInt(command, "_PcfSamples", data.lightingSetupResult.pcfSamples);
                pass.SetFloat(command, "_PcfRadius", data.lightingSetupResult.pcfRadius);
                pass.SetInt(command, "_BlockerSamples", data.lightingSetupResult.blockerSamples);
                pass.SetFloat(command, "_BlockerRadius", data.lightingSetupResult.blockerRadius);
                pass.SetFloat(command, "_PcssSoftness", data.lightingSetupResult.pcssSoftness);

                pass.SetFloat(command, "_NonLinearDepth", data.volumetricLightingResult.nonLinearDepth);
                pass.SetFloat(command, "_VolumeWidth", data.volumetricLightingResult.volumeWidth);
                pass.SetFloat(command, "_VolumeHeight", data.volumetricLightingResult.volumeHeight);
                pass.SetFloat(command, "_VolumeSlices", data.volumetricLightingResult.volumeSlices);

                pass.SetConstantBuffer(command, "Exposure", data.exposureBuffer);

                pass.SetTexture(command, "_BlueNoise2D", data.blueNoise2D);

                pass.SetVector(command, "_Jitter", data.jitter);
                pass.SetVector(command, "_AmbientLightColor", data.ambientLightColor);
                pass.SetFloat(command, "_AoEnabled", data.aoEnabled);
                pass.SetVector(command, "_ScaledResolution", data.scaledResolution);
            });

            data.clusteredLightCullingResult = clusteredLightCullingResult;
            data.lightingSetupResult = lightingSetupResult;
            data.volumetricLightingResult = volumetricLightingResult;
            data.exposureBuffer = exposureBuffer;
            data.jitter = temporalAA.Jitter;
            data.ambientLightColor = ambientLightColor;
            data.aoEnabled = renderPipelineAsset.AmbientOcclusionSettings.Strength > 0.0f ? 1.0f : 0.0f;
            data.scaledResolution = scaledResolution;
            data.blueNoise2D = blueNoise2D;
        }

        // After transparent post processing
        autoExposure.Render(cameraTarget, scaledWidth, scaledHeight, camera);

        var dofResult = depthOfField.Render(scaledWidth, scaledHeight, camera.fieldOfView, cameraTarget, cameraDepth);
        var taa = temporalAA.Render(camera, dofResult, motionVectors, dynamicResolution.ScaleFactor);
        var bloomResult = bloom.Render(camera, taa);

        tonemapping.Render(taa, bloomResult, camera.cameraType == CameraType.SceneView, camera.pixelWidth, camera.pixelHeight);

        // Only in editor
        using (var pass = renderGraph.AddRenderPass<GlobalRenderPass>("Render Gizmos"))
        {
            var data = pass.SetRenderFunction<Pass2Data>((command, context, pass, data) =>
            {
                context.ExecuteCommandBuffer(command);
                command.Clear();

                if (UnityEditor.Handles.ShouldRenderGizmos())
                {
                    context.DrawGizmos(data.camera, GizmoSubset.PreImageEffects);
                    context.DrawGizmos(data.camera, GizmoSubset.PostImageEffects);
                }
            });

            data.camera = camera;
        }
    }

    private class Pass0Data
    {
        internal Camera camera;
    }

    private class Pass1Data
    {
        internal RTHandle cameraTarget;
        internal RTHandle sceneTexture;
    }

    private class Pass2Data
    {
        internal Camera camera;
    }

    private class ObjectPassData
    {
        internal ClusteredLightCulling.Result clusteredLightCullingResult;
        internal LightingSetup.Result lightingSetupResult;
        internal VolumetricLighting.Result volumetricLightingResult;
        internal BufferHandle exposureBuffer;
        internal Vector2 jitter;
        internal Color ambientLightColor;
        internal float aoEnabled;
        internal Vector4 scaledResolution;
        internal Texture2D blueNoise2D;
    }

    private class MotionVectorsPassData
    {
        internal ObjectPassData objectPassData = new();
        internal Matrix4x4 nonJitteredVpMatrix;
        internal Matrix4x4 previousVpMatrix;
    }

    private class SkyPassData
    {
        internal VolumetricLighting.Result volumetricLightingResult;
        internal BufferHandle exposureBuffer;
        internal Vector4 scaledResolution;
    }
}
