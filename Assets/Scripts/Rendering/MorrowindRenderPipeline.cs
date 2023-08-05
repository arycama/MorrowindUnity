using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class MorrowindRenderPipeline : RenderPipeline
{
    private readonly MorrowindRenderPipelineAsset renderPipelineAsset;
    private readonly CommandBuffer commandBuffer;
    private ComputeBuffer pointLightBuffer; // Can't be readonly as we resize if needed.

    private readonly ShadowRenderer shadowRenderer;
    private readonly ObjectRenderer opaqueObjectRenderer;
    private readonly ObjectRenderer transparentObjectRenderer;

    private readonly List<PointLightData> pointLightList;

    public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset)
    {
        this.renderPipelineAsset = renderPipelineAsset;
        commandBuffer = new CommandBuffer() { name = "Render Camera" };
        pointLightBuffer = new ComputeBuffer(1, 32);

        pointLightList = new();

        shadowRenderer = new();
        opaqueObjectRenderer = new ObjectRenderer(RenderQueueRange.opaque, SortingCriteria.CommonOpaque);
        transparentObjectRenderer = new ObjectRenderer(RenderQueueRange.transparent, SortingCriteria.CommonTransparent);
    }

    protected override void Dispose(bool disposing)
    {
        commandBuffer.Release();
        pointLightBuffer.Release();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (var camera in cameras)
            RenderCamera(context, camera);

        context.Submit();
    }

    private void RenderCamera(ScriptableRenderContext context, Camera camera)
    {
        BeginCameraRendering(context, camera);

        if (!camera.TryGetCullingParameters(out var cullingParameters))
            return;

        cullingParameters.shadowDistance = renderPipelineAsset.ShadowDistance;
        cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.DisablePerObjectCulling | CullingOptions.ShadowCasters;
        var cullingResults = context.Cull(ref cullingParameters);

        commandBuffer.Clear();
        pointLightList.Clear();

        commandBuffer.SetGlobalVector("_SunDirection", Vector3.up);
        commandBuffer.SetGlobalVector("_SunColor", Color.black);
        commandBuffer.SetGlobalFloat("_SunShadowsOn", 0.0f);

        // Setup lights/shadows
        for (var i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            var visibleLight = cullingResults.visibleLights[i];
            if (visibleLight.lightType == LightType.Directional)
            {
                var light = visibleLight.light;
                commandBuffer.SetGlobalVector("_SunDirection", -light.transform.forward);
                commandBuffer.SetGlobalVector("_SunColor", visibleLight.light.color.linear);
                commandBuffer.SetGlobalFloat("_SunShadowsOn", light.shadows == LightShadows.None ? 0.0f : 1.0f);
                context.ExecuteCommandBuffer(commandBuffer);
                commandBuffer.Clear();

                if(light.shadows != LightShadows.None)
                {
                    shadowRenderer.Render(commandBuffer, ref context, renderPipelineAsset.ShadowResolution, ref cullingResults, i, light, renderPipelineAsset.ShadowBias, renderPipelineAsset.ShadowSlopeBias);
                    context.ExecuteCommandBuffer(commandBuffer);
                    commandBuffer.Clear();
                }
            }
            else if (visibleLight.lightType == LightType.Point)
            {
                pointLightList.Add(new PointLightData(visibleLight.localToWorldMatrix.GetPosition(), visibleLight.range, (Vector4)visibleLight.light.color.linear, uint.MaxValue));
            }
        }

        if (pointLightList.Count >= pointLightBuffer.count)
        {
            pointLightBuffer.Release();
            pointLightBuffer = new ComputeBuffer(pointLightList.Count, 32);
        }

        // Pre-object render setup
        commandBuffer.SetBufferData(pointLightBuffer, pointLightList);
        commandBuffer.SetGlobalBuffer("_PointLights", pointLightBuffer);
        commandBuffer.SetGlobalInt("_PointLightCount", pointLightList.Count);

        // Setup ambient
        commandBuffer.SetGlobalVector("_AmbientLightColor", RenderSettings.ambientLight);
        commandBuffer.SetGlobalVector("_FogColor", RenderSettings.fogColor.linear);
        commandBuffer.SetGlobalFloat("_FogStartDistance", RenderSettings.fogStartDistance);
        commandBuffer.SetGlobalFloat("_FogEndDistance", RenderSettings.fogEndDistance);

        var fogEnabled = RenderSettings.fog;

#if UNITY_EDITOR
        if (UnityEditor.SceneView.currentDrawingSceneView != null)
            fogEnabled &= UnityEditor.SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

        commandBuffer.SetGlobalFloat("_FogEnabled", RenderSettings.fog ? 1.0f : 0.0f);

        context.SetupCameraProperties(camera);

        commandBuffer.ClearRenderTarget(true, true, RenderSettings.fogColor.linear);
        opaqueObjectRenderer.Render(ref cullingResults, camera, commandBuffer, ref context);
        transparentObjectRenderer.Render(ref cullingResults, camera, commandBuffer, ref context);
        context.ExecuteCommandBuffer(commandBuffer);

        if (UnityEditor.Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }

        if (camera.cameraType == CameraType.SceneView)
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
    }
}

public class ShadowRenderer
{
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m)
    {
        if (SystemInfo.usesReversedZBuffer)
            m.SetRow(2, -m.GetRow(2));

        m.SetRow(0, 0.5f * (m.GetRow(0) + m.GetRow(3)));
        m.SetRow(1, 0.5f * (m.GetRow(1) + m.GetRow(3)));
        m.SetRow(2, 0.5f * (m.GetRow(2) + m.GetRow(3)));
        return m;
    }

    public void Render(CommandBuffer commandBuffer, ref ScriptableRenderContext context, int resolution, ref CullingResults cullingResults, int index, Light light, float shadowBias, float shadowSlopeBias)
    {
        if (!cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(index, 0, 1, Vector3.zero, resolution, light.shadowNearPlane, out var viewMatrix, out var projectionMatrix, out var shadowSplitData))
            return;

        commandBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        commandBuffer.SetGlobalFloat("_ZClip", 0);
        commandBuffer.SetGlobalDepthBias(shadowBias, shadowSlopeBias);

        var directionalShadowsId = Shader.PropertyToID("_DirectionalShadows");
        commandBuffer.GetTemporaryRT(directionalShadowsId, resolution, resolution, 16, FilterMode.Point, RenderTextureFormat.Shadowmap);
        commandBuffer.SetRenderTarget(directionalShadowsId);
        commandBuffer.ClearRenderTarget(true, false, Color.clear);
        context.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();

        var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, index) { splitData = shadowSplitData };
        context.DrawShadows(ref shadowDrawingSettings);

        commandBuffer.SetGlobalTexture("_DirectionalShadows", directionalShadowsId);

        var worldToShadow = ConvertToAtlasMatrix(projectionMatrix * viewMatrix);
        commandBuffer.SetGlobalMatrix("_WorldToShadow", worldToShadow);
        commandBuffer.SetGlobalFloat("_ZClip", 1);
        commandBuffer.SetGlobalDepthBias(0f, 0f);
    }
}

public class ObjectRenderer
{
    private RenderQueueRange renderQueueRange;
    private SortingCriteria sortingCriteria;

    public ObjectRenderer(RenderQueueRange renderQueueRange, SortingCriteria sortingCriteria)
    {
        this.renderQueueRange = renderQueueRange;
        this.sortingCriteria = sortingCriteria;
    }

    public void Render(ref CullingResults cullingResults, Camera camera, CommandBuffer commandBuffer, ref ScriptableRenderContext context)
    {
        var srpDefaultUnlitShaderPassName = new ShaderTagId("SRPDefaultUnlit");
        var rendererListDesc = new RendererListDesc(srpDefaultUnlitShaderPassName, cullingResults, camera)
        {
            renderQueueRange = renderQueueRange,
            sortingCriteria = sortingCriteria
        };

        var opaqueRendererList = context.CreateRendererList(rendererListDesc);
        commandBuffer.DrawRendererList(opaqueRendererList);
    }
}

public readonly struct PointLightData
{
    public Vector3 Position { get; }
    public float Range { get; }
    public Vector3 Color { get; }
    public uint ShadowIndex { get; }

    public PointLightData(Vector3 position, float range, Vector3 color, uint shadowIndex)
    {
        Position = position;
        Range = range;
        Color = color;
        ShadowIndex = shadowIndex;
    }
}