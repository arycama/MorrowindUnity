using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Arycama.CustomRenderPipeline;
using UnityEngine.Experimental.Rendering;
using CommandBufferPool = Arycama.CustomRenderPipeline.CommandBufferPool;

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
    private readonly ObjectRenderer opaqueObjectRenderer;
    private readonly ObjectRenderer motionVectorsRenderer;
    private readonly ObjectRenderer skyRenderer;
    private readonly CameraMotionVectors cameraMotionVectors;
    private readonly AmbientOcclusion ambientOcclusion;
    private readonly ObjectRenderer transparentObjectRenderer;
    private readonly DepthOfField depthOfField;
    private readonly AutoExposure autoExposure;
    private readonly TemporalAA temporalAA;
    private readonly Bloom bloom;
    private readonly Tonemapping tonemapping;

    private readonly Material skyClearMaterial;

    private readonly RenderGraph renderGraph = new();
    //private readonly RTHandleSystem rtHandleSystem = new();

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
        opaqueObjectRenderer = new(RenderQueueRange.opaque, SortingCriteria.CommonOpaque, true, PerObjectData.None, "SRPDefaultUnlit", renderGraph);
        motionVectorsRenderer = new(RenderQueueRange.opaque, SortingCriteria.CommonOpaque, false, PerObjectData.MotionVectors, "MotionVectors", renderGraph);

        skyRenderer = new(RenderQueueRange.all, SortingCriteria.None, false, PerObjectData.None, "Sky", renderGraph);

        cameraMotionVectors = new(renderGraph);
        ambientOcclusion = new(renderPipelineAsset.AmbientOcclusionSettings, renderGraph);
        transparentObjectRenderer = new(RenderQueueRange.transparent, SortingCriteria.CommonTransparent, false, PerObjectData.None, "SRPDefaultUnlit", renderGraph);
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

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
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

    private void RenderCamera(Camera camera, ScriptableRenderContext context)
    {
        camera.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;

        temporalAA.OnPreRender(camera, dynamicResolution.ScaleFactor, out var previousMatrix);

        if (!camera.TryGetCullingParameters(out var cullingParameters))
            return;

        var scaledWidth = (int)(camera.pixelWidth * dynamicResolution.ScaleFactor);
        var scaledHeight = (int)(camera.pixelHeight * dynamicResolution.ScaleFactor);

        //rtHandleSystem.SetResolution(scaledWidth, scaledHeight);

        BeginCameraRendering(context, camera);

        cullingParameters.shadowDistance = renderPipelineAsset.ShadowSettings.ShadowDistance;
        cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.DisablePerObjectCulling | CullingOptions.ShadowCasters;
        var cullingResults = context.Cull(ref cullingParameters);

        lightingSetup.Render(cullingResults, camera);

        {
            var pass = renderGraph.AddRenderPass<GlobalRenderPass>();

            // More camera setup
            var blueNoise1D = Resources.Load<Texture2D>(blueNoise1DIds.GetString(Time.renderedFrameCount % 64));
            var blueNoise2D = Resources.Load<Texture2D>(blueNoise2DIds.GetString(Time.renderedFrameCount % 64));

            pass.SetRenderFunction((command, context) =>
            {
                pass.SetTexture(command, "_BlueNoise1D", blueNoise1D);
                pass.SetTexture(command, "_BlueNoise2D", blueNoise2D);

                pass.SetVector(command, "_Jitter", temporalAA.Jitter);
                pass.SetVector(command, "_AmbientLightColor", RenderSettings.ambientLight.linear);
                pass.SetVector(command, "_FogColor", RenderSettings.fogColor.linear);

                pass.SetFloat(command, "_FogStartDistance", RenderSettings.fogStartDistance);
                pass.SetFloat(command, "_FogEndDistance", RenderSettings.fogEndDistance);
                pass.SetFloat(command, "_FogDensity", RenderSettings.fogDensity);
                pass.SetFloat(command, "_FogMode", (float)RenderSettings.fogMode);
                pass.SetFloat(command, "_FogEnabled", RenderSettings.fog ? 1.0f : 0.0f);
                pass.SetFloat(command, "_AoEnabled", renderPipelineAsset.AmbientOcclusionSettings.Strength > 0.0f ? 1.0f : 0.0f);
                pass.SetFloat(command, "_Scale", dynamicResolution.ScaleFactor);

                pass.SetVector(command, "_WaterAlbedo", renderPipelineAsset.waterAlbedo.linear);
                pass.SetVector(command, "_WaterExtinction", renderPipelineAsset.waterExtinction);
               
                command.SetGlobalMatrix("_NonJitteredVPMatrix", camera.nonJitteredProjectionMatrix);
                command.SetGlobalMatrix("_PreviousVPMatrix", previousMatrix);
                command.SetGlobalMatrix("_InvVPMatrix", (GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix).inverse);
                pass.SetInt(command, "_FrameCount", Time.renderedFrameCount);

                context.SetupCameraProperties(camera);
            });
        }

        clusteredLightCulling.Render(camera, dynamicResolution.ScaleFactor);
        volumetricLighting.Render(camera, dynamicResolution.ScaleFactor);

        var cameraTarget = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.B10G11R11_UFloatPack32);
        var cameraDepth = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.D32_SFloat_S8_UInt);

        {
            var pass = renderGraph.AddRenderPass<GlobalRenderPass>();
            pass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Clear, RenderBufferStoreAction.Store);
            pass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Clear, RenderBufferStoreAction.Store);
        }

        opaqueObjectRenderer.Render(cullingResults, camera);
        var motionVectors = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.R16G16_SFloat);

        {
            var pass = renderGraph.AddRenderPass<GlobalRenderPass>();
            pass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.WriteTexture("", motionVectors, RenderBufferLoadAction.Clear, RenderBufferStoreAction.Store);
        }

        motionVectorsRenderer.Render(cullingResults, camera);
        cameraMotionVectors.Render(motionVectors, cameraDepth);
        ambientOcclusion.Render(camera, cameraDepth, cameraTarget, dynamicResolution.ScaleFactor);

        {
            var pass = renderGraph.AddRenderPass<GlobalRenderPass>();
            pass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 1.0f, RenderTargetFlags.ReadOnlyDepth);
            pass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);

            pass.SetRenderFunction((command, context) =>
            {
                command.DrawProcedural(Matrix4x4.identity, skyClearMaterial, 0, MeshTopology.Triangles, 3);
            });
        }

        skyRenderer.Render(cullingResults, camera);

        var sceneTextureId = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.B10G11R11_UFloatPack32);
        {
            var pass = renderGraph.AddRenderPass<GlobalRenderPass>();
            pass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 1.0f, RenderTargetFlags.ReadOnlyDepth);
            pass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);

            pass.SetRenderFunction((command, context) =>
            {
                // Copy scene texture
                command.CopyTexture(cameraTarget, sceneTextureId);
                pass.SetTexture(command, "_SceneTexture", sceneTextureId);
                pass.SetTexture(command, "_CameraDepth", cameraDepth);
            });
        }

        transparentObjectRenderer.Render(cullingResults, camera);
        autoExposure.Render(cameraTarget, scaledWidth, scaledHeight);

        var dofResult = depthOfField.Render(scaledWidth, scaledHeight, camera.fieldOfView, cameraTarget, cameraDepth);
        var taa = temporalAA.Render(camera, dofResult, motionVectors, dynamicResolution.ScaleFactor);
        var bloomResult = bloom.Render(camera, taa);

        tonemapping.Render(taa, bloomResult, camera.cameraType == CameraType.SceneView, camera.pixelWidth, camera.pixelHeight);

        {
            var pass = renderGraph.AddRenderPass<GlobalRenderPass>();
            pass.SetRenderFunction((command, context) =>
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
