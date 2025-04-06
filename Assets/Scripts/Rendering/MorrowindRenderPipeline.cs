using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MorrowindRenderPipeline : RenderPipeline
{
    private MorrowindRenderPipelineAsset renderPipelineAsset;
    private CommandBuffer command;

    public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset)
    {
        this.renderPipelineAsset = renderPipelineAsset;
        command = new CommandBuffer() { name = "Render Camera" };
    }

    protected override void Dispose(bool disposing)
    {
        command.Release();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras) => Render(context, cameras);

    protected override void Render(ScriptableRenderContext context, List<Camera> cameras) => Render(context, cameras);

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m)
    {
        if (SystemInfo.usesReversedZBuffer)
            m.SetRow(2, -m.GetRow(2));

        m.SetRow(0, 0.5f * (m.GetRow(0) + m.GetRow(3)));
        m.SetRow(1, 0.5f * (m.GetRow(1) + m.GetRow(3)));
        m.SetRow(2, 0.5f * (m.GetRow(2) + m.GetRow(3)));
        return m;
    }

    protected void Render(ScriptableRenderContext context, IList<Camera> cameras)
    {
        GraphicsSettings.useScriptableRenderPipelineBatching = renderPipelineAsset.UseSrpBatching;

        foreach (var camera in cameras)
        {
            BeginCameraRendering(context, camera);

            if (!camera.TryGetCullingParameters(out var cullingParameters))
                continue;

            cullingParameters.shadowDistance = renderPipelineAsset.ShadowDistance;
            cullingParameters.cullingOptions = CullingOptions.NeedsLighting | CullingOptions.DisablePerObjectCulling | CullingOptions.ShadowCasters;
            var cullingResults = context.Cull(ref cullingParameters);

            // Setup lights
            for (var i = 0; i < cullingResults.visibleLights.Length; i++)
            {
                var visibleLight = cullingResults.visibleLights[i];
                if (visibleLight.lightType != LightType.Directional)
                    continue;

                var shadowResolution = renderPipelineAsset.ShadowResolution;
                if (!cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(i, 0, 1, Vector3.zero, shadowResolution, visibleLight.light.shadowNearPlane, out var viewMatrix, out var projectionMatrix, out var shadowSplitData))
                    continue;

                command.SetGlobalVector("_SunDirection", -visibleLight.light.transform.forward);
                command.SetGlobalColor("_SunColor", visibleLight.finalColor.linear);

                command.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
                command.SetGlobalFloat("_ZClip", 0);
                command.SetGlobalDepthBias(renderPipelineAsset.ShadowBias, renderPipelineAsset.ShadowSlopeBias);

                var directionalShadowsId = Shader.PropertyToID("_DirectionalShadows");
                command.GetTemporaryRT(directionalShadowsId, shadowResolution, shadowResolution, 16, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
                command.SetRenderTarget(directionalShadowsId);
                command.ClearRenderTarget(true, false, Color.clear);

                var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, i, BatchCullingProjectionType.Perspective) { splitData = shadowSplitData };
                var shadowRendererList = context.CreateShadowRendererList(ref shadowDrawingSettings);
                command.DrawRendererList(shadowRendererList);

                var worldToShadow = ConvertToAtlasMatrix(projectionMatrix * viewMatrix);
                command.SetGlobalMatrix("_WorldToShadow", worldToShadow);
                command.SetGlobalFloat("_ZClip", 1);
                command.SetGlobalDepthBias(0f, 0f);
                command.SetGlobalTexture("_DirectionalShadows", directionalShadowsId);
            }

            // Setup globals
            var fogEnabled = RenderSettings.fog;
#if UNITY_EDITOR
            if (UnityEditor.SceneView.currentDrawingSceneView != null)
                fogEnabled &= UnityEditor.SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

            command.SetGlobalColor("_AmbientLight", RenderSettings.ambientLight.linear);
            command.SetGlobalColor("_FogColor", RenderSettings.fogColor.linear);
            command.SetGlobalFloat("_FogStartDistance", RenderSettings.fogStartDistance);
            command.SetGlobalFloat("_FogEndDistance", RenderSettings.fogEndDistance);
            command.SetGlobalFloat("_FogEnabled", fogEnabled ? 1.0f : 0.0f);
            command.SetGlobalFloat("_Time", Time.time);
            command.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);

            command.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            command.ClearRenderTarget(true, true, camera.backgroundColor.linear);

            var srpDefaultUnlitShaderPassName = new ShaderTagId("SRPDefaultUnlit");

            // Opaque
            var opaqueSortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var opaqueDrawingSettings = new DrawingSettings(srpDefaultUnlitShaderPassName, opaqueSortingSettings);
            var opaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque) { };
            var opaqueRenderListParams = new RendererListParams(cullingResults, opaqueDrawingSettings, opaqueFilteringSettings);
            var opaqueRendererList = context.CreateRendererList(ref opaqueRenderListParams);
            command.DrawRendererList(opaqueRendererList);

            // Transparent
            var transparentSortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent };
            var transparentDrawingSettings = new DrawingSettings(srpDefaultUnlitShaderPassName, transparentSortingSettings);
            var transparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent) { };
            var transparentRenderListParams = new RendererListParams(cullingResults, transparentDrawingSettings, transparentFilteringSettings);
            var transparentRendererList = context.CreateRendererList(ref transparentRenderListParams);
            command.DrawRendererList(transparentRendererList);

            context.ExecuteCommandBuffer(command);
            command.Clear();

#if UNITY_EDITOR
            if (UnityEditor.Handles.ShouldRenderGizmos())
            {
                context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
                context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
            }

            if (camera.cameraType == CameraType.SceneView)
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
        }

        context.Submit();
    }
}
