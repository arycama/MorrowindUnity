﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MorrowindRenderPipeline : RenderPipeline
{
    private static readonly IndexedString noiseIds = new("STBN/Scalar/stbn_scalar_2Dx1Dx1D_128x128x64x1_");
    private static readonly int cameraTargetId = Shader.PropertyToID("_CameraTarget");
    private static readonly int cameraDepthId = Shader.PropertyToID("_CameraDepth");
    private static readonly int depthTextureId = Shader.PropertyToID("_DepthTexture");
    private static readonly int sceneTextureId = Shader.PropertyToID("_SceneTexture");

    private readonly MorrowindRenderPipelineAsset renderPipelineAsset;
    private readonly CommandBuffer shadowsCommand, renderCameraCommand, envCommand, volLightingCommand, opaqueCommand, transparentCommand;

    private readonly LightingSetup lightingSetup;
    private readonly ClusteredLightCulling clusteredLightCulling;
    private readonly VolumetricLighting volumetricLighting;
    private readonly EnvironmentSettings environmentSettings;
    private readonly ObjectRenderer opaqueObjectRenderer;
    private readonly ObjectRenderer transparentObjectRenderer;

    private Dictionary<Camera, int> cameraRenderedFrameCount = new();
    private Dictionary<Camera, Matrix4x4> previousViewProjectionMatrices = new();

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
        BeginCameraRendering(context, camera);

        if (!camera.TryGetCullingParameters(out var cullingParameters))
            return;

        cullingParameters.shadowDistance = renderPipelineAsset.ShadowSettings.ShadowDistance;
        cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.DisablePerObjectCulling | CullingOptions.ShadowCasters;
        var cullingResults = context.Cull(ref cullingParameters);

        lightingSetup.Render(shadowsCommand, cullingResults, context);
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

        var viewProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;

        if (!previousViewProjectionMatrices.TryGetValue(camera, out var previousViewProjectionMatrix))
        {
            previousViewProjectionMatrix = viewProjectionMatrix;
            previousViewProjectionMatrices.Add(camera, previousViewProjectionMatrix);
        }
        else
        {
            previousViewProjectionMatrices[camera] = viewProjectionMatrix;
        }

        // More camera setup
        var blueNoise1D = Resources.Load<Texture2D>(noiseIds.GetString(frameCount % 64));
        //blueNoise1D = Resources.Load<Texture2D>(noiseIds.GetString(0));
        renderCameraCommand.SetGlobalTexture("_BlueNoise1D", blueNoise1D);
        renderCameraCommand.SetGlobalMatrix("_PreviousViewProjectionMatrix", previousViewProjectionMatrix);
        renderCameraCommand.SetGlobalMatrix("_InvViewProjectionMatrix", viewProjectionMatrix.inverse);
        renderCameraCommand.SetGlobalInt("_FrameCount", frameCount);

        // Clustered light culling
        clusteredLightCulling.Render(renderCameraCommand, camera);

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

        renderCameraCommand.GetTemporaryRT(depthTextureId, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Point, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
        renderCameraCommand.CopyTexture(cameraDepthId, depthTextureId);
        renderCameraCommand.SetGlobalTexture(depthTextureId, depthTextureId);

        renderCameraCommand.BeginSample("Render Transparent");
        transparentObjectRenderer.Render(ref cullingResults, camera, renderCameraCommand, ref context);
        renderCameraCommand.EndSample("Render Transparent");

        renderCameraCommand.ReleaseTemporaryRT(sceneTextureId);
        renderCameraCommand.ReleaseTemporaryRT(depthTextureId);

        // Copy final result
        renderCameraCommand.Blit(cameraTargetId, BuiltinRenderTextureType.CameraTarget);
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

public static class GraphicsUtilities
{
    public static void SafeExpand(ref ComputeBuffer computeBuffer, int size = 1, int stride = sizeof(int), ComputeBufferType type = ComputeBufferType.Default)
    {
        size = Mathf.Max(size, 1);

        if (computeBuffer == null || computeBuffer.count < size)
        {
            if (computeBuffer != null)
                computeBuffer.Release();

            computeBuffer = new ComputeBuffer(size, stride, type);
        }
    }
}