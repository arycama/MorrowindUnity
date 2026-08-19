using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;

public class NewPipeline : RenderPipeline
{
	private readonly NewPipelineAsset asset;
	private readonly CommandBuffer command;
	private readonly Material blitMaterial;
	private readonly NativeList<AttachmentDescriptor> attachments = new(8, Allocator.Persistent);
	private readonly NativeList<SubPassDescriptor> subpasses = new(8, Allocator.Persistent);

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		command = new() { name = "Render Frame" };
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
	}

	protected override void Dispose(bool disposing)
	{
		attachments.Dispose();
	}

	public RenderTextureDescriptor GetRenderTextureDescriptor(RenderTargetDescriptor a, bool resolve)
	{
		bool isColor = false, isDepth = false, isStencil = false;
		switch (a.format)
		{
			case GraphicsFormat.D16_UNorm:
			case GraphicsFormat.D24_UNorm:
			case GraphicsFormat.D32_SFloat:
				isDepth = true;
				break;
			case GraphicsFormat.D16_UNorm_S8_UInt:
			case GraphicsFormat.D24_UNorm_S8_UInt:
			case GraphicsFormat.D32_SFloat_S8_UInt:
				isDepth = true;
				isStencil = true;
				break;
			case GraphicsFormat.S8_UInt:
				isStencil = true;
				break;
			default:
				isColor = true;
				break;
		}

		return new RenderTextureDescriptor
		{
			width = a.size.x,
			height = a.size.y,
			volumeDepth = 1,
			msaaSamples = resolve ? 1 : a.samples,
			graphicsFormat = isColor ? a.format : GraphicsFormat.None,
			depthStencilFormat = isDepth ? a.format : GraphicsFormat.None,
			mipCount = 1,
			dimension = TextureDimension.Tex2D,
			shadowSamplingMode = ShadowSamplingMode.None,
			vrUsage = VRTextureUsage.None,
			enableRandomWrite = false,
			stencilFormat = isStencil ? GraphicsFormat.R8_UInt : GraphicsFormat.None,
			useMipMap = false,
			bindMS = a.samples > 1 && !resolve,
		};
	}

	private void AddAttachment(RenderTargetDescriptor descriptor, RenderTargetIdentifier? target = null, bool resolve = true)
	{
		var requiresResolve = target.HasValue && resolve && descriptor.samples > 1;

		attachments.Add(new AttachmentDescriptor
		{
			loadAction = descriptor.clear ? RenderBufferLoadAction.Clear : RenderBufferLoadAction.DontCare, // TODO: Support load, if contents have already been written to previously
			storeAction = target == null ? RenderBufferStoreAction.DontCare : (requiresResolve ? RenderBufferStoreAction.Resolve : RenderBufferStoreAction.Store), // TODO: Only store if result is read later
			graphicsFormat = descriptor.format,
			loadStoreTarget = target == null || requiresResolve ? BuiltinRenderTextureType.None : target.Value, // TODO: Only set if target is read later
			resolveTarget = requiresResolve ? target.Value : BuiltinRenderTextureType.None, 
			clearColor = descriptor.clearColor,
			clearDepth = descriptor.clearDepth,
			clearStencil = descriptor.clearStencil
		});
	}

	private void BeginRenderPass(Int2 size, int depthBufferIndex, int samples, string name)
	{
		Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(name)];
		_ = Encoding.UTF8.GetBytes(name, debugNameUtf8);

		command.BeginRenderPass(size.x, size.y, samples, attachments.AsArray(), depthBufferIndex, subpasses.AsArray(), debugNameUtf8);
		subpasses.Clear();
		attachments.Clear();
	}

	protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
	{
		foreach (var camera in cameras)
		{
			if (!camera.TryGetCullingParameters(out var cullingParameters))
				continue;

#if UNITY_EDITOR
			if (camera.cameraType == CameraType.SceneView)
				ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
			else
#endif
				ScriptableRenderContext.EmitGeometryForCamera(camera);

			cullingParameters.cullingOptions = CullingOptions.DisablePerObjectCulling;
			var cullingResults = context.Cull(ref cullingParameters);
			var cameraTarget = Shader.PropertyToID("CameraTarget");
			var cameraDepth = Shader.PropertyToID("CameraDepth");

			// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
			// TODO: Check for hardware msaa backbuffer resolve support
			var renderToBackbuffer = asset.Samples == 1 && camera.targetTexture == null;

			// TODO: This should also account for HDR
			var depthFormat = camera.targetTexture == null ? GraphicsFormat.D32_SFloat_S8_UInt : camera.targetTexture.depthStencilFormat;
			var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
			var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;
			var cameraTargetDescriptor = new RenderTargetDescriptor(new(camera.pixelWidth, camera.pixelHeight), targetFormat, asset.Samples, true, RenderSettings.fogColor.linear);
			var cameraDepthDescriptor = new RenderTargetDescriptor(new(camera.pixelWidth, camera.pixelHeight), depthFormat, asset.Samples, true);

			// Pass 0
			{
				// Setup view data
				var tanHalfFovY = Tan(0.5f * Radians(camera.fieldOfView));
				var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);
				var worldToView = Float4x4.WorldToLocal(0.0f, camera.transform.WorldRotation());
				var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, camera.nearClipPlane, camera.farClipPlane);
				var worldToClip = viewToClip.Mul(worldToView);

				var fogEnabled = RenderSettings.fog;
#if UNITY_EDITOR
				if (SceneView.currentDrawingSceneView != null)
					fogEnabled &= SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

				command.SetGlobalMatrix("WorldToView", worldToView);
				command.SetGlobalMatrix("WorldToClip", worldToClip);
				command.SetGlobalVector("ViewPosition", camera.transform.position);
				command.SetGlobalVector("SunDirection", camera.transform.WorldRotation().InverseRotate(-RenderSettings.sun.transform.forward));
				command.SetGlobalVector("SunColor", RenderSettings.sun.color.linear);
				command.SetGlobalVector("AmbientLight", RenderSettings.ambientLight.linear);
				command.SetGlobalVector("FogColor", RenderSettings.fogColor.linear);
				command.SetGlobalFloat("FogScale", fogEnabled ? 1 / (RenderSettings.fogEndDistance - RenderSettings.fogStartDistance) : 0);
				command.SetGlobalFloat("FogOffset", fogEnabled ? RenderSettings.fogStartDistance / (RenderSettings.fogStartDistance - RenderSettings.fogEndDistance) : 0);

				// For scene view we need depth for wireframe (Note that msaa depth can not be resolved automatically so we need to store and manually resolve later)
				if (camera.cameraType == CameraType.SceneView)
					command.GetTemporaryRT(cameraDepth, GetRenderTextureDescriptor(cameraDepthDescriptor, false));
				AddAttachment(cameraDepthDescriptor, camera.cameraType == CameraType.SceneView ? cameraDepth : BuiltinRenderTextureType.None, false);

				if (!renderToBackbuffer)
					command.GetTemporaryRT(cameraTarget, GetRenderTextureDescriptor(cameraTargetDescriptor, true));
				AddAttachment(cameraTargetDescriptor, renderToBackbuffer ? BuiltinRenderTextureType.CameraTarget : cameraTarget, true);

				var colorOutputs = new AttachmentIndexArray(1);
				colorOutputs[0] = 1;
				subpasses.Add(new() { colorOutputs = colorOutputs });
				BeginRenderPass(new(camera.pixelWidth, camera.pixelHeight), 0, asset.Samples, "Base Pass");

				// This is basically only required if the camera is rendering to a no-resolved MSAA texture, which is the case for depth in the scene view..
				if (asset.Samples > 1 && camera.targetTexture == null)
					command.SetInvertCulling(true);

				command.DrawRendererList(context.CreateRendererList(new(new ShaderTagId("Forward"), cullingResults, camera) { renderQueueRange = RenderQueueRange.opaque }));

				if (asset.Samples > 1 && camera.targetTexture == null)
					command.SetInvertCulling(false);

				command.EndRenderPass();
			}

			// Pass 1
			if (!renderToBackbuffer)
			{
				// Need to bind the camera's depth buffer 
				var attachmentCount = camera.cameraType == CameraType.SceneView ? 2 : 1;
				var depthIndex = camera.cameraType == CameraType.SceneView ? 1 : -1;

				AddAttachment(cameraTargetDescriptor, camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture, false);

				// For sceneView, take the first depth sample for for gizmos, wireframe, etc.
				if (camera.cameraType == CameraType.SceneView)
					AddAttachment(cameraDepthDescriptor, camera.targetTexture, false);

				// Can't really be a subpass since it requires resolving or flipping
				command.SetGlobalTexture("CameraTarget", cameraTarget);

				if (camera.targetTexture == null)
					command.EnableShaderKeyword("FLIP");

				if (camera.cameraType == CameraType.SceneView)
				{
					command.SetGlobalTexture("CameraDepth", cameraDepth);

					switch (asset.Samples)
					{
						case 1:
							command.EnableShaderKeyword("DEPTH");
							break;
						case 2:
							command.EnableShaderKeyword("DEPTH_MSAA_2");
							break;
						case 4:
							command.EnableShaderKeyword("DEPTH_MSAA_4");
							break;
						case 8:
							command.EnableShaderKeyword("DEPTH_MSAA_8");
							break;
					}
				}

				command.SetGlobalVector("ViewSize", new(camera.pixelWidth, camera.pixelHeight));
				command.SetWireframe(false);

				var colorOutputs = new AttachmentIndexArray(1);
				colorOutputs[0] = 0;
				subpasses.Add(new() { colorOutputs = colorOutputs });
				BeginRenderPass(new(camera.pixelWidth, camera.pixelHeight), depthIndex, 1, "Blit Pass");
				command.DrawProcedural(Matrix4x4.identity, blitMaterial, 0, MeshTopology.Triangles, 3);
				command.EndRenderPass();

				if (camera.targetTexture == null)
					command.DisableShaderKeyword("FLIP");

				if (camera.cameraType == CameraType.SceneView)
				{
					switch (asset.Samples)
					{
						case 1:
							command.DisableShaderKeyword("DEPTH");
							break;
						case 2:
							command.DisableShaderKeyword("DEPTH_MSAA_2");
							break;
						case 4:
							command.DisableShaderKeyword("DEPTH_MSAA_4");
							break;
						case 8:
							command.DisableShaderKeyword("DEPTH_MSAA_8");
							break;
					}
				}
			}

			if (Handles.ShouldRenderGizmos())
			{
				command.DrawRendererList(context.CreateGizmoRendererList(camera, GizmoSubset.PreImageEffects));
				command.DrawRendererList(context.CreateGizmoRendererList(camera, GizmoSubset.PostImageEffects));
			}

			{
				// Editor-only, to make selection-wireframe render properly, we need to setup the same camera properties again but with a flipped matrix
				var tanHalfFovY = Tan(0.5f * Radians(camera.fieldOfView));
				var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);
				var worldToView = Float4x4.WorldToLocal(0.0f, camera.transform.WorldRotation());
				var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, camera.nearClipPlane, camera.farClipPlane, 0, true);
				var worldToClip = viewToClip.Mul(worldToView);
				command.SetGlobalMatrix("WorldToClip", worldToClip);

				// For selection outline to work, we need to also set this builtin matrix
				var worldToViewAbs = Float4x4.WorldToLocal(camera.transform.WorldPosition(), camera.transform.WorldRotation());
				var worldToClipAbs = viewToClip.Mul(worldToViewAbs);
				command.SetGlobalMatrix("unity_MatrixVP", worldToClipAbs);
			}

			command.DrawRendererList(context.CreateWireOverlayRendererList(camera));
		}

		// Set matrices for UI rendering
		var overlayMatrix = Float4x4.OrthoReverseZ(-Screen.width / 2f, Screen.width / 2f, -Screen.height / 2f, Screen.height / 2f, 0, 1);
		command.SetGlobalMatrix("UiOverlayMatrix", overlayMatrix);

		context.ExecuteCommandBuffer(command);
		command.Clear();

		if (context.SubmitForRenderPassValidation())
			context.Submit();
	}
}
