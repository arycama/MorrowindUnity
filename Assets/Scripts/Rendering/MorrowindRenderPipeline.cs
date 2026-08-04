using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using CustomRenderPipeline;
using System;
using Unmath;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class MorrowindRenderPipeline : CustomRenderPipelineBase<MorrowindRenderPipelineAsset>
{
	protected override bool RenderUiOverlay => false;
	protected override bool RenderWireframe => false;

	private readonly Material tonemap, pointLightMaterial, deferredLightingMaterial;

	public MorrowindRenderPipeline(MorrowindRenderPipelineAsset renderPipelineAsset) : base(renderPipelineAsset)
	{
		tonemap = new Material(Shader.Find("Hidden/Morrowind Tonemap")) { hideFlags = HideFlags.HideAndDontSave };
		pointLightMaterial = new Material(Shader.Find("Hidden/Point Light")) { hideFlags = HideFlags.HideAndDontSave };
		deferredLightingMaterial = new Material(Shader.Find("Hidden/Morrowind Deferred")) { hideFlags = HideFlags.HideAndDontSave };
	}

	protected override List<FrameRenderFeature> InitializePerFrameRenderFeatures() => new()
	{
	};

	protected override List<ViewRenderFeature> InitializePerCameraRenderFeatures() => new()
	{
		new SetupCamera(renderGraph, asset),
		new SetupLighting(renderGraph, asset),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			var cameraDepth = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.D32_SFloat_S8_UInt, clear: true, isCcw: viewPassData.isFlipped, isScreenTexture: true);
			var albedoMetallic = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.R8G8B8A8_SRGB, isCcw: viewPassData.isFlipped, isScreenTexture: true);
			var normalOcclusionRoughness = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.R8G8B8A8_UNorm, isCcw: viewPassData.isFlipped, isScreenTexture: true);
			var cameraTarget = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.B10G11R11_UFloatPack32, clear: true, clearColor: viewPassData.camera.backgroundColor.linear, isCcw: viewPassData.isFlipped, isScreenTexture: true);

			renderGraph.SetRTHandle<CameraDepth>(cameraDepth);
			renderGraph.SetRTHandle<GBufferAlbedoMetallic>(albedoMetallic);
			renderGraph.SetRTHandle<GBufferNormalOcclusionRoughness>(normalOcclusionRoughness);
			renderGraph.SetRTHandle<CameraTarget>(cameraTarget);

			var cullingResults = renderGraph.GetResource<CullingResultsData>().cullingResults;
			using (var pass = renderGraph.AddObjectRenderPass("Opaque"))
			{
				pass.Initialize("GBuffer", context, cullingResults, RenderQueueRange.opaque, viewPassData.viewSize, viewPassData.position, viewPassData.rotation, viewPassData.sortAxis, viewPassData.distanceMetric, SortingCriteria.QuantizedFrontToBack | SortingCriteria.OptimizeStateChanges);

				pass.WriteRtHandleDepth<CameraDepth>();
				pass.WriteRtHandle<GBufferAlbedoMetallic>();
				pass.WriteRtHandle<GBufferNormalOcclusionRoughness>();
				pass.WriteRtHandle<CameraTarget>();
				pass.ReadResource<ViewData>();
			}
		}),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			if(asset.PointLightMesh == null || !renderGraph.TryGetResource<PointLightData>(out var pointLightData))
				return;

			using var pass = renderGraph.AddDrawInstancedProceduralRenderPass("Light Culling", pointLightData);

			pass.Initialize(asset.PointLightMesh, 0, pointLightMaterial, pointLightData.lightCount, viewPassData.viewSize, viewPassData.viewCount);
			pass.WriteRtHandleDepth<CameraDepth>(SubPassFlags.ReadOnlyDepthStencil);

			pass.ReadResource<ViewData>();
			pass.ReadResource<PointLightData>();

			pass.SetRenderFunction(static (command, pass, data) =>
			{
				command.SetRandomWriteTarget(0, pass.GetRenderTexture(data.visibleLightBits));
				pass.SetTexture(Shader.PropertyToID("VisibleLightBitsWrite"), pass.GetRenderTexture(data.visibleLightBits));
			});
		}),

		new VolumetricLighting(asset.VolumetricLighting, renderGraph),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			using var pass = renderGraph.AddFullscreenRenderPass("Deferred Lighting");
			pass.Initialize(deferredLightingMaterial, viewPassData.viewSize, isScreenPass: true);
			pass.WriteRtHandleDepth<CameraDepth>(SubPassFlags.ReadOnlyDepthStencil);
			pass.WriteRtHandle<CameraTarget>();
			pass.ReadRtHandle<CameraDepth>();
			pass.ReadRtHandle<GBufferAlbedoMetallic>();
			pass.ReadRtHandle<GBufferNormalOcclusionRoughness>();

			pass.ReadResource<ViewData>();
			pass.ReadResource<EnvironmentData>();
			pass.ReadResource<LightingData>();
			pass.ReadResource<VolumetricLighting.Result>();

			if (pass.TryReadResource<PointLightData>())
				pass.AddKeyword("POINT_LIGHTS_ON");

			if(asset.VolumetricLighting.Enabled)
				pass.AddKeyword("VOLUMETRIC_LIGHT_ON");
		}),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			using var pass = renderGraph.AddObjectRenderPass("Sky");
			var cullingResults = renderGraph.GetResource<CullingResultsData>().cullingResults;
			pass.Initialize("Sky", context, cullingResults, RenderQueueRange.all, viewPassData.viewSize, viewPassData.position, viewPassData.rotation, viewPassData.sortAxis, viewPassData.distanceMetric);
			pass.PreventNewSubPass = true;
			pass.WriteRtHandleDepth<CameraDepth>(SubPassFlags.ReadOnlyDepthStencil);
			pass.WriteRtHandle<CameraTarget>();
			pass.ReadResource<ViewData>();
			pass.ReadResource<EnvironmentData>();
			pass.ReadResource<VolumetricLighting.Result>();
		}),

		new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
		{
			var cullingResults = renderGraph.GetResource<CullingResultsData>().cullingResults;

			// Opaque copy
			var cameraCopy = renderGraph.GetTexture(viewPassData.viewSize, GraphicsFormat.B10G11R11_UFloatPack32, isScreenTexture: true);
			using (var pass = renderGraph.AddGenericRenderPass("Opaque Copy"))
			{
				pass.PreventNewSubPass = true;

				var cameraTarget = renderGraph.GetRtHandleData<CameraTarget>().handle;
				pass.ReadTexture("", cameraTarget);
				pass.WriteTexture(cameraCopy);

				pass.SetRenderFunction((command, pass) =>
				{
					command.CopyTexture(pass.GetRenderTexture(cameraTarget), pass.GetRenderTexture(cameraCopy));
				});
			}

			using (var pass = renderGraph.AddObjectRenderPass("Transparent"))
			{
				pass.Initialize("Forward", context, cullingResults, RenderQueueRange.transparent, viewPassData.viewSize, viewPassData.position, viewPassData.rotation, viewPassData.sortAxis, viewPassData.distanceMetric, SortingCriteria.BackToFront | SortingCriteria.OptimizeStateChanges);

				pass.PreventNewSubPass = true;

				pass.WriteRtHandleDepth<CameraDepth>(SubPassFlags.ReadOnlyDepth);
				pass.WriteRtHandle<CameraTarget>();

				pass.ReadRtHandle<CameraDepth>();
				pass.ReadTexture("CameraColor", cameraCopy);

				pass.ReadResource<ViewData>();
				pass.ReadResource<EnvironmentData>();
				pass.ReadResource<LightingData>();
				pass.ReadResource<VolumetricLighting.Result>();

				if (pass.TryReadResource<PointLightData>())
					pass.AddKeyword("POINT_LIGHTS_ON");
			}

			// Tonemap
			using(var pass = renderGraph.AddBlitToScreenPass("Tonemap"))
			{
				pass.Initialize(tonemap, viewPassData.viewSize, 1, 0, 1, viewPassData.target, viewPassData.format);
				pass.PreventNewSubPass = true;
				pass.ReadRtHandle<CameraTarget>();
			}
		}),

		#if UNITY_EDITOR
			new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
			{
				if(!Handles.ShouldRenderGizmos())
					return;

				var preImageEffects = context.CreateGizmoRendererList(viewPassData.camera, GizmoSubset.PreImageEffects);
				var postImageEffects = context.CreateGizmoRendererList(viewPassData.camera, GizmoSubset.PostImageEffects);

				using (var pass = renderGraph.AddGenericRenderPass("Render Gizmos", (viewPassData.target, preImageEffects, postImageEffects)))
				{
					pass.SetRenderFunction(static (command, pass, data) =>
					{
						command.SetRenderTarget(data.target);
						command.DrawRendererList(data.preImageEffects);
						command.DrawRendererList(data.postImageEffects);
					});
				}
			}),

			new GenericViewRenderFeature(renderGraph, (in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context) =>
			{
				var wireOverlay = context.CreateWireOverlayRendererList(viewPassData.camera);

				using var pass = renderGraph.AddGenericRenderPass("Render Wireframe", (wireOverlay, viewPassData.target));
				pass.SetRenderFunction(static (command, pass, data) =>
				{
					command.SetRenderTarget(data.target);
					command.DrawRendererList(data.wireOverlay);
				});
			}),
			#endif
	};
}