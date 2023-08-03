using UnityEngine;
using UnityEngine.Rendering;

public  class MorrowindRenderPipeline : RenderPipeline
{
    private MorrowindRenderPipelineAsset renderPipelineAsset;
    private CommandBuffer commandBuffer;

    public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset)
    {
        this.renderPipelineAsset = renderPipelineAsset;
        commandBuffer = new CommandBuffer() { name = "Render Camera" };
    }

    protected override void Dispose(bool disposing)
    {
        commandBuffer.Release();
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
        foreach(var camera in cameras)
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
            commandBuffer.SetGlobalColor("_FogColor", RenderSettings.fogColor);
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

            // Setup lights
            for (var i = 0; i < cullingResults.visibleLights.Length; i++)
            {
                var visibleLight = cullingResults.visibleLights[i];
                if (visibleLight.lightType != LightType.Directional)
                    continue;

                var light = visibleLight.light;
                commandBuffer.SetGlobalVector("_SunDirection", -light.transform.forward);
                commandBuffer.SetGlobalColor("_SunColor", visibleLight.finalColor);
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

            context.SetupCameraProperties(camera);

            var clearDepth = camera.clearFlags != CameraClearFlags.Nothing;
            var clearColor = camera.clearFlags == CameraClearFlags.Color;

            commandBuffer.ClearRenderTarget(clearDepth, clearColor, camera.backgroundColor.linear);
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