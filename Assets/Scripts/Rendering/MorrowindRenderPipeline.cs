using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MorrowindRenderPipeline : RenderPipeline
{
    private static readonly IndexedString noiseIds = new("STBN/Scalar/stbn_scalar_2Dx1Dx1D_128x128x64x1_");

    private readonly MorrowindRenderPipelineAsset renderPipelineAsset;
    private readonly CommandBuffer command, envCommand;

    private readonly LightingSetup lightingSetup;
    private readonly VolumetricLighting volumetricLighting;
    private readonly EnvironmentSettings environmentSettings;
    private readonly ObjectRenderer opaqueObjectRenderer;
    private readonly ObjectRenderer transparentObjectRenderer;

    private Dictionary<Camera, int> cameraRenderedFrameCount = new();
    private Dictionary<Camera, Matrix4x4> previousViewProjectionMatrices = new();

    public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset)
    {
        this.renderPipelineAsset = renderPipelineAsset;
        command = new CommandBuffer() { name = "Test" };
        envCommand = new() { name = "Environment" };

        lightingSetup = new(renderPipelineAsset.ShadowSettings);
        environmentSettings = new();
        volumetricLighting = new();
        opaqueObjectRenderer = new(RenderQueueRange.opaque, SortingCriteria.CommonOpaque);
        transparentObjectRenderer = new(RenderQueueRange.transparent, SortingCriteria.CommonTransparent);
    }

    protected override void Dispose(bool disposing)
    {
        lightingSetup.Release();
        command.Release();
        envCommand.Release();
        volumetricLighting.Dispose();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        command.Clear();

        foreach (var camera in cameras)
        {
            RenderCamera(context, camera);
        }

        context.ExecuteCommandBuffer(command);
        command.Clear();

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

        lightingSetup.Render(command, cullingResults, context);
        context.ExecuteCommandBuffer(command);

        if(environmentSettings.NeedsRebuild())
            environmentSettings.Rebuild(envCommand);

        context.ExecuteCommandBuffer(envCommand);

        context.SetupCameraProperties(camera);

        // Use a seperate frame count per camera, which we manually track
        if(!cameraRenderedFrameCount.TryGetValue(camera, out var frameCount))
            cameraRenderedFrameCount.Add(camera, 0);
        else
        {
            // Only increase when frame debugger not enabled, or we get flickering
            if(!FrameDebugger.enabled)
                cameraRenderedFrameCount[camera] = ++frameCount;
        }

        var flip = camera.cameraType == CameraType.Game;
        var viewProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, flip) * camera.worldToCameraMatrix;

        if(!previousViewProjectionMatrices.TryGetValue(camera, out var previousViewProjectionMatrix))
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
        command.SetGlobalTexture("_BlueNoise1D", blueNoise1D);
        command.SetGlobalMatrix("_PreviousViewProjectionMatrix", previousViewProjectionMatrix);
        command.SetGlobalMatrix("_InvViewProjectionMatrix", viewProjectionMatrix.inverse);
        command.SetGlobalInt("_FrameCount", frameCount);
        context.ExecuteCommandBuffer(command);
        command.Clear();

        // Volumetric lighting
        volumetricLighting.Render(camera, command, renderPipelineAsset.TileSize, renderPipelineAsset.DepthSlices, frameCount, renderPipelineAsset.BlurSigma, renderPipelineAsset.NonLinearDepth);
        context.ExecuteCommandBuffer(command);
        command.Clear();

        command.ClearRenderTarget(true, true, RenderSettings.fogColor.linear);
        opaqueObjectRenderer.Render(ref cullingResults, camera, command, ref context);
        transparentObjectRenderer.Render(ref cullingResults, camera, command, ref context);
        volumetricLighting.CameraRenderComplete(command);
        context.ExecuteCommandBuffer(command);
        command.Clear();

        if (UnityEditor.Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }

        if (camera.cameraType == CameraType.SceneView)
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
    }
}
