using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class NewPipeline : RenderPipelineBase
{
	private static readonly int
		viewDataId = Shader.PropertyToID("ViewData"),
		environmentDataId = Shader.PropertyToID("EnvironmentData");

	private readonly NewPipelineAsset asset;
	private readonly Material blitMaterial, deferredMaterial;
	private readonly MaterialPropertyBlock propertyBlock;
	private readonly SetupView setupView;
	private readonly SetupLighting setupLighting;

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
		deferredMaterial = new Material(Shader.Find("Hidden/Morrowind Deferred")) { hideFlags = HideFlags.HideAndDontSave };
		propertyBlock = new();
		setupView = new(renderGraph);
		setupLighting = new(renderGraph, asset.Lighting);
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		setupLighting.Dispose();
	}

	protected override void RenderCamera(Camera camera, ScriptableCullingParameters cullingParameters, ScriptableRenderContext context)
	{
		cullingParameters.cullingOptions = CullingOptions.DisablePerObjectCulling | CullingOptions.NeedsLighting | CullingOptions.ShadowCasters;
		cullingParameters.shadowDistance = asset.Lighting.DirectionalShadowDistance;
		var cullingResults = context.Cull(ref cullingParameters);

		var viewData = setupView.Render(camera);
		var (environmentData, sunShadow) = setupLighting.Render(camera, cullingResults, context, viewData);

		var viewInfo = renderGraph.AddViewInfo(new(camera.pixelWidth, camera.pixelHeight), asset.Samples);
		var cameraDepth = renderGraph.GetTexture(new(viewInfo, GraphicsFormat.D32_SFloat_S8_UInt, true), Shader.PropertyToID("CameraDepth"));
		var albedoNormal = renderGraph.GetTexture(new(viewInfo, GraphicsFormat.R8G8B8A8_UNorm), Shader.PropertyToID("AlbedoNormal"));
		var cameraColor = renderGraph.GetTexture(new(viewInfo, GraphicsFormat.B10G11R11_UFloatPack32, true, RenderSettings.fogColor.linear), Shader.PropertyToID("CameraColor"));

		using (var pass = renderGraph.AddRenderPass("Terrain"))
		{
			pass.ViewHandle = viewInfo;
			pass.DepthStencil = cameraDepth;
			pass.AddOutputs(stackalloc[] { albedoNormal, cameraColor });

			var rendererList = context.CreateRendererList(new(new ShaderTagId("Terrain"), cullingResults, camera) { renderQueueRange = RenderQueueRange.all, sortingCriteria = SortingCriteria.QuantizedFrontToBack });
			pass.SetRenderFunction((rendererList, viewData, environmentData), static (command, data) =>
			{
				command.SetGlobalConstantBuffer(data.environmentData, environmentDataId, 0, data.environmentData.stride);
				command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
				command.DrawRendererList(data.rendererList);
			});
		}

		using (var pass = renderGraph.AddRenderPass("GBuffer"))
		{
			pass.ViewHandle = viewInfo;
			pass.DepthStencil = cameraDepth;
			pass.AddOutputs(stackalloc[] { albedoNormal, cameraColor });

			var rendererParams = new RendererListParams(cullingResults, new(new("GBuffer"), new(camera) { criteria = SortingCriteria.OptimizeStateChanges }) { enableInstancing = true }, new(RenderQueueRange.opaque));
			var rendererList = context.CreateRendererList(ref rendererParams);
			pass.SetRenderFunction((rendererList, viewData, environmentData), static (command, data) =>
			{
				command.SetGlobalConstantBuffer(data.environmentData, environmentDataId, 0, data.environmentData.stride);
				command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
				command.DrawRendererList(data.rendererList);
			});
		}

		using (var pass = renderGraph.AddRenderPass("Deferred"))
		{
			pass.ViewHandle = viewInfo;
			pass.DepthStencil = cameraDepth;
			pass.AddOutput(cameraColor);
			pass.AddInputs(stackalloc[] { cameraDepth, albedoNormal });

			var hasShadow = renderGraph.IsResourceWritten(sunShadow);
			if (hasShadow)
			{
				pass.AddResource(sunShadow);
				pass.AddKeyword("SHADOWS_ON");
			}

			if (asset.Samples > 1)
				pass.AddKeyword("MSAA_ON");

			pass.SetRenderFunction((deferredMaterial, viewData, environmentData, propertyBlock), static (command, data) =>
			{
				data.propertyBlock.Clear();
				data.propertyBlock.SetConstantBuffer(environmentDataId, data.environmentData, 0, data.environmentData.stride);
				data.propertyBlock.SetConstantBuffer(viewDataId, data.viewData, 0, data.viewData.stride);
				command.DrawProcedural(default, data.deferredMaterial, 0, MeshTopology.Triangles, 3, 1, data.propertyBlock);
			});
		}

		using (var pass = renderGraph.AddRenderPass("Sky"))
		{
			pass.ViewHandle = viewInfo;
			pass.DepthStencil = cameraDepth;
			pass.AddOutput(cameraColor);

			var rendererList = context.CreateRendererList(new(new ShaderTagId("Sky"), cullingResults, camera) { renderQueueRange = RenderQueueRange.all });
			pass.SetRenderFunction((rendererList, viewData, environmentData), static (command, data) =>
			{
				command.SetGlobalConstantBuffer(data.environmentData, environmentDataId, 0, data.environmentData.stride);
				command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
				command.DrawRendererList(data.rendererList);
			});
		}

		using (var pass = renderGraph.AddRenderPass("Forward Transparent"))
		{
			pass.ViewHandle = viewInfo;
			pass.DepthStencil = cameraDepth;
			pass.AddOutput(cameraColor);

			var hasShadow = renderGraph.IsResourceWritten(sunShadow);
			if (hasShadow)
			{
				pass.AddResource(sunShadow);
				pass.AddKeyword("SHADOWS_ON");
			}

			var rendererParams = new RendererListParams(cullingResults, new(new("Forward"), new(camera) { criteria = SortingCriteria.BackToFront | SortingCriteria.OptimizeStateChanges }) { enableInstancing = true }, new(RenderQueueRange.transparent));
			var rendererList = context.CreateRendererList(ref rendererParams);
			pass.SetRenderFunction((rendererList, viewData, environmentData), (command, data) =>
			{
				command.SetGlobalConstantBuffer(data.environmentData, environmentDataId, 0, data.environmentData.stride);
				command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
				command.DrawRendererList(data.rendererList);
			});
		}

		using (var pass = renderGraph.AddRenderPass("Final Blit"))
		{
			// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
			// TODO: Check for hardware msaa backbuffer resolve support
			pass.ViewHandle = renderGraph.AddViewInfo(new(camera.pixelWidth, camera.pixelHeight));

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
				sceneDepth = renderGraph.GetTexture(new(viewInfo, GraphicsFormat.D32_SFloat_S8_UInt), Shader.PropertyToID("SceneDepth"));
				renderGraph.ExportResource(sceneDepth, camera.targetTexture);
			}

			var renderToBackbuffer = asset.Samples == 1 && camera.targetTexture == null;
			if (renderToBackbuffer)
			{
				pass.AddInput(cameraColor);
				pass.AddKeyword("DIRECT");
			}
			else
			{
				pass.AddResource(cameraColor);

				if (requiresSceneDepth)
					pass.AddResource(cameraDepth);
			}

			// TODO: Currently we need to set depth as the first output if it exists. Once this is replaced with a set depth stencil function, this wont be neccessary
			if (requiresSceneDepth)
			{
				pass.DepthStencil = sceneDepth;
				pass.AddOutput(sceneColor);
				pass.AddKeyword("DEPTH");
			}
			else
				pass.AddOutputs(stackalloc[] { sceneColor });

			var requiresFlip = camera.targetTexture == null;
			if (requiresFlip)
				pass.AddKeyword("FLIP");

			if (asset.Samples > 1)
				pass.AddKeyword("MSAA");

			pass.SetRenderFunction((blitMaterial, viewData), static (command, data) =>
			{
				command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
				command.SetWireframe(false);
				command.DrawProcedural(Matrix4x4.identity, data.blitMaterial, 0, MeshTopology.Triangles, 3);
			});
		}

#if UNITY_EDITOR
		// Render gizmos
		if (Handles.ShouldRenderGizmos())
		{
			using var pass = renderGraph.AddRenderPass("Render Gizmos");
			var preImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PreImageEffects);
			var postImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PostImageEffects);

			pass.SetRenderFunction((preImageEffectsRenderList, postImageEffectsRenderList), static (command, data) =>
			{
				// Note that gizmos use their own matrix logic which we can't override
				command.DrawRendererList(data.preImageEffectsRenderList);
				command.DrawRendererList(data.postImageEffectsRenderList);
			});
		}

		// Render wireframe
		if (camera.cameraType == CameraType.SceneView)
		{
			viewData = setupView.Render(camera, true);

			using var pass = renderGraph.AddRenderPass("Wireframe");
			var wireframeRendererList = context.CreateWireOverlayRendererList(camera);
			pass.SetRenderFunction((camera, wireframeRendererList, context, viewData), static (command, data) =>
			{
				data.context.SetupCameraProperties(data.camera);
				command.SetGlobalConstantBuffer(data.viewData, viewDataId, 0, data.viewData.stride);
				command.DrawRendererList(data.wireframeRendererList);
			});
		}
#endif
	}
}