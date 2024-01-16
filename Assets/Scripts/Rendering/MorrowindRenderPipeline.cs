using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Arycama.CustomRenderPipeline;
using UnityEngine.Experimental.Rendering;
using CommandBufferPool = Arycama.CustomRenderPipeline.CommandBufferPool;
using RendererListDesc = UnityEngine.Rendering.RendererUtils.RendererListDesc;
using System.Collections.Generic;

public class MorrowindRenderPipeline : CustomRenderPipeline
{
    private static readonly IndexedString blueNoise1DIds = new("STBN/Scalar/stbn_scalar_2Dx1Dx1D_128x128x64x1_");
    private static readonly IndexedString blueNoise2DIds = new("STBN/Vec2/stbn_vec2_2Dx1D_128x128x64_");

    private readonly MorrowindRenderPipelineAsset renderPipelineAsset;

    private readonly DynamicResolution dynamicResolution;
    private CustomSampler frameTimeSampler;

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

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        
    }

    class Pass0Data { }
    class Pass1Data { }
    class Pass2Data { }
    class Pass3Data { }
    class Pass4Data { }
    class Pass5Data { }

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

        var setGlobalsPass = renderGraph.AddRenderPass<GlobalRenderPass>();
        var data0 = setGlobalsPass.SetRenderFunction<Pass0Data>((command, context, data) =>
        {
            setGlobalsPass.SetTexture(command, "_BlueNoise1D", blueNoise1D);
            setGlobalsPass.SetTexture(command, "_BlueNoise2D", blueNoise2D);

            setGlobalsPass.SetVector(command, "_Jitter", temporalAA.Jitter);
            setGlobalsPass.SetVector(command, "_AmbientLightColor", RenderSettings.ambientLight.linear);
            setGlobalsPass.SetVector(command, "_FogColor", RenderSettings.fogColor.linear);

            setGlobalsPass.SetFloat(command, "_FogStartDistance", RenderSettings.fogStartDistance);
            setGlobalsPass.SetFloat(command, "_FogEndDistance", RenderSettings.fogEndDistance);
            setGlobalsPass.SetFloat(command, "_FogDensity", RenderSettings.fogDensity);
            setGlobalsPass.SetFloat(command, "_FogMode", (float)RenderSettings.fogMode);
            setGlobalsPass.SetFloat(command, "_FogEnabled", RenderSettings.fog ? 1.0f : 0.0f);
            setGlobalsPass.SetFloat(command, "_AoEnabled", renderPipelineAsset.AmbientOcclusionSettings.Strength > 0.0f ? 1.0f : 0.0f);
            setGlobalsPass.SetFloat(command, "_Scale", dynamicResolution.ScaleFactor);

            setGlobalsPass.SetVector(command, "_WaterAlbedo", renderPipelineAsset.waterAlbedo.linear);
            setGlobalsPass.SetVector(command, "_WaterExtinction", renderPipelineAsset.waterExtinction);

            command.SetGlobalMatrix("_NonJitteredVPMatrix", camera.nonJitteredProjectionMatrix);
            command.SetGlobalMatrix("_PreviousVPMatrix", previousMatrix);
            command.SetGlobalMatrix("_InvVPMatrix", (GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix).inverse);
            setGlobalsPass.SetInt(command, "_FrameCount", Time.renderedFrameCount);

            context.SetupCameraProperties(camera);
        });

        clusteredLightCulling.Render(scaledWidth, scaledHeight, camera.nearClipPlane, camera.farClipPlane);
        volumetricLighting.Render(scaledWidth, scaledHeight, camera.farClipPlane, camera);

        // Opaque
        var cameraTarget = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.B10G11R11_UFloatPack32);
        var cameraDepth = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.D32_SFloat_S8_UInt);
        var opaquePass = renderGraph.AddRenderPass<ObjectRenderPass>();
        opaquePass.Initialize("SRPDefaultUnlit", context, cullingResults, camera, RenderQueueRange.opaque, SortingCriteria.CommonOpaque, PerObjectData.None, true);
        opaquePass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Clear, RenderBufferStoreAction.Store);
        opaquePass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Clear, RenderBufferStoreAction.Store);
        var data1 = opaquePass.SetRenderFunction<Pass1Data>((command, context, data) =>
        {
            opaquePass.Execute(command);
        });

        // Motion Vectors
        var motionVectors = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.R16G16_SFloat);
        var motionVectorPass = renderGraph.AddRenderPass<ObjectRenderPass>();
        motionVectorPass.Initialize("SRPDefaultUnlit", context, cullingResults, camera, RenderQueueRange.opaque, SortingCriteria.CommonOpaque, PerObjectData.MotionVectors);
        motionVectorPass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        motionVectorPass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        motionVectorPass.WriteTexture("", motionVectors, RenderBufferLoadAction.Clear, RenderBufferStoreAction.Store);

        var data2 = motionVectorPass.SetRenderFunction<Pass2Data>((command, context, data) =>
        {
            motionVectorPass.Execute(command);
        });

        // Sky
        var skyPass = renderGraph.AddRenderPass<ObjectRenderPass>();
        skyPass.Initialize("Sky", context, cullingResults, camera, RenderQueueRange.all);
        skyPass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 1.0f, RenderTargetFlags.ReadOnlyDepth);
        skyPass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        var data3 = skyPass.SetRenderFunction<Pass3Data>((command, context, data) =>
        {
            command.DrawProcedural(Matrix4x4.identity, skyClearMaterial, 0, MeshTopology.Triangles, 3);
            skyPass.Execute(command);
        });

        // Before transparent post processing
        cameraMotionVectors.Render(motionVectors, cameraDepth);
        ambientOcclusion.Render(camera, cameraDepth, cameraTarget, dynamicResolution.ScaleFactor);

        // Transparents
        var sceneTextureId = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.B10G11R11_UFloatPack32);
        var transparentPass = renderGraph.AddRenderPass<ObjectRenderPass>();
        transparentPass.Initialize("SRPDefaultUnlit", context, cullingResults, camera, RenderQueueRange.transparent, SortingCriteria.CommonTransparent);
        transparentPass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 1.0f, RenderTargetFlags.ReadOnlyDepth);
        transparentPass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        var data4 = transparentPass.SetRenderFunction<Pass4Data>((command, context, data) =>
        {
            // Copy scene texture
            command.CopyTexture(cameraTarget, sceneTextureId);
            transparentPass.SetTexture(command, "_SceneTexture", sceneTextureId);
            transparentPass.SetTexture(command, "_CameraDepth", cameraDepth);
            transparentPass.Execute(command);
        });

        // After transparent post processing
        autoExposure.Render(cameraTarget, scaledWidth, scaledHeight);

        var dofResult = depthOfField.Render(scaledWidth, scaledHeight, camera.fieldOfView, cameraTarget, cameraDepth);
        var taa = temporalAA.Render(camera, dofResult, motionVectors, dynamicResolution.ScaleFactor);
        var bloomResult = bloom.Render(camera, taa);

        tonemapping.Render(taa, bloomResult, camera.cameraType == CameraType.SceneView, camera.pixelWidth, camera.pixelHeight);

        // Only in editor
        var gizmosPass = renderGraph.AddRenderPass<GlobalRenderPass>();
        var data5 = gizmosPass.SetRenderFunction<Pass5Data>((command, context, data) =>
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
