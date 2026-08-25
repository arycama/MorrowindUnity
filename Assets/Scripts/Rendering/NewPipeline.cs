using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
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
		cullingParameters.cullingOptions = CullingOptions.DisablePerObjectCulling | CullingOptions.NeedsLighting | CullingOptions.ShadowCasters;
		cullingParameters.shadowDistance = asset.Lighting.DirectionalShadowDistance;
		var cullingResults = context.Cull(ref cullingParameters);

		var fogEnabled = RenderSettings.fog;
#if UNITY_EDITOR
		if (SceneView.currentDrawingSceneView != null)
			fogEnabled &= SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

		static void SetViewDataStruct(Camera camera, CommandBuffer command, GraphicsBuffer viewDataBuffer, bool isFlipped)
		{
			var tanHalfFovY = Tan(0.5f * Radians(camera.fieldOfView));
			var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);
			var viewToWorld = Float4x4.Rotate(camera.transform.WorldRotation());
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
				viewToWorld,
				overlayMatrix,
				(far - near) * Rcp(near * far), Rcp(far), near, far,
				(Float2)viewSize, 1.0f / (Float2)viewSize,
				camera.transform.WorldPosition(), 0f,
				tanHalfFov, 0, 0
			)}.AsArray());
		}

		var viewInfo = renderGraph.AddViewInfo(new(camera.pixelWidth, camera.pixelHeight), asset.Samples);
		renderGraph.AddRenderPass("Set View Data", viewInfo, (camera, previousCameraTransform, viewDataBuffer), render: static (command, data) =>
		{
			SetViewDataStruct(data.camera, command, data.viewDataBuffer, false);
		});

		var tanHalfFovY = Tan(0.5f * Radians(camera.fieldOfView));
		var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);

		var shadowView = renderGraph.AddViewInfo(asset.Lighting.DirectionalShadowResolution);
		var sunDirection = camera.transform.WorldRotation().InverseRotate(Float3.Up);
		var sunColor = Float3.One;
		var mainLightIndex = -1;
		var sunShadow = renderGraph.GetTexture(new(shadowView, GraphicsFormat.D16_UNorm, true), Shader.PropertyToID("SunShadow"));
		var viewToSunShadow = Float4x4.Identity;
		var sunShadowEnabled = false;
		var lightCount = cullingResults.visibleLights.Length;

		var perLightInfos = new NativeArray<LightShadowCasterCullingInfo>(lightCount, Allocator.Temp);
		var splitBuffer = new NativeList<ShadowSplitData>(Allocator.Temp);

		for (var i = 0; i < lightCount; i++)
		{
			var visibleLight = cullingResults.visibleLights[i];
			var lightToWorld = (Float4x4)visibleLight.localToWorldMatrix;
			var lightColor = visibleLight.finalColor.Float3();
			var lightDirection = -lightToWorld.Forward;
			var splitRange = new RangeInt(0, 0);
			var lightRotation = lightToWorld.Rotation;
			var viewSpaceLightRotation = camera.transform.WorldRotation().InverseRotate(lightRotation);
			var hasShadows = visibleLight.light.shadows != LightShadows.None;

			if (visibleLight.lightType == LightType.Directional && mainLightIndex == -1)
			{
				mainLightIndex = i;
				sunDirection = -viewSpaceLightRotation.Forward;
				sunColor = lightColor;

				sunShadowEnabled = hasShadows && cullingResults.GetShadowCasterBounds(mainLightIndex, out _);
				if (sunShadowEnabled)
				{
					// Transform from view space to light space
					var viewToLight = Float4x4.Rotate(viewSpaceLightRotation.Inverse);
					var viewSpaceLightBounds = Geometry.GetFrustumBounds(tanHalfFov, camera.nearClipPlane, asset.Lighting.DirectionalShadowDistance, viewToLight);

					// Matrix that goes from world space to light space
					var worldToLight = Float4x4.Rotate(lightRotation.Inverse);
					var worldToLightClip = Float4x4.OrthoReverseZ(viewSpaceLightBounds).Mul(worldToLight);

					var shadowSplitData = CalculateShadowSplitData(worldToLightClip, lightDirection, true);
					shadowSplitData.shadowCascadeBlendCullingFactor = 1;
					splitRange = new RangeInt(splitBuffer.Length, 1);
					splitBuffer.Add(in shadowSplitData);

					// Matrix that converts from view space to shadow-sampling space
					viewToSunShadow = Float4x4.OrthoReverseZSample(viewSpaceLightBounds).Mul(viewToLight);

					var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, i);
					var rendererList = context.CreateShadowRendererList(ref shadowDrawingSettings);

					renderGraph.AddRenderPass("Directional Shadows", shadowView, (rendererList, worldToLightClip, asset.Lighting, viewDataBuffer), outputs: stackalloc[] { sunShadow }, render: (command, data) =>
					{
						command.SetGlobalDepthBias(data.Lighting.DirectionalShadowBias, data.Lighting.DirectionalShadowSlopeBias);
						command.SetGlobalInt("ZClip", 0);
						command.SetGlobalMatrix("WorldToShadowClip", data.worldToLightClip);
						command.SetGlobalConstantBuffer(data.viewDataBuffer, Shader.PropertyToID("ViewData"), 0, data.viewDataBuffer.stride);
						command.DrawRendererList(rendererList);
						command.SetGlobalDepthBias(0.0f, 0.0f);
						command.SetGlobalInt("ZClip", 1);
					});
				}
			}

			perLightInfos[i] = new LightShadowCasterCullingInfo
			{
				projectionType = visibleLight.lightType == LightType.Directional ? BatchCullingProjectionType.Orthographic : BatchCullingProjectionType.Perspective,
				splitExclusionMask = 0,
				splitRange = splitRange
			};
		}

		context.CullShadowCasters(cullingResults, new ShadowCastersCullingInfos
		{
			perLightInfos = perLightInfos,
			splitBuffer = splitBuffer.AsArray()
		});

		renderGraph.AddRenderPass("Set Environment Data", viewInfo, (sunDirection, sunColor, fogEnabled, environmentDataBuffer, asset.Lighting, viewToSunShadow), render: static (command, data) =>
		{
			var fogStart = data.fogEnabled ? RenderSettings.fogStartDistance : 0;
			var fogEnd = data.fogEnabled ? RenderSettings.fogEndDistance : 0;
			var fogScale = data.fogEnabled ? 1 / (fogEnd - fogStart) : 0;
			var fogOffset = data.fogEnabled ? fogStart / (fogStart - fogEnd) : 0;
			var sunShadowFadeScale = -1.0f / data.Lighting.DirectionalFadeLength;
			var sunShadowFadeOffset = data.Lighting.DirectionalShadowDistance / data.Lighting.DirectionalFadeLength;

			command.SetBufferData(data.environmentDataBuffer, stackalloc[]
			{(
				RenderSettings.ambientLight.LinearFloat3(), fogScale,
				RenderSettings.fogColor.LinearFloat3(), fogOffset,
				Time.time, fogStart, fogEnd, 0,
				data.sunDirection, sunShadowFadeScale,
				data.sunColor, sunShadowFadeOffset,
				data.viewToSunShadow
			)}.AsArray());
		});

		var depthFormat = camera.targetTexture == null ? GraphicsFormat.D32_SFloat_S8_UInt : camera.targetTexture.depthStencilFormat;
		var cameraDepth = renderGraph.GetTexture(new(viewInfo, depthFormat, true), Shader.PropertyToID("CameraDepth"));
		var albedoNormal = renderGraph.GetTexture(new(viewInfo, GraphicsFormat.R8G8B8A8_UNorm), Shader.PropertyToID("AlbedoNormal"));
		var cameraColor = renderGraph.GetTexture(new(viewInfo, GraphicsFormat.B10G11R11_UFloatPack32, true, RenderSettings.fogColor.linear), Shader.PropertyToID("CameraColor"));

		var terrainRendererParams = new RendererListParams(cullingResults, new(new("Terrain"), new(camera) { criteria = SortingCriteria.QuantizedFrontToBack }) { enableInstancing = true }, new(RenderQueueRange.all));
		var terrainRendererList = context.CreateRendererList(ref terrainRendererParams);
		renderGraph.AddRenderPass("Terrain", viewInfo, (terrainRendererList, viewDataBuffer, environmentDataBuffer), outputs: stackalloc[] { cameraDepth, albedoNormal, cameraColor }, render: static (command, data) =>
		{
			command.SetGlobalConstantBuffer(data.environmentDataBuffer, Shader.PropertyToID("EnvironmentData"), 0, data.environmentDataBuffer.stride);
			command.SetGlobalConstantBuffer(data.viewDataBuffer, Shader.PropertyToID("ViewData"), 0, data.viewDataBuffer.stride);
			command.DrawRendererList(data.terrainRendererList);
		});

		var opaqueRendererParams = new RendererListParams(cullingResults, new(new("GBuffer"), new(camera) { criteria = SortingCriteria.OptimizeStateChanges }) { enableInstancing = true }, new(RenderQueueRange.opaque));
		var opaqueRendererList = context.CreateRendererList(ref opaqueRendererParams);
		renderGraph.AddRenderPass("Gbuffer", viewInfo, (opaqueRendererList, viewDataBuffer, environmentDataBuffer), outputs: stackalloc[] { cameraDepth, albedoNormal, cameraColor }, render: static (command, data) =>
		{
			command.SetGlobalConstantBuffer(data.environmentDataBuffer, Shader.PropertyToID("EnvironmentData"), 0, data.environmentDataBuffer.stride);
			command.SetGlobalConstantBuffer(data.viewDataBuffer, Shader.PropertyToID("ViewData"), 0, data.viewDataBuffer.stride);
			command.DrawRendererList(data.opaqueRendererList);
		});

		renderGraph.AddRenderPass("Deferred", viewInfo, (deferredMaterial, asset, viewDataBuffer, environmentDataBuffer, propertyBlock, sunShadowEnabled), stackalloc[] { sunShadow }, stackalloc[] { cameraDepth, cameraColor }, stackalloc[] { cameraDepth, albedoNormal }, static (command, data) =>
		{
			data.propertyBlock.Clear();
			data.propertyBlock.SetConstantBuffer(Shader.PropertyToID("EnvironmentData"), data.environmentDataBuffer, 0, data.environmentDataBuffer.stride);
			data.propertyBlock.SetConstantBuffer(Shader.PropertyToID("ViewData"), data.viewDataBuffer, 0, data.viewDataBuffer.stride);

			if (data.asset.Samples > 1)
				command.EnableShaderKeyword("MSAA_ON");

			if (data.sunShadowEnabled)
				command.EnableShaderKeyword("SHADOWS_ON");

			command.DrawProcedural(default, data.deferredMaterial, 0, MeshTopology.Triangles, 3, 1, data.propertyBlock);

			if (data.asset.Samples > 1)
				command.DisableShaderKeyword("MSAA_ON");

			if (data.sunShadowEnabled)
				command.DisableShaderKeyword("SHADOWS_ON");
		});

		var skyRendererList = context.CreateRendererList(new RendererListDesc(new ShaderTagId("Sky"), cullingResults, camera) { renderQueueRange = RenderQueueRange.all });
		renderGraph.AddRenderPass("Sky", viewInfo, (skyRendererList, viewDataBuffer, environmentDataBuffer), outputs: stackalloc[] { cameraDepth, cameraColor }, render: static (command, data) =>
		{
			command.SetGlobalConstantBuffer(data.environmentDataBuffer, Shader.PropertyToID("EnvironmentData"), 0, data.environmentDataBuffer.stride);
			command.SetGlobalConstantBuffer(data.viewDataBuffer, Shader.PropertyToID("ViewData"), 0, data.viewDataBuffer.stride);
			command.DrawRendererList(data.skyRendererList);
		});

		var transparentRendererParams = new RendererListParams(cullingResults, new(new("Forward"), new(camera) { criteria = SortingCriteria.BackToFront | SortingCriteria.OptimizeStateChanges }) { enableInstancing = true }, new(RenderQueueRange.transparent));
		var transparentRendererList = context.CreateRendererList(ref transparentRendererParams);
		renderGraph.AddRenderPass("Forward Transparent", viewInfo, (transparentRendererList, viewDataBuffer, environmentDataBuffer, sunShadowEnabled), stackalloc[] { sunShadow }, stackalloc[] { cameraDepth, cameraColor }, render: static (command, data) =>
		{
			command.SetGlobalConstantBuffer(data.environmentDataBuffer, Shader.PropertyToID("EnvironmentData"), 0, data.environmentDataBuffer.stride);
			command.SetGlobalConstantBuffer(data.viewDataBuffer, Shader.PropertyToID("ViewData"), 0, data.viewDataBuffer.stride);

			if (data.sunShadowEnabled)
				command.EnableShaderKeyword("SHADOWS_ON");

			command.DrawRendererList(data.transparentRendererList);

			if (data.sunShadowEnabled)
				command.DisableShaderKeyword("SHADOWS_ON");
		});

		// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
		// TODO: Check for hardware msaa backbuffer resolve support
		var backbufferInfo = renderGraph.AddViewInfo(new(camera.pixelWidth, camera.pixelHeight));

		// Final blit/resolve if needed
		// TODO: This should also account for HDR
		var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
		var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;
		var sceneColor = renderGraph.GetTexture(new(viewInfo, targetFormat), Shader.PropertyToID("SceneColor"));
		renderGraph.ExportResource(sceneColor, camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture);

		// For sceneView, take the first depth sample for for gizmos, wireframe, etc.
		TextureHandle sceneDepth = default;
		var requiresSceneDepth = camera.cameraType == CameraType.SceneView;
		if (requiresSceneDepth)
		{
			sceneDepth = renderGraph.GetTexture(new(viewInfo, depthFormat), Shader.PropertyToID("SceneDepth"));
			renderGraph.ExportResource(sceneDepth, camera.targetTexture);
		}

		var renderToBackbuffer = asset.Samples == 1 && camera.targetTexture == null;
		var requiresFlip = camera.targetTexture == null;

		var resources = renderToBackbuffer ? default : requiresSceneDepth ? stackalloc[] { cameraDepth, cameraColor } : stackalloc[] { cameraColor };
		var outputs = requiresSceneDepth ? stackalloc[] { sceneDepth, sceneColor } : stackalloc[] { sceneColor };
		var inputs = renderToBackbuffer ? stackalloc[] { cameraColor } : default;

		renderGraph.AddRenderPass("Final Blit", backbufferInfo, (blitMaterial, requiresFlip, asset, requiresSceneDepth, viewDataBuffer, renderToBackbuffer), resources, outputs, inputs, static (command, data) =>
		{
			if (data.renderToBackbuffer)
				command.EnableShaderKeyword("DIRECT");

			if (data.requiresFlip)
				command.EnableShaderKeyword("FLIP");

			if (data.requiresSceneDepth)
				command.EnableShaderKeyword("DEPTH");

			if (data.asset.Samples > 1)
				command.EnableShaderKeyword("MSAA");

			command.SetGlobalConstantBuffer(data.viewDataBuffer, Shader.PropertyToID("ViewData"), 0, data.viewDataBuffer.stride);
			command.SetWireframe(false);
			command.DrawProcedural(Matrix4x4.identity, data.blitMaterial, 0, MeshTopology.Triangles, 3);

			if (data.requiresFlip)
				command.DisableShaderKeyword("FLIP");

			if (data.requiresSceneDepth)
				command.DisableShaderKeyword("DEPTH");

			if (data.asset.Samples > 1)
				command.DisableShaderKeyword("MSAA");

			if (data.renderToBackbuffer)
				command.DisableShaderKeyword("DIRECT");
		});

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

	private static ShadowSplitData CalculateShadowSplitData(Float4x4 matrix, Float3 lightDirection, bool skipNearPlane)
	{
		var shadowSplitData = new ShadowSplitData() { shadowCascadeBlendCullingFactor = 1 };
		for (var i = FrustumPlane.Left; i < FrustumPlane.Count; i++)
		{
			if (!skipNearPlane || i != FrustumPlane.Near)
			{
				var plane = matrix.GetFrustumPlane(i);
				shadowSplitData.SetCullingPlane(shadowSplitData.cullingPlaneCount++, plane);
			}
		}

		for (var i = FrustumPlane.Left; i < FrustumPlane.Count; i++)
		{
			var plane = matrix.GetFrustumPlane(i);
			if (plane.normal.Dot(lightDirection) > 0.0f)
			{
				shadowSplitData.SetCullingPlane(shadowSplitData.cullingPlaneCount++, plane);

				if (shadowSplitData.cullingPlaneCount == 10)
					break;
			}
		}

		return shadowSplitData;
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
	public Float4x4 item13;
}

internal struct ViewDataStruct
{
	public Float4x4 worldToClip;
	public Float4x4 viewToClip;
	public Float4x4 worldToView;
	public Float4x4 viewToWorld;
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

	public ViewDataStruct(Float4x4 worldToClip, Float4x4 viewToClip, Float4x4 worldToView, Float4x4 viewToWorld, Float4x4 overlayMatrix, float item5, float item6, float near, float far, Float2 item9, Float2 item10, Float3 item11, float item12, Float2 tanHalfFov, int item14, int item15)
	{
		this.worldToClip = worldToClip;
		this.viewToClip = viewToClip;
		this.worldToView = worldToView;
		this.viewToWorld = viewToWorld;
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