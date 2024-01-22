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

        GraphicsSettings.useScriptableRenderPipelineBatching = renderPipelineAsset.EnableSrpBatcher;
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

        skyClearMaterial = new Material(Shader.Find("Hidden/SkyClear")) { hideFlags = HideFlags.HideAndDontSave };
    }

    protected override void Dispose(bool disposing)
    {
        lightingSetup.Release();
        volumetricLighting.Release();
        temporalAA.Release();
        dynamicResolution.Release();
        renderGraph.Release();
    }

    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        dynamicResolution.Update(frameTimeSampler.GetRecorder().gpuElapsedNanoseconds);

        foreach (var camera in cameras)
            RenderCamera(camera, context);

        var command = CommandBufferPool.Get("Render Camera");
        command.BeginSample(frameTimeSampler);
        renderGraph.Execute(command, context);
        command.EndSample(frameTimeSampler);
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

        lightingSetup.Render(cullingResults, camera);

        // Camera setup
        var blueNoise1D = Resources.Load<Texture2D>(blueNoise1DIds.GetString(Time.renderedFrameCount % 64));
        var blueNoise2D = Resources.Load<Texture2D>(blueNoise2DIds.GetString(Time.renderedFrameCount % 64));

        using (var pass = renderGraph.AddRenderPass<GlobalRenderPass>())
        {
            pass.RenderPass.SetRenderFunction((command, context) =>
            {
                pass.RenderPass.SetTexture(command, "_BlueNoise1D", blueNoise1D);
                pass.RenderPass.SetTexture(command, "_BlueNoise2D", blueNoise2D);

                pass.RenderPass.SetVector(command, "_Jitter", temporalAA.Jitter);
                pass.RenderPass.SetVector(command, "_AmbientLightColor", RenderSettings.ambientLight.linear);
                pass.RenderPass.SetVector(command, "_FogColor", RenderSettings.fogColor.linear);

                pass.RenderPass.SetFloat(command, "_FogStartDistance", RenderSettings.fogStartDistance);
                pass.RenderPass.SetFloat(command, "_FogEndDistance", RenderSettings.fogEndDistance);
                pass.RenderPass.SetFloat(command, "_FogDensity", RenderSettings.fogDensity);
                pass.RenderPass.SetFloat(command, "_FogMode", (float)RenderSettings.fogMode);
                pass.RenderPass.SetFloat(command, "_FogEnabled", RenderSettings.fog ? 1.0f : 0.0f);
                pass.RenderPass.SetFloat(command, "_AoEnabled", renderPipelineAsset.AmbientOcclusionSettings.Strength > 0.0f ? 1.0f : 0.0f);
                pass.RenderPass.SetFloat(command, "_Scale", dynamicResolution.ScaleFactor);

                pass.RenderPass.SetVector(command, "_WaterAlbedo", renderPipelineAsset.waterAlbedo.linear);
                pass.RenderPass.SetVector(command, "_WaterExtinction", renderPipelineAsset.waterExtinction);

                command.SetGlobalMatrix("_NonJitteredVPMatrix", camera.nonJitteredProjectionMatrix);
                command.SetGlobalMatrix("_PreviousVPMatrix", previousMatrix);
                command.SetGlobalMatrix("_InvVPMatrix", (GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix).inverse);
                pass.RenderPass.SetInt(command, "_FrameCount", Time.renderedFrameCount);

                context.SetupCameraProperties(camera);
            });
        }

        clusteredLightCulling.Render(scaledWidth, scaledHeight, camera.nearClipPlane, camera.farClipPlane);
        var volumetricLightingTexture = volumetricLighting.Render(scaledWidth, scaledHeight, camera.farClipPlane, camera);

        // Opaque
        var cameraTarget = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.B10G11R11_UFloatPack32);
        var cameraDepth = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.D32_SFloat_S8_UInt);
        using (var pass = renderGraph.AddRenderPass<ObjectRenderPass>())
        {
            pass.RenderPass.Initialize("SRPDefaultUnlit", context, cullingResults, camera, RenderQueueRange.opaque, SortingCriteria.CommonOpaque, PerObjectData.None, true);
            pass.RenderPass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Clear, RenderBufferStoreAction.Store);
            pass.RenderPass.WriteTexture("", cameraTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            pass.RenderPass.ReadTexture("_VolumetricLighting", volumetricLightingTexture);
        }

        // Motion Vectors
        var motionVectors = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.R16G16_SFloat);
        using (var pass = renderGraph.AddRenderPass<ObjectRenderPass>())
        {
            pass.RenderPass.Initialize("SRPDefaultUnlit", context, cullingResults, camera, RenderQueueRange.opaque, SortingCriteria.CommonOpaque, PerObjectData.MotionVectors);
            pass.RenderPass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.RenderPass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.RenderPass.WriteTexture("", motionVectors, RenderBufferLoadAction.Clear, RenderBufferStoreAction.Store, Color.clear);
            pass.RenderPass.ReadTexture("_VolumetricLighting", volumetricLightingTexture);
        }

        // Sky clear color
        using (var pass = renderGraph.AddRenderPass<FullscreenRenderPass>())
        {
            pass.RenderPass.Material = skyClearMaterial;
            pass.RenderPass.Index = 0;
            pass.RenderPass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 1.0f, RenderTargetFlags.ReadOnlyDepth);
            pass.RenderPass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.RenderPass.ReadTexture("_VolumetricLighting", volumetricLightingTexture);
        }

        // Sky
        using (var pass = renderGraph.AddRenderPass<ObjectRenderPass>())
        {
            pass.RenderPass.Initialize("Sky", context, cullingResults, camera, RenderQueueRange.all);
            pass.RenderPass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 1.0f, RenderTargetFlags.ReadOnlyDepth);
            pass.RenderPass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.RenderPass.ReadTexture("_VolumetricLighting", volumetricLightingTexture);
        }

        // Before transparent post processing
        cameraMotionVectors.Render(motionVectors, cameraDepth);
        ambientOcclusion.Render(camera, cameraDepth, cameraTarget, dynamicResolution.ScaleFactor, volumetricLightingTexture);

        // Copy scene texture
        var sceneTexture = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.B10G11R11_UFloatPack32);
        using (var pass = renderGraph.AddRenderPass<GlobalRenderPass>())
        {
            pass.RenderPass.SetRenderFunction((command, context) => { command.CopyTexture(cameraTarget, sceneTexture); });
        }

        // Transparents
        using (var pass = renderGraph.AddRenderPass<ObjectRenderPass>())
        {
            pass.RenderPass.Initialize("SRPDefaultUnlit", context, cullingResults, camera, RenderQueueRange.transparent, SortingCriteria.CommonTransparent);
            pass.RenderPass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 1.0f, RenderTargetFlags.ReadOnlyDepth);
            pass.RenderPass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.RenderPass.ReadTexture("_SceneTexture", sceneTexture);
            pass.RenderPass.ReadTexture("_CameraDepth", cameraDepth);
            pass.RenderPass.ReadTexture("_VolumetricLighting", volumetricLightingTexture);
        }

        // After transparent post processing
        autoExposure.Render(cameraTarget, scaledWidth, scaledHeight);

        var dofResult = depthOfField.Render(scaledWidth, scaledHeight, camera.fieldOfView, cameraTarget, cameraDepth);
        var taa = temporalAA.Render(camera, dofResult, motionVectors, dynamicResolution.ScaleFactor);
        var bloomResult = bloom.Render(camera, taa);

        tonemapping.Render(taa, bloomResult, camera.cameraType == CameraType.SceneView, camera.pixelWidth, camera.pixelHeight);

        // Only in editor
        using (var pass = renderGraph.AddRenderPass<GlobalRenderPass>())
        {
            pass.RenderPass.SetRenderFunction((command, context) =>
            {
                context.ExecuteCommandBuffer(command);
                command.Clear();

                if (UnityEditor.Handles.ShouldRenderGizmos())
                {
                    context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
                    context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
                }
            });
        }
    }
}
