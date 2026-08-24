using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;
using Quaternion = Unmath.Quaternion;

public class NewPipeline : RenderPipelineBase
{
	private readonly NewPipelineAsset asset;
	private readonly Material blitMaterial, deferredMaterial;
	private readonly Dictionary<int, (Float3, Quaternion, Float4x4)> previousCameraTransform = new();
	private readonly GraphicsBuffer environmentDataBuffer, viewDataBuffer;
	private readonly MaterialPropertyBlock propertyBlock;

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
		deferredMaterial = new Material(Shader.Find("Hidden/Morrowind Deferred")) { hideFlags = HideFlags.HideAndDontSave };
		propertyBlock = new();
		environmentDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1, UnsafeUtility.SizeOf<EnvironmentDataStruct>());
		viewDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1, UnsafeUtility.SizeOf<ViewDataStruct>());
	}

	protected override void RenderCamera(Camera camera, ScriptableCullingParameters cullingParameters, ScriptableRenderContext context)
	{
		cullingParameters.cullingOptions = CullingOptions.DisablePerObjectCulling | CullingOptions.NeedsLighting;
		var cullingResults = context.Cull(ref cullingParameters);

		var fogEnabled = RenderSettings.fog;
#if UNITY_EDITOR
		if (SceneView.currentDrawingSceneView != null)
			fogEnabled &= SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

		var sunDirection = camera.transform.WorldRotation().InverseRotate(Float3.Up);
		var sunColor = Float3.One;
		for (var i = 0; i < cullingResults.visibleLights.Length; i++)
		{
			var visibleLight = cullingResults.visibleLights[i];
			if (visibleLight.lightType != LightType.Directional)
				continue;

			var lightToWorld = (Float4x4)visibleLight.localToWorldMatrix;
			var lightRotation = lightToWorld.Rotation;
			var viewSpaceLightRotation = camera.transform.WorldRotation().InverseRotate(lightRotation);
			sunDirection = -viewSpaceLightRotation.Forward;
			sunColor = visibleLight.finalColor.Float3();
			break;
		}

		static void SetViewDataStruct(Camera camera, CommandBuffer command, GraphicsBuffer viewDataBuffer, bool isFlipped)
		{
			var tanHalfFovY = Tan(0.5f * Radians(camera.fieldOfView));
			var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);
			var worldToView = Float4x4.Rotate(camera.transform.WorldRotation().Inverse);
			var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, camera.nearClipPlane, camera.farClipPlane, 0, isFlipped);
			var worldToClip = viewToClip.Mul(worldToView);
			var overlayMatrix = Float4x4.OrthoReverseZ(-Screen.width / 2f, Screen.width / 2f, -Screen.height / 2f, Screen.height / 2f, 0, 1);

			var viewSize = new Int2(camera.pixelWidth, camera.pixelHeight);
			var near = camera.nearClipPlane;
			var far = camera.farClipPlane;

			command.SetBufferData(viewDataBuffer, stackalloc[]
			{(
				worldToClip,
				viewToClip,
				worldToView,
				overlayMatrix,
				(far - near) * Rcp(near * far), Rcp(far), near, far,
				(Float2)viewSize, 1.0f / (Float2)viewSize,
				camera.transform.WorldPosition(), 0f,
				tanHalfFov, 0, 0
			)}.AsArray());
		}

		var viewInfo = renderGraph.AddViewInfo(new(camera.pixelWidth, camera.pixelHeight), asset.Samples);
		renderGraph.AddRenderPass("Set View Data", viewInfo, (camera, sunDirection, sunColor, fogEnabled, previousCameraTransform, environmentDataBuffer, viewDataBuffer), render: static (command, data) =>
		{
			var fogStart = data.fogEnabled ? RenderSettings.fogStartDistance : 0;
			var fogEnd = data.fogEnabled ? RenderSettings.fogEndDistance : 0;
			var fogScale = data.fogEnabled ? 1 / (fogEnd - fogStart) : 0;
			var fogOffset = data.fogEnabled ? fogStart / (fogStart - fogEnd) : 0;

			command.SetBufferData(data.environmentDataBuffer, stackalloc[]
			{(
				RenderSettings.ambientLight.LinearFloat3(), fogScale,
				RenderSettings.fogColor.LinearFloat3(), fogOffset,
				Time.time, fogStart, fogEnd, 0,
				data.sunDirection, 0,
				data.sunColor, 0
			)}.AsArray());

			SetViewDataStruct(data.camera, command, data.viewDataBuffer, false);
		});

		var depthFormat = camera.targetTexture == null ? GraphicsFormat.D32_SFloat_S8_UInt : camera.targetTexture.depthStencilFormat;
		var cameraDepth = renderGraph.GetTexture(new(viewInfo, depthFormat, true), Shader.PropertyToID("CameraDepth"));
		var cameraColor = renderGraph.GetTexture(new(viewInfo, GraphicsFormat.B10G11R11_UFloatPack32, true, RenderSettings.fogColor.linear), Shader.PropertyToID("CameraColor"));
		var albedoNormal = renderGraph.GetTexture(new(viewInfo, GraphicsFormat.R8G8B8A8_UNorm), Shader.PropertyToID("AlbedoNormal"));

		var opaqueRendererParams = new RendererListParams(cullingResults, new(new("GBuffer"), new(camera) { criteria = SortingCriteria.CommonOpaque }) { enableInstancing = true }, new(RenderQueueRange.opaque));
		var opaqueRendererList = context.CreateRendererList(ref opaqueRendererParams);
		renderGraph.AddRenderPass("Gbuffer", viewInfo, (opaqueRendererList, viewDataBuffer, environmentDataBuffer), stackalloc[] { cameraDepth, albedoNormal, cameraColor }, render: static (command, data) =>
		{
			command.SetGlobalConstantBuffer(data.environmentDataBuffer, Shader.PropertyToID("EnvironmentData"), 0, data.environmentDataBuffer.stride);
			command.SetGlobalConstantBuffer(data.viewDataBuffer, Shader.PropertyToID("ViewData"), 0, data.viewDataBuffer.stride);
			command.DrawRendererList(data.opaqueRendererList);
		});

		renderGraph.AddRenderPass("Deferred Lighting", viewInfo, (deferredMaterial, asset, viewDataBuffer, environmentDataBuffer, propertyBlock), stackalloc[] { cameraDepth, cameraColor }, default, stackalloc[] { cameraDepth, albedoNormal }, static (command, data) =>
		{
			data.propertyBlock.Clear();
			data.propertyBlock.SetConstantBuffer(Shader.PropertyToID("EnvironmentData"), data.environmentDataBuffer, 0, data.environmentDataBuffer.stride);
			data.propertyBlock.SetConstantBuffer(Shader.PropertyToID("ViewData"), data.viewDataBuffer, 0, data.viewDataBuffer.stride);

			if (data.asset.Samples > 1)
				command.EnableShaderKeyword("MSAA_ON");

			command.DrawProcedural(default, data.deferredMaterial, 0, MeshTopology.Triangles, 3, 1, data.propertyBlock);

			if (data.asset.Samples > 1)
				command.DisableShaderKeyword("MSAA_ON");
		});

		var transparentRendererParams = new RendererListParams(cullingResults, new(new("Forward"), new(camera) { criteria = SortingCriteria.CommonTransparent }) { enableInstancing = true }, new(RenderQueueRange.transparent));
		var transparentRendererList = context.CreateRendererList(ref transparentRendererParams);
		renderGraph.AddRenderPass("Render Forward Transparent", viewInfo, (transparentRendererList, viewDataBuffer, environmentDataBuffer), stackalloc[] { cameraDepth, cameraColor }, render: static (command, data) =>
		{
			command.SetGlobalConstantBuffer(data.environmentDataBuffer, Shader.PropertyToID("EnvironmentData"), 0, data.environmentDataBuffer.stride);
			command.SetGlobalConstantBuffer(data.viewDataBuffer, Shader.PropertyToID("ViewData"), 0, data.viewDataBuffer.stride);
			command.DrawRendererList(data.transparentRendererList);
		});

		// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
		// TODO: Check for hardware msaa backbuffer resolve support
		var backbufferInfo = renderGraph.AddViewInfo(new(camera.pixelWidth, camera.pixelHeight));
		var requiresSceneDepth = camera.cameraType == CameraType.SceneView;

		// Final blit/resolve if needed
		// TODO: This should also account for HDR
		var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
		var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;
		var backbufferColor = renderGraph.GetTexture(new(viewInfo, targetFormat), Shader.PropertyToID("SceneColor"));

		// For sceneView, take the first depth sample for for gizmos, wireframe, etc.
		TextureHandle sceneDepth = default;
		if (requiresSceneDepth)
		{
			sceneDepth = renderGraph.GetTexture(new(viewInfo, depthFormat), Shader.PropertyToID("SceneDepth"));
			renderGraph.ExportResource(sceneDepth, camera.targetTexture);
		}

		var outputs = requiresSceneDepth ? stackalloc[] { sceneDepth, backbufferColor } : stackalloc[] { backbufferColor };
		var inputs = requiresSceneDepth ? stackalloc[] { cameraColor, cameraDepth } : stackalloc[] { cameraColor };
		renderGraph.AddRenderPass("Final Blit", backbufferInfo, (blitMaterial, camera, asset, requiresSceneDepth, viewDataBuffer), outputs, inputs, render: static (command, data) =>
		{
			if (data.camera.targetTexture == null)
				command.EnableShaderKeyword("FLIP");

			if (data.requiresSceneDepth)
				command.EnableShaderKeyword("DEPTH");

			if (data.asset.Samples > 1)
				command.EnableShaderKeyword("MSAA");

			command.SetGlobalConstantBuffer(data.viewDataBuffer, Shader.PropertyToID("ViewData"), 0, data.viewDataBuffer.stride);
			command.SetWireframe(false);
			command.DrawProcedural(Matrix4x4.identity, data.blitMaterial, 0, MeshTopology.Triangles, 3);

			if (data.camera.targetTexture == null)
				command.DisableShaderKeyword("FLIP");

			if (data.requiresSceneDepth)
				command.DisableShaderKeyword("DEPTH");

			if (data.asset.Samples > 1)
				command.DisableShaderKeyword("MSAA");
		});

		renderGraph.ExportResource(backbufferColor, camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture);

#if UNITY_EDITOR
		// Render gizmos
		var viewSize = new Int2(camera.pixelWidth, camera.pixelHeight);
		if (Handles.ShouldRenderGizmos())
		{
			var preImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PreImageEffects);
			var postImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PostImageEffects);

			renderGraph.AddRenderPass("Render Gizmos", viewInfo, (preImageEffectsRenderList, postImageEffectsRenderList), render: static (command, data) =>
			{
				// Note that gizmos use their own matrix logic which we can't override
				command.DrawRendererList(data.preImageEffectsRenderList);
				command.DrawRendererList(data.postImageEffectsRenderList);
			});
		}

		// Render wireframe
		if (camera.cameraType == CameraType.SceneView)
		{
			var wireframeRendererList = context.CreateWireOverlayRendererList(camera);
			renderGraph.AddRenderPass("Render Wireframe", viewInfo, (camera, wireframeRendererList, context, viewDataBuffer), render: static (command, data) =>
			{
				SetViewDataStruct(data.camera, command, data.viewDataBuffer, true);
				data.context.SetupCameraProperties(data.camera);
				command.DrawRendererList(data.wireframeRendererList);
			});
		}
#endif
	}
}

internal struct EnvironmentDataStruct
{
	public Float3 Item1;
	public float fogScale;
	public Float3 Item3;
	public float fogOffset;
	public float time;
	public float fogStart;
	public float fogEnd;
	public int Item8;
	public Float3 sunDirection;
	public int Item10;
	public Float3 sunColor;
	public int Item12;

	public EnvironmentDataStruct(Float3 item1, float fogScale, Float3 item3, float fogOffset, float time, float fogStart, float fogEnd, int item8, Float3 sunDirection, int item10, Float3 sunColor, int item12)
	{
		Item1 = item1;
		this.fogScale = fogScale;
		Item3 = item3;
		this.fogOffset = fogOffset;
		this.time = time;
		this.fogStart = fogStart;
		this.fogEnd = fogEnd;
		Item8 = item8;
		this.sunDirection = sunDirection;
		Item10 = item10;
		this.sunColor = sunColor;
		Item12 = item12;
	}
}

internal struct ViewDataStruct
{
	public Float4x4 worldToClip;
	public Float4x4 viewToClip;
	public Float4x4 worldToView;
	public Float4x4 overlayMatrix;
	public float Item5;
	public float Item6;
	public float near;
	public float far;
	public Float2 Item9;
	public Float2 Item10;
	public Float3 Item11;
	public float Item12;
	public Float2 tanHalfFov;
	public int Item14;
	public int Item15;

	public ViewDataStruct(Float4x4 worldToClip, Float4x4 viewToClip, Float4x4 worldToView, Float4x4 overlayMatrix, float item5, float item6, float near, float far, Float2 item9, Float2 item10, Float3 item11, float item12, Float2 tanHalfFov, int item14, int item15)
	{
		this.worldToClip = worldToClip;
		this.viewToClip = viewToClip;
		this.worldToView = worldToView;
		this.overlayMatrix = overlayMatrix;
		Item5 = item5;
		Item6 = item6;
		this.near = near;
		this.far = far;
		Item9 = item9;
		Item10 = item10;
		Item11 = item11;
		Item12 = item12;
		this.tanHalfFov = tanHalfFov;
		Item14 = item14;
		Item15 = item15;
	}
}