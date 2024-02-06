using System;
using System.Collections.Generic;
using Arycama.CustomRenderPipeline;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using CommandBufferPool = Arycama.CustomRenderPipeline.CommandBufferPool;

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

        temporalAA.OnPreRender();

        var scaledWidth = (int)(camera.pixelWidth * dynamicResolution.ScaleFactor);
        var scaledHeight = (int)(camera.pixelHeight * dynamicResolution.ScaleFactor);

        var previousMatrix = camera.nonJitteredProjectionMatrix;

        camera.ResetProjectionMatrix();
        camera.nonJitteredProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix;

        var projectionMatrix = camera.projectionMatrix;
        projectionMatrix[0, 2] = 2.0f * temporalAA.Jitter.x / scaledWidth;
        projectionMatrix[1, 2] = 2.0f * temporalAA.Jitter.y / scaledHeight;
        camera.projectionMatrix = projectionMatrix;

        var worldToClip = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;
        var worldToView = camera.worldToCameraMatrix;
        var viewToWorld = camera.cameraToWorldMatrix;
        var clipToWorld = (GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix).inverse;

        if (!camera.TryGetCullingParameters(out var cullingParameters))
            return;

        renderGraph.SetScreenWidth(camera.pixelWidth);
        renderGraph.SetScreenHeight(camera.pixelHeight);

        BeginCameraRendering(context, camera);

        cullingParameters.shadowDistance = renderPipelineAsset.ShadowSettings.ShadowDistance;
        cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.DisablePerObjectCulling | CullingOptions.ShadowCasters;
        var cullingResults = context.Cull(ref cullingParameters);

        var lightingSetupResult = lightingSetup.Render(cullingResults, camera);

        // Camera setup
        var blueNoise1D = Resources.Load<Texture2D>(blueNoise1DIds.GetString(Time.renderedFrameCount % 64));
        var blueNoise2D = Resources.Load<Texture2D>(blueNoise2DIds.GetString(Time.renderedFrameCount % 64));
        var scaledResolution = new Vector4(scaledWidth, scaledHeight, 1.0f / scaledWidth, 1.0f / scaledHeight);
        var exposureBuffer = autoExposure.OnPreRender(camera);

        var objectPassData = new ObjectPassData
        {
            exposureBuffer = exposureBuffer,
            jitter = temporalAA.Jitter,
            ambientLightColor = RenderSettings.ambientLight.linear,
            scaledResolution = scaledResolution,
            blueNoise2D = blueNoise2D,
            mipBias = Mathf.Log(dynamicResolution.ScaleFactor, 2.0f),
            aoEnabled = renderPipelineAsset.AmbientOcclusionSettings.Strength > 0.0f ? 1.0f : 0.0f,
            time = Time.time,
            near = camera.nearClipPlane,
            far = camera.farClipPlane,
            worldToClip = worldToClip,
            worldToView = worldToView,
            viewToWorld = viewToWorld
        };

        var clusteredLightCullingResult = clusteredLightCulling.Render(scaledWidth, scaledHeight, camera.nearClipPlane, camera.farClipPlane, lightingSetupResult, clipToWorld);
        var volumetricLightingResult = volumetricLighting.Render(scaledWidth, scaledHeight, camera.farClipPlane, camera, clusteredLightCullingResult, lightingSetupResult, exposureBuffer, blueNoise1D, blueNoise2D, RenderSettings.ambientLight.linear, RenderSettings.fogColor.linear, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance, RenderSettings.fogDensity, RenderSettings.fog ? (float)RenderSettings.fogMode : 0.0f, previousMatrix, clipToWorld);

        // Opaque
        var cameraTarget = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.B10G11R11_UFloatPack32, isScreenTexture: true);
        var cameraDepth = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.D32_SFloat_S8_UInt, isScreenTexture: true);
        using (var pass = renderGraph.AddRenderPass<ObjectRenderPass>("Render Opaque"))
        {
            pass.Initialize("SRPDefaultUnlit", context, cullingResults, camera, RenderQueueRange.opaque, SortingCriteria.CommonOpaque, PerObjectData.None, true);
            pass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Clear, RenderBufferStoreAction.Store);
            pass.WriteTexture("", cameraTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

            volumetricLightingResult.SetInputs(pass);
            clusteredLightCullingResult.SetInputs(pass);
            lightingSetupResult.SetInputs(pass);

            var data = pass.SetRenderFunction<CommonPassData>((command, context, pass, data) =>
            {
                data.SetProperties(pass, command);
            });

            data.clusteredLightCullingResult = clusteredLightCullingResult;
            data.lightingSetupResult = lightingSetupResult;
            data.volumetricLightingResult = volumetricLightingResult;
            data.data = objectPassData;
        }

        // Motion Vectors
        var motionVectors = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.R16G16_SFloat, isScreenTexture: true);
        using (var pass = renderGraph.AddRenderPass<ObjectRenderPass>("Render Motion Vectors"))
        {
            pass.Initialize("MotionVectors", context, cullingResults, camera, RenderQueueRange.opaque, SortingCriteria.CommonOpaque, PerObjectData.MotionVectors);
            pass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            pass.WriteTexture("", motionVectors, RenderBufferLoadAction.Clear, RenderBufferStoreAction.Store, Color.clear);

            volumetricLightingResult.SetInputs(pass);
            clusteredLightCullingResult.SetInputs(pass);
            lightingSetupResult.SetInputs(pass);

            var data = pass.SetRenderFunction<MotionVectorsPassData>((command, context, pass, data) =>
            {
                data.objectPassData.SetProperties(pass, command);

                pass.SetMatrix(command, "_WorldToNonJitteredClip", data.nonJitteredVpMatrix);
                pass.SetMatrix(command, "_ClipToWorldPrevious", data.previousVpMatrix);
            });

            data.objectPassData.clusteredLightCullingResult = clusteredLightCullingResult;
            data.objectPassData.lightingSetupResult = lightingSetupResult;
            data.objectPassData.volumetricLightingResult = volumetricLightingResult;
            data.objectPassData.data = objectPassData;
            data.nonJitteredVpMatrix = camera.nonJitteredProjectionMatrix;
            data.previousVpMatrix = previousMatrix;
        }

        // Sky clear color
        using (var pass = renderGraph.AddRenderPass<FullscreenRenderPass>("Clear Color"))
        {
            pass.Material = skyClearMaterial;
            pass.Index = 0;
            pass.WriteDepth("", cameraDepth, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 1.0f, RenderTargetFlags.ReadOnlyDepth);
            pass.WriteTexture("", cameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);

            volumetricLightingResult.SetInputs(pass);

            var data = pass.SetRenderFunction<SkyPassData>((command, context, pass, data) =>
            {
                data.volumetricLightingResult.SetProperties(pass, command);
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

            volumetricLightingResult.SetInputs(pass);

            var data = pass.SetRenderFunction<SkyPassData>((command, context, pass, data) =>
            {
                data.volumetricLightingResult.SetProperties(pass, command);
                pass.SetConstantBuffer(command, "Exposure", data.exposureBuffer);
                pass.SetVector(command, "_ScaledResolution", data.scaledResolution);
            });

            data.volumetricLightingResult = volumetricLightingResult;
            data.exposureBuffer = exposureBuffer;
            data.scaledResolution = scaledResolution;
        }

        // Before transparent post processing
        cameraMotionVectors.Render(motionVectors, cameraDepth, scaledWidth, scaledHeight, camera.nonJitteredProjectionMatrix, previousMatrix, clipToWorld);
        ambientOcclusion.Render(camera, cameraDepth, cameraTarget, dynamicResolution.ScaleFactor, volumetricLightingResult, blueNoise2D, clipToWorld);

        // Copy scene texture
        var sceneTexture = renderGraph.GetTexture(scaledWidth, scaledHeight, GraphicsFormat.B10G11R11_UFloatPack32);
        using (var pass = renderGraph.AddRenderPass<GlobalRenderPass>("Copy Scene Texture"))
        {
            pass.ReadTexture("", cameraTarget);
            pass.WriteTexture("", sceneTexture);

            var data = pass.SetRenderFunction<Pass1Data>((command, context, pass, data) =>
            {
                command.CopyTexture(data.cameraTarget, 0, 0, 0, 0, data.cameraTarget.Width, data.cameraTarget.Height, data.sceneTexture, 0, 0, 0, 0);
            });

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

            volumetricLightingResult.SetInputs(pass);
            clusteredLightCullingResult.SetInputs(pass);
            lightingSetupResult.SetInputs(pass);

            var data = pass.SetRenderFunction<CommonPassData>((command, context, pass, data) =>
            {
                data.SetProperties(pass, command);
            });

            data.clusteredLightCullingResult = clusteredLightCullingResult;
            data.lightingSetupResult = lightingSetupResult;
            data.volumetricLightingResult = volumetricLightingResult;
            data.data = objectPassData;
        }

        // After transparent post processing
        autoExposure.Render(cameraTarget, scaledWidth, scaledHeight, camera);

        var dofResult = depthOfField.Render(scaledWidth, scaledHeight, camera.fieldOfView, cameraTarget, cameraDepth);
        var taa = temporalAA.Render(camera, dofResult, motionVectors, dynamicResolution.ScaleFactor);
        var bloomResult = bloom.Render(camera, taa);

        tonemapping.Render(taa, bloomResult, camera.cameraType == CameraType.SceneView, camera.pixelWidth, camera.pixelHeight);

        // Only in editor
        //using (var pass = renderGraph.AddRenderPass<GlobalRenderPass>("Render Gizmos"))
        //{
        //    var data = pass.SetRenderFunction<Pass2Data>((command, context, pass, data) =>
        //    {
        //        context.ExecuteCommandBuffer(command);
        //        command.Clear();

        //        if (UnityEditor.Handles.ShouldRenderGizmos())
        //        {
        //            context.SetupCameraProperties(camera);
        //            context.DrawGizmos(data.camera, GizmoSubset.PreImageEffects);
        //            context.DrawGizmos(data.camera, GizmoSubset.PostImageEffects);
        //        }
        //    });

        //    data.camera = camera;
        //}
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

    private class CommonPassData
    {
        public ClusteredLightCulling.Result clusteredLightCullingResult { private get; set; }
        public LightingSetup.Result lightingSetupResult { private get; set; }
        public VolumetricLighting.Result volumetricLightingResult { private get; set; }
        public ObjectPassData data;

        public void SetProperties(RenderPass pass, CommandBuffer command)
        {
            pass.SetConstantBuffer(command, "Exposure", data.exposureBuffer);

            pass.SetTexture(command, "_BlueNoise2D", data.blueNoise2D);

            pass.SetVector(command, "_Jitter", data.jitter);
            pass.SetVector(command, "_AmbientLightColor", data.ambientLightColor);
            pass.SetVector(command, "_ScaledResolution", data.scaledResolution);
            pass.SetFloat(command, "_AoEnabled", data.aoEnabled);
            pass.SetFloat(command, "_MipBias", data.mipBias);

            pass.SetFloat(command, "_Time", data.time);
            pass.SetFloat(command, "_Near", data.near);
            pass.SetFloat(command, "_Far", data.far);

            pass.SetMatrix(command, "_WorldToClip", data.worldToClip);
            pass.SetMatrix(command, "_WorldToView", data.worldToView);
            pass.SetMatrix(command, "_ViewToWorld", data.viewToWorld);

            clusteredLightCullingResult.SetProperties(pass, command);
            lightingSetupResult.SetProperties(pass, command);
            volumetricLightingResult.SetProperties(pass, command);
        }
    }

    private struct ObjectPassData
    {
        public BufferHandle exposureBuffer;
        public Vector2 jitter;
        public Color ambientLightColor;
        public float aoEnabled;
        public Vector4 scaledResolution;
        public Texture2D blueNoise2D;
        public float mipBias;
        internal float time;
        internal float near;
        internal float far;
        internal Matrix4x4 worldToClip;
        internal Matrix4x4 worldToView;
        internal Matrix4x4 viewToWorld;
    }

    private class MotionVectorsPassData
    {
        internal CommonPassData objectPassData = new();
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
