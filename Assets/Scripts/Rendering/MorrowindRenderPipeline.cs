using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

public partial class MorrowindRenderPipeline : RenderPipeline
{
    private MorrowindRenderPipelineAsset renderPipelineAsset;
    private CommandBuffer command;
    private GraphicsBuffer perFrameBuffer, perViewBuffer, perCascadeData;

    public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset)
    {
        this.renderPipelineAsset = renderPipelineAsset;
        command = new CommandBuffer() { name = "Render Camera" };

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
            reflectionProbesBlendDistance = false,
            overridesEnableLODCrossFade = false,
            overridesLightProbeSystem = true,
            overridesLightProbeSystemWarningMessage = default,
            supportsHDR = false,
        };

        perFrameBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1, UnsafeUtility.SizeOf<PerFrameData>());
        perViewBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1, UnsafeUtility.SizeOf<PerViewData>());
        perCascadeData = new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1, UnsafeUtility.SizeOf<PerCascadeData>());
    }

    protected override void Dispose(bool disposing)
    {
        command.Release();
        perFrameBuffer.Release();
        perViewBuffer.Release();
        perCascadeData.Release();
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

    struct PerFrameData
    {
        public Vector3 ambientLight;
        public float fogScale;
        public Vector3 sunDirection;
        public float fogOffset;
        public Vector3 sunColor;
        public float time;
        public Vector3 fogColor;
        public float padding;

        public PerFrameData(Vector3 ambientLight, float fogScale, Vector3 sunDirection, float fogOffset, Vector3 sunColor, float time, Vector3 fogColor)
        {
            this.ambientLight = ambientLight;
            this.fogScale = fogScale;
            this.sunDirection = sunDirection;
            this.fogOffset = fogOffset;
            this.sunColor = sunColor;
            this.time = time;
            this.fogColor = fogColor;
            this.padding = 0;
        }
    }

    struct PerViewData
    {
        public Matrix4x4 worldToClip;
        public Matrix4x4 worldToShadow;

        public PerViewData(Matrix4x4 worldToClip, Matrix4x4 worldToShadow)
        {
            this.worldToClip = worldToClip;
            this.worldToShadow = worldToShadow;
        }
    }

    struct PerCascadeData
    {
        public Matrix4x4 worldToShadowClip;

        public PerCascadeData(Matrix4x4 worldToShadowClip) => this.worldToShadowClip = worldToShadowClip;
    }

    private void SetConstantBufferData<T>(GraphicsBuffer buffer, CommandBuffer command, T data) where T : struct
    {
        using (ListPool<T>.Get(out var list))
        {
            list.Add(data);
            command.SetBufferData(buffer, list);
        }
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
            var sunDirection = Vector3.up;
            var sunColor = Color.white;
            var worldToShadow = Matrix4x4.identity;
            var directionalShadowsId = Shader.PropertyToID("_DirectionalShadows");
                var shadowResolution = renderPipelineAsset.ShadowResolution;
            command.GetTemporaryRT(directionalShadowsId, shadowResolution, shadowResolution, 16, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            command.SetRenderTarget(directionalShadowsId);
            command.ClearRenderTarget(true, false, Color.clear);

            for (var i = 0; i < cullingResults.visibleLights.Length; i++)
            {
                var visibleLight = cullingResults.visibleLights[i];
                if (visibleLight.lightType != LightType.Directional)
                    continue;

                sunDirection = -visibleLight.light.transform.forward;
                sunColor = visibleLight.finalColor.linear;

                if (!cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(i, 0, 1, Vector3.zero, shadowResolution, visibleLight.light.shadowNearPlane, out var viewMatrix, out var projectionMatrix, out var shadowSplitData))
                    continue;

                command.SetGlobalFloat("_ZClip", 0);
                command.SetGlobalDepthBias(renderPipelineAsset.ShadowBias, renderPipelineAsset.ShadowSlopeBias);

                SetConstantBufferData(perCascadeData, command, new PerCascadeData(GL.GetGPUProjectionMatrix(projectionMatrix, true) * viewMatrix));
                command.SetGlobalConstantBuffer(perCascadeData, "PerCascadeData", 0, perCascadeData.stride);

                var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, i, BatchCullingProjectionType.Perspective) { splitData = shadowSplitData };
                var shadowRendererList = context.CreateShadowRendererList(ref shadowDrawingSettings);
                command.DrawRendererList(shadowRendererList);

                command.SetGlobalDepthBias(0f, 0f);
                command.SetGlobalFloat("_ZClip", 1);

                worldToShadow = ConvertToAtlasMatrix(projectionMatrix * viewMatrix);
            }

            // Setup globals
            var fogEnabled = RenderSettings.fog;
#if UNITY_EDITOR
            if (UnityEditor.SceneView.currentDrawingSceneView != null)
                fogEnabled &= UnityEditor.SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

            var fogStart = RenderSettings.fogStartDistance;
            var fogEnd = RenderSettings.fogEndDistance;
            var fogScale = fogEnabled ? 1 / (fogEnd - fogStart) : 0;
            var fogOffset = fogEnabled ? fogStart / (fogStart - fogEnd) : 0;

            SetConstantBufferData(perFrameBuffer, command, new PerFrameData
            (
                (Vector4)RenderSettings.ambientLight.linear,
                fogScale,
                sunDirection,
                fogOffset,
                (Vector4)sunColor,
                Time.time,
                (Vector4)RenderSettings.fogColor.linear
            ));
            command.SetGlobalConstantBuffer(perFrameBuffer, "PerFrameData", 0, perFrameBuffer.stride);

            SetConstantBufferData(perViewBuffer, command, new PerViewData(GL.GetGPUProjectionMatrix(camera.projectionMatrix, camera.cameraType != CameraType.Game) * camera.worldToCameraMatrix, worldToShadow));
            command.SetGlobalConstantBuffer(perViewBuffer, "PerViewData", 0, perViewBuffer.stride);
            command.SetGlobalTexture("_DirectionalShadows", directionalShadowsId);

            command.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            command.ClearRenderTarget(true, true, camera.backgroundColor.linear);

            var srpDefaultUnlitShaderPassName = new ShaderTagId("SRPDefaultUnlit");

            // Opaque
            var opaqueSortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.OptimizeStateChanges };
            var opaqueDrawingSettings = new DrawingSettings(srpDefaultUnlitShaderPassName, opaqueSortingSettings) { enableInstancing = true };
            var opaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            var opaqueRenderListParams = new RendererListParams(cullingResults, opaqueDrawingSettings, opaqueFilteringSettings);
            var opaqueRendererList = context.CreateRendererList(ref opaqueRenderListParams);
            command.DrawRendererList(opaqueRendererList);

            // Transparent
            var transparentSortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent };
            var transparentDrawingSettings = new DrawingSettings(srpDefaultUnlitShaderPassName, transparentSortingSettings) { enableInstancing = true };
            var transparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent);
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
