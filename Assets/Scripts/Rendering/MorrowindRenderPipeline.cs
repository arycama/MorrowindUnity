using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MorrowindRenderPipeline : RenderPipeline
{
    private MorrowindRenderPipelineAsset renderPipelineAsset;
    private CommandBuffer commandBuffer;
    private ComputeBuffer pointLightBuffer;

    public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset)
    {
        this.renderPipelineAsset = renderPipelineAsset;
        commandBuffer = new CommandBuffer() { name = "Render Camera" };
        pointLightBuffer = new ComputeBuffer(1, 32);
    }

    protected override void Dispose(bool disposing)
    {
        commandBuffer.Release();
        pointLightBuffer.Release();
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m)
    {
        if (SystemInfo.usesReversedZBuffer)
            m.SetRow(2, -m.GetRow(2));

        m.SetRow(0, 0.5f * (m.GetRow(0) + m.GetRow(3)));
        m.SetRow(1, 0.5f * (m.GetRow(1) + m.GetRow(3)));
        m.SetRow(2, 0.5f * (m.GetRow(2) + m.GetRow(3)));
        return m;
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (var camera in cameras)
        {
            BeginCameraRendering(context, camera);

            if (!camera.TryGetCullingParameters(out var cullingParameters))
                continue;

            cullingParameters.shadowDistance = renderPipelineAsset.ShadowDistance;
            cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.DisablePerObjectCulling | CullingOptions.ShadowCasters;
            var cullingResults = context.Cull(ref cullingParameters);

            commandBuffer.Clear();
            commandBuffer.BeginSample("Render Camera");

            // Setup ambient
            commandBuffer.SetGlobalColor("_Ambient", RenderSettings.ambientLight);
            commandBuffer.SetGlobalColor("_FogColor", RenderSettings.fogColor.linear);
            commandBuffer.SetGlobalFloat("_FogStartDistance", RenderSettings.fogStartDistance);
            commandBuffer.SetGlobalFloat("_FogEndDistance", RenderSettings.fogEndDistance);

            var fogEnabled = RenderSettings.fog;

#if UNITY_EDITOR
            if (UnityEditor.SceneView.currentDrawingSceneView != null)
                fogEnabled &= UnityEditor.SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

            commandBuffer.SetGlobalFloat("_FogEnabled", RenderSettings.fog ? 1.0f : 0.0f);
            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();

            var pointLightList = new List<PointLightData>();

            commandBuffer.SetGlobalVector("_SunDirection", Vector3.up);
            commandBuffer.SetGlobalColor("_SunColor", Color.black);

            // Setup lights
            for (var i = 0; i < cullingResults.visibleLights.Length; i++)
            {
                var visibleLight = cullingResults.visibleLights[i];
                if (visibleLight.lightType == LightType.Directional)
                {
                    var light = visibleLight.light;
                    commandBuffer.SetGlobalVector("_SunDirection", -light.transform.forward);
                    commandBuffer.SetGlobalColor("_SunColor", visibleLight.light.color.linear);
                    context.ExecuteCommandBuffer(commandBuffer);
                    commandBuffer.Clear();

                    var shadowResolution = renderPipelineAsset.ShadowResolution;
                    if (!cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(i, 0, 1, Vector3.zero, shadowResolution, light.shadowNearPlane, out var viewMatrix, out var projectionMatrix, out var shadowSplitData))
                        continue;

                    commandBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
                    commandBuffer.SetGlobalFloat("_ZClip", 0);
                    commandBuffer.SetGlobalDepthBias(renderPipelineAsset.ShadowBias, renderPipelineAsset.ShadowSlopeBias);

                    var directionalShadowsId = Shader.PropertyToID("_DirectionalShadows");
                    commandBuffer.GetTemporaryRT(directionalShadowsId, shadowResolution, shadowResolution, 16, FilterMode.Point, RenderTextureFormat.Shadowmap);
                    commandBuffer.SetRenderTarget(directionalShadowsId);
                    commandBuffer.ClearRenderTarget(true, false, Color.clear);
                    context.ExecuteCommandBuffer(commandBuffer);
                    commandBuffer.Clear();

                    var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, i) { splitData = shadowSplitData };
                    context.DrawShadows(ref shadowDrawingSettings);

                    commandBuffer.SetGlobalTexture("_DirectionalShadows", directionalShadowsId);

                    var worldToShadow = ConvertToAtlasMatrix(projectionMatrix * viewMatrix);
                    commandBuffer.SetGlobalMatrix("_WorldToShadow", worldToShadow);
                    commandBuffer.SetGlobalFloat("_ZClip", 1);
                    commandBuffer.SetGlobalDepthBias(0f, 0f);

                    context.ExecuteCommandBuffer(commandBuffer);
                    commandBuffer.Clear();
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

            commandBuffer.SetBufferData(pointLightBuffer, pointLightList);
            commandBuffer.SetGlobalBuffer("_PointLights", pointLightBuffer);
            commandBuffer.SetGlobalInt("_PointLightCount", pointLightList.Count);

            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();

            context.SetupCameraProperties(camera);

            commandBuffer.ClearRenderTarget(true, true, RenderSettings.fogColor.linear);
            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();

            var srpDefaultUnlitShaderPassName = new ShaderTagId("SRPDefaultUnlit");

            var opaqueSortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var opaqueDrawingSettings = new DrawingSettings(srpDefaultUnlitShaderPassName, opaqueSortingSettings) { enableInstancing = true };
            var opaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque) { };
            context.DrawRenderers(cullingResults, ref opaqueDrawingSettings, ref opaqueFilteringSettings);

            var transparentSortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent };
            var transparentDrawingSettings = new DrawingSettings(srpDefaultUnlitShaderPassName, transparentSortingSettings) { enableInstancing = true };

            var transparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent) { };
            context.DrawRenderers(cullingResults, ref transparentDrawingSettings, ref transparentFilteringSettings);

            if (UnityEditor.Handles.ShouldRenderGizmos())
            {
                context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
                context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
            }

            if (camera.cameraType == CameraType.SceneView)
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);

            commandBuffer.EndSample("Render Camera");
            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();

            context.Submit();
        }
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