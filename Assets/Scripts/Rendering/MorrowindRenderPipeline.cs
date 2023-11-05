using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

public class MorrowindRenderPipeline : RenderPipeline
{
    private static readonly IndexedString blueNoise1DIds = new("STBN/Scalar/stbn_scalar_2Dx1Dx1D_128x128x64x1_");
    private static readonly IndexedString blueNoise2DIds = new("STBN/Vec2/stbn_vec2_2Dx1D_128x128x64_");
    private static readonly int cameraTargetId = Shader.PropertyToID("_CameraTarget");
    private static readonly int cameraDepthId = Shader.PropertyToID("_CameraDepth");
    private static readonly int sceneTextureId = Shader.PropertyToID("_SceneTexture");
    private static readonly int motionVectorsId = Shader.PropertyToID("_MotionVectors");

    private readonly MorrowindRenderPipelineAsset renderPipelineAsset;

    private readonly LightingSetup lightingSetup;
    private readonly ClusteredLightCulling clusteredLightCulling;
    private readonly VolumetricLighting volumetricLighting;
    private readonly ObjectRenderer opaqueObjectRenderer;
    private readonly ObjectRenderer motionVectorsRenderer;
    private readonly ObjectRenderer transparentObjectRenderer;
    private readonly TemporalAA temporalAA;

    private Dictionary<Camera, int> cameraRenderedFrameCount = new();
    private Dictionary<Camera, Matrix4x4> previousMatrices = new();

    private Material motionVectorsMaterial;

    public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset)
    {
        this.renderPipelineAsset = renderPipelineAsset;

        GraphicsSettings.useScriptableRenderPipelineBatching = renderPipelineAsset.EnableSrpBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;
        GraphicsSettings.lightsUseColorTemperature = true;
        GraphicsSettings.disableBuiltinCustomRenderTextureUpdate = true;
        GraphicsSettings.realtimeDirectRectangularAreaLights = true;

        lightingSetup = new(renderPipelineAsset.ShadowSettings);
        clusteredLightCulling = new(renderPipelineAsset.ClusteredLightingSettings);
        volumetricLighting = new();
        opaqueObjectRenderer = new(RenderQueueRange.opaque, SortingCriteria.CommonOpaque, true, PerObjectData.None, "SRPDefaultUnlit");
        motionVectorsRenderer = new(RenderQueueRange.opaque, SortingCriteria.CommonOpaque, false, PerObjectData.MotionVectors, "MotionVectors");
        transparentObjectRenderer = new(RenderQueueRange.transparent, SortingCriteria.CommonTransparent, false, PerObjectData.None, "SRPDefaultUnlit");
        temporalAA = new(renderPipelineAsset.TemporalAASettings);

        motionVectorsMaterial = new Material(Shader.Find("Hidden/Camera Motion Vectors"));

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
    }

    protected override void Dispose(bool disposing)
    {
        lightingSetup.Release();
        clusteredLightCulling.Release();
        volumetricLighting.Release();
        temporalAA.Release();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        var command = GenericPool<CommandBuffer>.Get();
        command.name = "Render Camera";
        command.Clear();

        foreach (var camera in cameras)
        {
            RenderCamera(context, camera, command);
        }

        context.ExecuteCommandBuffer(command);
        GenericPool<CommandBuffer>.Release(command);

        context.Submit();
    }

    private void RenderCamera(ScriptableRenderContext context, Camera camera, CommandBuffer command)
    {
        camera.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;

        // Use a seperate frame count per camera, which we manually track
        if (!cameraRenderedFrameCount.TryGetValue(camera, out var frameCount))
            cameraRenderedFrameCount.Add(camera, 0);
        else
        {
            // Only increase when frame debugger not enabled, or we get flickering
            if (!FrameDebugger.enabled)
                cameraRenderedFrameCount[camera] = ++frameCount;
        }

        camera.ResetProjectionMatrix();
        temporalAA.OnPreRender(camera, frameCount);

        if (!previousMatrices.TryGetValue(camera, out var previousMatrix))
        {
            previousMatrix = camera.nonJitteredProjectionMatrix;
            previousMatrices.Add(camera, previousMatrix);
        }
        else
        {
            previousMatrices[camera] = camera.nonJitteredProjectionMatrix;
        }

        if (!camera.TryGetCullingParameters(out var cullingParameters))
            return;

        BeginCameraRendering(context, camera);

        cullingParameters.shadowDistance = renderPipelineAsset.ShadowSettings.ShadowDistance;
        cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.DisablePerObjectCulling | CullingOptions.ShadowCasters;
        var cullingResults = context.Cull(ref cullingParameters);

        lightingSetup.Render(command, cullingResults, context, camera);

        command.SetGlobalVector("_AmbientLightColor", RenderSettings.ambientLight.linear);
        command.SetGlobalVector("_FogColor", RenderSettings.fogColor.linear);
        command.SetGlobalFloat("_FogStartDistance", RenderSettings.fogStartDistance);
        command.SetGlobalFloat("_FogEndDistance", RenderSettings.fogEndDistance);
        command.SetGlobalFloat("_FogEnabled", RenderSettings.fog ? 1.0f : 0.0f);

        context.SetupCameraProperties(camera);

        // More camera setup
        var blueNoise1D = Resources.Load<Texture2D>(blueNoise1DIds.GetString(frameCount % 64));
        var blueNoise2D = Resources.Load<Texture2D>(blueNoise2DIds.GetString(frameCount % 64));
        command.SetGlobalTexture("_BlueNoise1D", blueNoise1D);
        command.SetGlobalTexture("_BlueNoise2D", blueNoise2D);
        command.SetGlobalMatrix("_NonJitteredVPMatrix", camera.nonJitteredProjectionMatrix);
        command.SetGlobalMatrix("_PreviousVPMatrix", previousMatrix);
        command.SetGlobalMatrix("_InvVPMatrix", (GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix).inverse);

        command.SetGlobalInt("_FrameCount", frameCount);

        clusteredLightCulling.Render(command, camera);
        volumetricLighting.Render(camera, command, renderPipelineAsset.TileSize, renderPipelineAsset.DepthSlices, frameCount, renderPipelineAsset.BlurSigma, renderPipelineAsset.NonLinearDepth);

        command.GetTemporaryRT(cameraDepthId, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Point, RenderTextureFormat.Depth);
        command.GetTemporaryRT(cameraTargetId, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RGB111110Float);
        command.GetTemporaryRT(motionVectorsId, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.RGHalf);
        context.ExecuteCommandBuffer(command);
        command.Clear();

        var attachmentDescriptors = new NativeArray<AttachmentDescriptor>(3, Allocator.Temp);
        attachmentDescriptors[0] = new AttachmentDescriptor(RenderTextureFormat.Depth) { loadAction = RenderBufferLoadAction.Clear, storeAction = RenderBufferStoreAction.Store, loadStoreTarget = cameraDepthId }; 
        attachmentDescriptors[1] = new AttachmentDescriptor(RenderTextureFormat.RGB111110Float) { clearColor = RenderSettings.fogColor.linear, loadAction = RenderBufferLoadAction.Clear, storeAction = RenderBufferStoreAction.Store, loadStoreTarget = cameraTargetId };
        attachmentDescriptors[2] = new AttachmentDescriptor(RenderTextureFormat.RGHalf) { clearColor = Color.clear, loadAction = RenderBufferLoadAction.Clear, storeAction = RenderBufferStoreAction.Store, loadStoreTarget = motionVectorsId };

        context.BeginRenderPass(camera.pixelWidth, camera.pixelHeight, 1, attachmentDescriptors, 0);

        // Base pass
        var opauqePassColors = new NativeArray<int>(1, Allocator.Temp);
        opauqePassColors[0] = 1;

        context.BeginSubPass(opauqePassColors);
        opaqueObjectRenderer.Render(ref cullingResults, camera, command, ref context);
        context.ExecuteCommandBuffer(command);
        command.Clear();
        context.EndSubPass();

        // Motion Vectors
        var motionVectorsPassColors = new NativeArray<int>(2, Allocator.Temp);
        motionVectorsPassColors[0] = 1;
        motionVectorsPassColors[1] = 2;

        context.BeginSubPass(motionVectorsPassColors);
        motionVectorsRenderer.Render(ref cullingResults, camera, command, ref context);
        context.ExecuteCommandBuffer(command);
        command.Clear();
        context.EndSubPass();

        // Camera motion Vectors
        var cameraMotionVectorPassColors = new NativeArray<int>(1, Allocator.Temp);
        cameraMotionVectorPassColors[0] = 2;

        var cameraMotionVectorPassInputs = new NativeArray<int>(1, Allocator.Temp);
        cameraMotionVectorPassInputs[0] = 0;

        context.BeginSubPass(cameraMotionVectorPassColors, cameraMotionVectorPassInputs, true);
        command.DrawProcedural(Matrix4x4.identity, motionVectorsMaterial, 0, MeshTopology.Triangles, 3);
        context.ExecuteCommandBuffer(command);
        command.Clear();
        context.EndSubPass();

        // Copy scene texture
        command.GetTemporaryRT(sceneTextureId, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RGB111110Float);
        command.CopyTexture(cameraTargetId, sceneTextureId);
        command.SetGlobalTexture(sceneTextureId, sceneTextureId);

        var transparentPassColors = new NativeArray<int>(1, Allocator.Temp);
        transparentPassColors[0] = 1;

        var transparentPassInputs = new NativeArray<int>(1, Allocator.Temp);
        transparentPassInputs[0] = 0;

        context.BeginSubPass(transparentPassColors, transparentPassInputs, true);
        transparentObjectRenderer.Render(ref cullingResults, camera, command, ref context);
        context.EndSubPass();

        context.EndRenderPass();

        var final = temporalAA.Render(camera, command, frameCount, cameraTargetId, motionVectorsId, cameraDepthId);

        command.ReleaseTemporaryRT(sceneTextureId);
        command.ReleaseTemporaryRT(cameraTargetId);
        command.ReleaseTemporaryRT(cameraDepthId);

        // Copy final result
        command.Blit(final, BuiltinRenderTextureType.CameraTarget);

        // Should release these sooner.. ideally track where they are used and release once done
        clusteredLightCulling.CameraRenderingComplete(command);
        volumetricLighting.CameraRenderComplete(command);

        if (UnityEditor.Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }

        if (camera.cameraType == CameraType.SceneView)
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
    }
}
