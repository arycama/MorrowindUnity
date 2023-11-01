using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

public class MorrowindRenderPipeline : RenderPipeline
{
    private static readonly IndexedString blueNoise1DIds = new("STBN/Scalar/stbn_scalar_2Dx1Dx1D_128x128x64x1_");
    private static readonly IndexedString blueNoise2DIds = new("STBN/Vec2/stbn_vec2_2Dx1D_128x128x64_");
    private static readonly int cameraTargetId = Shader.PropertyToID("_CameraTarget");
    private static readonly int cameraDepthId = Shader.PropertyToID("_CameraDepth");
    private static readonly int depthTextureId = Shader.PropertyToID("_DepthTexture");
    private static readonly int sceneTextureId = Shader.PropertyToID("_SceneTexture");
    private static readonly int motionVectorsId = Shader.PropertyToID("_MotionVectors");

    private readonly MorrowindRenderPipelineAsset renderPipelineAsset;
    private readonly CommandBuffer shadowsCommand, renderCameraCommand, envCommand, volLightingCommand, opaqueCommand, transparentCommand;

    private readonly LightingSetup lightingSetup;
    private readonly ClusteredLightCulling clusteredLightCulling;
    private readonly VolumetricLighting volumetricLighting;
    private readonly EnvironmentSettings environmentSettings;
    private readonly ObjectRenderer opaqueObjectRenderer;
    private readonly ObjectRenderer transparentObjectRenderer;

    private Dictionary<Camera, int> cameraRenderedFrameCount = new();
    private Dictionary<Camera, (Matrix4x4, Matrix4x4)> previousViewProjectionMatrices = new();

    private Material motionVectorsMaterial;

    public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset)
    {
        this.renderPipelineAsset = renderPipelineAsset;
        renderCameraCommand = new CommandBuffer() { name = "Render Camera" };
        shadowsCommand = new() { name = "Render Shadows" };
        envCommand = new() { name = "Environment" };
        volLightingCommand = new() { name = "Volumetric Lighting" };
        opaqueCommand = new() { name = "Render Opaque" };
        transparentCommand = new() { name = "Render Transparent" };

        lightingSetup = new(renderPipelineAsset.ShadowSettings);
        clusteredLightCulling = new(renderPipelineAsset.ClusteredLightingSettings);
        environmentSettings = new();
        volumetricLighting = new();
        opaqueObjectRenderer = new(RenderQueueRange.opaque, SortingCriteria.CommonOpaque);
        transparentObjectRenderer = new(RenderQueueRange.transparent, SortingCriteria.CommonTransparent);

        motionVectorsMaterial = new Material(Shader.Find("Hidden/Camera Motion Vectors"));
    }

    protected override void Dispose(bool disposing)
    {
        lightingSetup.Release();
        clusteredLightCulling.Release();
        renderCameraCommand.Release();
        shadowsCommand.Release();
        envCommand.Release();
        volumetricLighting.Release();
        volLightingCommand.Release();
        opaqueCommand.Release();
        transparentCommand.Release();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        renderCameraCommand.Clear();

        foreach (var camera in cameras)
        {
            RenderCamera(context, camera);
        }

        context.ExecuteCommandBuffer(renderCameraCommand);
        renderCameraCommand.Clear();

        context.Submit();
    }

    private void RenderCamera(ScriptableRenderContext context, Camera camera)
    {
        // SetUp Post-processing
        PostProcessLayer postLayer = camera.GetComponent<PostProcessLayer>();
        bool hasPostProcessing = postLayer != null;
        bool usePostProcessing = false;
        bool hasOpaqueOnlyEffects = false;
        PostProcessRenderContext postContext = null;
        if (hasPostProcessing)
        {
            postContext = new PostProcessRenderContext();
            postContext.camera = camera;
            usePostProcessing = postLayer.enabled;
            hasOpaqueOnlyEffects = postLayer.HasOpaqueOnlyEffects(postContext);
        }

        camera.ResetProjectionMatrix();
        camera.nonJitteredProjectionMatrix = camera.projectionMatrix;

        if (postLayer.antialiasingMode == PostProcessLayer.Antialiasing.TemporalAntialiasing)
            postLayer.temporalAntialiasing.ConfigureJitteredProjectionMatrix(postContext);

        var nonJitteredViewProjectionMatrix = GL.GetGPUProjectionMatrix(camera.nonJitteredProjectionMatrix, false) * camera.worldToCameraMatrix;
        var flippedNonJitteredViewProjectionMatrix = GL.GetGPUProjectionMatrix(camera.nonJitteredProjectionMatrix, true) * camera.worldToCameraMatrix;
        if (!previousViewProjectionMatrices.TryGetValue(camera, out var previousMatrices))
        {
            previousMatrices = (nonJitteredViewProjectionMatrix, flippedNonJitteredViewProjectionMatrix);
            previousViewProjectionMatrices.Add(camera, previousMatrices);
        }
        else
        {
            previousViewProjectionMatrices[camera] = (nonJitteredViewProjectionMatrix, flippedNonJitteredViewProjectionMatrix);
        }

        var previousVpMatrix = previousMatrices.Item1;
        var previousVpMatrixFlipped = previousMatrices.Item2;

        BeginCameraRendering(context, camera);

        if (!camera.TryGetCullingParameters(out var cullingParameters))
            return;

        cullingParameters.shadowDistance = renderPipelineAsset.ShadowSettings.ShadowDistance;
        cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.DisablePerObjectCulling | CullingOptions.ShadowCasters;
        var cullingResults = context.Cull(ref cullingParameters);

        lightingSetup.Render(shadowsCommand, cullingResults, context, camera);
        context.ExecuteCommandBuffer(shadowsCommand);
        shadowsCommand.Clear();

        if (environmentSettings.NeedsRebuild())
            environmentSettings.Rebuild(envCommand);

        context.ExecuteCommandBuffer(envCommand);

        context.SetupCameraProperties(camera);

        // Use a seperate frame count per camera, which we manually track
        if (!cameraRenderedFrameCount.TryGetValue(camera, out var frameCount))
            cameraRenderedFrameCount.Add(camera, 0);
        else
        {
            // Only increase when frame debugger not enabled, or we get flickering
            if (!FrameDebugger.enabled)
                cameraRenderedFrameCount[camera] = ++frameCount;
        }

        // More camera setup
        var blueNoise1D = Resources.Load<Texture2D>(blueNoise1DIds.GetString(frameCount % 64));
        var blueNoise2D = Resources.Load<Texture2D>(blueNoise2DIds.GetString(frameCount % 64));
        renderCameraCommand.SetGlobalTexture("_BlueNoise1D", blueNoise1D);
        renderCameraCommand.SetGlobalTexture("_BlueNoise2D", blueNoise2D);
        renderCameraCommand.SetGlobalMatrix("_NonJitteredVP", nonJitteredViewProjectionMatrix);
        renderCameraCommand.SetGlobalMatrix("_NonJitteredVPFlipped", flippedNonJitteredViewProjectionMatrix);
        renderCameraCommand.SetGlobalMatrix("_PreviousViewProjectionMatrix", previousVpMatrix);
        renderCameraCommand.SetGlobalMatrix("_PreviousViewProjectionMatrixFlipped", previousVpMatrixFlipped);
        renderCameraCommand.SetGlobalMatrix("_InvViewProjectionMatrix", (GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix).inverse);
        renderCameraCommand.SetGlobalMatrix("_InvViewProjectionMatrixFlipped", (GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix).inverse);

        renderCameraCommand.SetGlobalInt("_FrameCount", frameCount);

        // Clustered light culling
        renderCameraCommand.BeginSample("Clustered Light Culling");
        clusteredLightCulling.Render(renderCameraCommand, camera);
        renderCameraCommand.EndSample("Clustered Light Culling");

        context.ExecuteCommandBuffer(renderCameraCommand);
        renderCameraCommand.Clear();

        // Volumetric lighting
        volumetricLighting.Render(camera, volLightingCommand, renderPipelineAsset.TileSize, renderPipelineAsset.DepthSlices, frameCount, renderPipelineAsset.BlurSigma, renderPipelineAsset.NonLinearDepth);
        context.ExecuteCommandBuffer(volLightingCommand);
        volLightingCommand.Clear();

        renderCameraCommand.GetTemporaryRT(cameraTargetId, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RGB111110Float);
        renderCameraCommand.GetTemporaryRT(cameraDepthId, camera.pixelWidth, camera.pixelHeight, 24, FilterMode.Point, RenderTextureFormat.Depth);
        renderCameraCommand.SetRenderTarget(cameraTargetId, new RenderTargetIdentifier(cameraDepthId));
        renderCameraCommand.ClearRenderTarget(true, true, RenderSettings.fogColor.linear);

        renderCameraCommand.BeginSample("Render Opaque");
        opaqueObjectRenderer.Render(ref cullingResults, camera, renderCameraCommand, ref context);
        renderCameraCommand.EndSample("Render Opaque");

        // Copy depth/scene textures
        renderCameraCommand.GetTemporaryRT(sceneTextureId, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RGB111110Float);
        renderCameraCommand.CopyTexture(cameraTargetId, sceneTextureId);
        renderCameraCommand.SetGlobalTexture(sceneTextureId, sceneTextureId);

        renderCameraCommand.GetTemporaryRT(depthTextureId, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Point, RenderTextureFormat.Depth);
        renderCameraCommand.CopyTexture(cameraDepthId, depthTextureId);
        renderCameraCommand.SetGlobalTexture(depthTextureId, depthTextureId);

        renderCameraCommand.SetGlobalTexture("_CameraDepthTexture", depthTextureId);

        renderCameraCommand.GetTemporaryRT(motionVectorsId, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.RGHalf);
        renderCameraCommand.SetRenderTarget(motionVectorsId);
        renderCameraCommand.DrawProcedural(Matrix4x4.identity, motionVectorsMaterial, 0, MeshTopology.Triangles, 3);

        renderCameraCommand.SetGlobalTexture("_CameraMotionVectorsTexture", motionVectorsId);

        context.ExecuteCommandBuffer(renderCameraCommand);
        renderCameraCommand.Clear();

        // Opaque Post-processing
        // Ambient Occlusion, Screen-spaced reflection are generally not supported for SRP
        // So this part is only for custom opaque post-processing
        if (usePostProcessing)
        {
            CommandBuffer cmdpp = new CommandBuffer();
            cmdpp.name = "(" + camera.name + ")" + "Post-processing Opaque";

            postContext.Reset();
            postContext.camera = camera;
            postContext.source = cameraTargetId;
            postContext.sourceFormat = RenderTextureFormat.RGB111110Float;
            postContext.destination = cameraTargetId;
            postContext.command = cmdpp;
            postContext.flip = camera.targetTexture == null;
            postLayer.RenderOpaqueOnly(postContext);

            context.ExecuteCommandBuffer(cmdpp);
            cmdpp.Release();
        }

        renderCameraCommand.SetRenderTarget(cameraTargetId, new RenderTargetIdentifier(cameraDepthId));

        renderCameraCommand.BeginSample("Render Transparent");
        transparentObjectRenderer.Render(ref cullingResults, camera, renderCameraCommand, ref context);
        renderCameraCommand.EndSample("Render Transparent");

        context.ExecuteCommandBuffer(renderCameraCommand);
        renderCameraCommand.Clear();

        //************************** Transparent Post-processing ************************************
        //Bloom, Vignette, Grain, ColorGrading, LensDistortion, Chromatic Aberration, Auto Exposure
        if (usePostProcessing)
        {
            CommandBuffer cmdpp = new CommandBuffer();
            cmdpp.name = "(" + camera.name + ")" + "Post-processing Transparent";

            cmdpp.SetGlobalTexture("_CameraDepthTexture", cameraDepthId);

            postContext.Reset();
            postContext.camera = camera;
            postContext.source = cameraTargetId;
            postContext.sourceFormat = RenderTextureFormat.RGB111110Float;
            postContext.destination = BuiltinRenderTextureType.CameraTarget;
            postContext.command = cmdpp;
            postContext.flip = camera.targetTexture == null;

            var isSceneCamera = camera.cameraType == CameraType.SceneView;
            if (isSceneCamera)
                camera.cameraType = CameraType.Game;

            postLayer.Render(postContext);

            if (isSceneCamera)
                camera.cameraType = CameraType.SceneView;

            context.ExecuteCommandBuffer(cmdpp);
            cmdpp.Release();
        }
        else
        {
            // Copy final result
            renderCameraCommand.Blit(cameraTargetId, BuiltinRenderTextureType.CameraTarget);
        }

        renderCameraCommand.ReleaseTemporaryRT(sceneTextureId);
        renderCameraCommand.ReleaseTemporaryRT(depthTextureId);
       
        renderCameraCommand.ReleaseTemporaryRT(cameraTargetId);
        renderCameraCommand.ReleaseTemporaryRT(cameraDepthId);

        // Should release these sooner.. ideally track where they are used and release once done
        clusteredLightCulling.CameraRenderingComplete(renderCameraCommand);
        volumetricLighting.CameraRenderComplete(renderCameraCommand);
        context.ExecuteCommandBuffer(renderCameraCommand);
        renderCameraCommand.Clear();

        if (UnityEditor.Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }

        if (camera.cameraType == CameraType.SceneView)
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
    }
}
