using System;
using System.Collections.Generic;
using System.Linq;
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

	// Renderpass
	private readonly NativeList<AttachmentDescriptor> attachments = new(8, Allocator.Persistent);
	private readonly NativeList<SubPassDescriptor> subpasses = new(8, Allocator.Persistent);
	private readonly NativeList<int> colorOutputs = new(8, Allocator.Persistent);
	private int depthIndex = -1;

	private readonly List<RenderTargetDescriptor> targetDescriptors = new();
	private readonly List<int> resourceIndices = new();
	private readonly List<RenderTargetIdentifier> resources = new();

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

	private TextureHandle GetTexture(RenderTargetDescriptor descriptor)
	{
		targetDescriptors.Add(descriptor);
		resourceIndices.Add(-1);
		return new(targetDescriptors.Count - 1);
	}

	private void WriteTexture(TextureHandle handle, bool dontResolve = false)
	{
		var descriptor = targetDescriptors[handle.index];
		var resourceIndex = resourceIndices[handle.index];
		var hasTarget = resourceIndex != -1;
		var requiresResolve = hasTarget && !dontResolve && descriptor.samples > 1;

		attachments.Add(new AttachmentDescriptor
		{
			loadAction = descriptor.clear ? RenderBufferLoadAction.Clear : RenderBufferLoadAction.DontCare, // TODO: Support load, if contents have already been written to previously
			storeAction = !hasTarget ? RenderBufferStoreAction.DontCare : (requiresResolve ? RenderBufferStoreAction.Resolve : RenderBufferStoreAction.Store), // TODO: Only store if result is read later
			graphicsFormat = descriptor.format,
			loadStoreTarget = !hasTarget || requiresResolve ? BuiltinRenderTextureType.None : resources[resourceIndex], // TODO: Only set if target is read later
			resolveTarget = requiresResolve ? resources[resourceIndex] : BuiltinRenderTextureType.None,
			clearColor = descriptor.clearColor,
			clearDepth = descriptor.clearDepth,
			clearStencil = descriptor.clearStencil
		});

		var index = attachments.Length - 1;
		switch(descriptor.format)
		{
			case GraphicsFormat.D16_UNorm:
			case GraphicsFormat.D24_UNorm:
			case GraphicsFormat.D32_SFloat:
			case GraphicsFormat.D16_UNorm_S8_UInt:
			case GraphicsFormat.D24_UNorm_S8_UInt:
			case GraphicsFormat.D32_SFloat_S8_UInt:
				depthIndex = index;
				break;
			default:
				colorOutputs.Add(index);
				break;
		}
	}

	private void BeginRenderPass(Int2 size, int depthBufferIndex, int samples, string name)
	{
		subpasses.Add(new() { colorOutputs = new(colorOutputs.AsArray()) });
		colorOutputs.Clear();

		Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(name)];
		_ = Encoding.UTF8.GetBytes(name, debugNameUtf8);

		command.BeginRenderPass(size.x, size.y, samples, attachments.AsArray(), depthBufferIndex, subpasses.AsArray(), debugNameUtf8);
		subpasses.Clear();
		attachments.Clear();
	}

	private RenderPass<T> AddRenderPass<T>(T data, Action<CommandBuffer, T> render)
	{
		return new RenderPass<T>(data, render);
	}

	private void OutputTexture(TextureHandle handle, RenderTargetIdentifier id)
	{
		resources.Add(id);
		resourceIndices[handle.index] = resources.Count - 1;
	}

	private void OutputTexture(int id, TextureHandle handle, bool resolve)
	{
		var descriptor = targetDescriptors[handle.index];
		command.GetTemporaryRT(id, GetRenderTextureDescriptor(descriptor, resolve));
		OutputTexture(handle, id);
	}

	private void ReadTexture(string propertyName, TextureHandle handle, CommandBuffer command)
	{
		var resourceIndex = resourceIndices[handle.index];
		var resource = resources[resourceIndex];
		command.SetGlobalTexture(propertyName, resource);
	}

	protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
	{
		command.Clear();
		targetDescriptors.Clear();
		resourceIndices.Clear();
		resources.Clear();

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

			// Pass 0: Setup view data
			var setViewData = AddRenderPass(0, (command, data) => { });
			{
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
			}

			var cameraTargetId = Shader.PropertyToID("CameraTarget");
			var cameraDepthId = Shader.PropertyToID("CameraDepth");

			// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
			// TODO: Check for hardware msaa backbuffer resolve support
			var renderToBackbuffer = asset.Samples == 1 && camera.targetTexture == null;

			// TODO: This should also account for HDR
			var depthFormat = camera.targetTexture == null ? GraphicsFormat.D32_SFloat_S8_UInt : camera.targetTexture.depthStencilFormat;

			var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
			var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;

			var cameraDepth = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), depthFormat, asset.Samples, true));
			var cameraTarget = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), targetFormat, asset.Samples, true, RenderSettings.fogColor.linear));

			// Pass 1: Render forward
			var renderForward = AddRenderPass(0, (command, data) => { });
			{
				// For scene view we need depth for wireframe (Note that msaa depth can not be resolved automatically so we need to store and manually resolve later)
				if (camera.cameraType == CameraType.SceneView)
					OutputTexture(cameraDepthId, cameraDepth, false);

				if (!renderToBackbuffer)
					OutputTexture(cameraTargetId, cameraTarget, true);
				else
					OutputTexture(cameraTarget, BuiltinRenderTextureType.CameraTarget);

				WriteTexture(cameraDepth, true);
				WriteTexture(cameraTarget);

				renderForward.WriteTexture(cameraDepth);
				renderForward.WriteTexture(cameraTarget);
				BeginRenderPass(new(camera.pixelWidth, camera.pixelHeight), 0, asset.Samples, "Base Pass");

				// This is basically only required if the camera is rendering to a no-resolved MSAA texture, which is the case for depth in the scene view..
				if (asset.Samples > 1 && camera.targetTexture == null)
					command.SetInvertCulling(true);

				command.DrawRendererList(context.CreateRendererList(new(new ShaderTagId("Forward"), cullingResults, camera) { renderQueueRange = RenderQueueRange.opaque }));

				if (asset.Samples > 1 && camera.targetTexture == null)
					command.SetInvertCulling(false);

				command.EndRenderPass();
			}

			if (!renderToBackbuffer)
			{
				// Pass 2: Final blit/resolve if needed
				var finalBlit = AddRenderPass(0, (command, data) => { });
				var backbufferColor = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), targetFormat));

				// TODO: Remove
				var attachmentCount = camera.cameraType == CameraType.SceneView ? 2 : 1;
				var depthIndex = camera.cameraType == CameraType.SceneView ? 1 : -1;

				OutputTexture(backbufferColor, camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture);
				WriteTexture(backbufferColor);
				finalBlit.WriteTexture(backbufferColor);

				// For sceneView, take the first depth sample for for gizmos, wireframe, etc.
				if (camera.cameraType == CameraType.SceneView)
				{
					var sceneDepth = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), depthFormat));
					OutputTexture(sceneDepth, camera.targetTexture);
					WriteTexture(sceneDepth);
					finalBlit.WriteTexture(sceneDepth);
				}

				// Can't really be a subpass since it requires resolving or flipping
				ReadTexture("CameraTarget", cameraTarget, command);

				if (camera.cameraType == CameraType.SceneView)
					ReadTexture("CameraDepth", cameraDepth, command);

				if (camera.targetTexture == null)
					command.EnableShaderKeyword("FLIP");

				if (camera.cameraType == CameraType.SceneView)
				{
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

			// Pass 3, render gizmos wireframe (editor-only
			{
				var renderGizmos = AddRenderPass(0, (command, data) => { });

				if (Handles.ShouldRenderGizmos())
				{
					command.DrawRendererList(context.CreateGizmoRendererList(camera, GizmoSubset.PreImageEffects));
					command.DrawRendererList(context.CreateGizmoRendererList(camera, GizmoSubset.PostImageEffects));
				}

				command.DrawRendererList(context.CreateWireOverlayRendererList(camera));

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
		}

		// Final pass: Set matrices for UI rendering
		var setUiMatrices = AddRenderPass(0, (command, data) => { });

		var overlayMatrix = Float4x4.OrthoReverseZ(-Screen.width / 2f, Screen.width / 2f, -Screen.height / 2f, Screen.height / 2f, 0, 1);
		command.SetGlobalMatrix("UiOverlayMatrix", overlayMatrix);

		context.ExecuteCommandBuffer(command);

		if (context.SubmitForRenderPassValidation())
			context.Submit();
	}
}
