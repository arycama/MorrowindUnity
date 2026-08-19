using System;
using System.Collections.Generic;
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

	private static AttachmentDescriptor GetAttachmentDescriptor(RenderTargetDescriptor descriptor, RenderTargetIdentifier? target = null, bool resolve = true)
	{
		var requiresResolve = target.HasValue && resolve && descriptor.samples > 1;

		return new AttachmentDescriptor
		{
			loadAction = descriptor.clear ? RenderBufferLoadAction.Clear : RenderBufferLoadAction.DontCare, // TODO: Support load, if contents have already been written to previously
			storeAction = target == null ? RenderBufferStoreAction.DontCare : (requiresResolve ? RenderBufferStoreAction.Resolve : RenderBufferStoreAction.Store), // TODO: Only store if result is read later
			graphicsFormat = descriptor.format,
			loadStoreTarget = target == null || requiresResolve ? BuiltinRenderTextureType.None : target.Value, // TODO: Only set if target is read later
			resolveTarget = requiresResolve ? target.Value : BuiltinRenderTextureType.None, 
			clearColor = descriptor.clearColor,
			clearDepth = descriptor.clearDepth,
			clearStencil = descriptor.clearStencil
		};
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

				var fogStart = fogEnabled ? RenderSettings.fogStartDistance : 0;
				var fogEnd = fogEnabled ? RenderSettings.fogEndDistance : 0;
				var fogScale = fogEnabled ? 1 / (fogEnd - fogStart) : 0;
				var fogOffset = fogEnabled ? fogStart / (fogStart - fogEnd) : 0;

				command.SetGlobalMatrix("WorldToView", worldToView);
				command.SetGlobalMatrix("WorldToClip", worldToClip);
				command.SetGlobalVector("ViewPosition", camera.transform.position);
				command.SetGlobalVector("SunDirection", camera.transform.WorldRotation().InverseRotate(-RenderSettings.sun.transform.forward));
				command.SetGlobalVector("SunColor", RenderSettings.sun.color.linear);
				command.SetGlobalVector("AmbientLight", RenderSettings.ambientLight.linear);
				command.SetGlobalFloat("FogScale", fogScale);
				command.SetGlobalFloat("FogOffset", fogOffset);
				command.SetGlobalVector("FogColor", RenderSettings.fogColor.linear);

				var cameraDepthDescriptor = new RenderTargetDescriptor(new(camera.pixelWidth, camera.pixelHeight), GraphicsFormat.D32_SFloat_S8_UInt, samples: asset.Samples, clear: true);
				if (camera.cameraType == CameraType.SceneView)
					command.GetTemporaryRT(cameraDepth, cameraDepthDescriptor);

				// For scene view we also need depth for wireframe (Note that msaa depth can not be resolved automatically so we need to store and manually resolve later)
				// TODO: cameraDepth could be some thing set based on above.
				attachments.Add(GetAttachmentDescriptor(cameraDepthDescriptor, camera.cameraType == CameraType.SceneView ? cameraDepth : BuiltinRenderTextureType.None, false));

				// TODO: This should also account for HDR
				var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
				var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;
				var requiresIntermediateTexture = camera.targetTexture != null || asset.Samples > 1;
				var cameraTargetDescriptor = new RenderTargetDescriptor(new(camera.pixelWidth, camera.pixelHeight), targetFormat);
				if (requiresIntermediateTexture)
					command.GetTemporaryRT(cameraTarget, cameraTargetDescriptor);

				// Color 
				attachments.Add(new AttachmentDescriptor()
				{
					loadAction = RenderBufferLoadAction.Clear,
					storeAction = asset.Samples == 1 ? RenderBufferStoreAction.Store : RenderBufferStoreAction.Resolve,
					graphicsFormat = targetFormat,
					loadStoreTarget = asset.Samples == 1 ? (camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : cameraTarget) : BuiltinRenderTextureType.None,
					resolveTarget = asset.Samples == 1 ? BuiltinRenderTextureType.None : cameraTarget,
					clearColor = RenderSettings.fogColor.linear,
					clearDepth = 1.0f,
					clearStencil = 1u
				});

				var colorOutputs = new AttachmentIndexArray(1);
				colorOutputs[0] = 1;
				subpasses.Add(new() { colorOutputs = colorOutputs });
				command.BeginRenderPass(camera.pixelWidth, camera.pixelHeight, asset.Samples, attachments.AsArray(), 0, subpasses.AsArray());
				subpasses.Clear();
				attachments.Clear();

				// This is basically only required if the camera is rendering to a no-resolved MSAA texture, which is the case for depth in the scene view..
				if (asset.Samples > 1 && camera.targetTexture == null)
					command.SetInvertCulling(true);

				command.DrawRendererList(context.CreateRendererList(new(new ShaderTagId("Forward"), cullingResults, camera) { renderQueueRange = RenderQueueRange.opaque }));

				if (asset.Samples > 1 && camera.targetTexture == null)
					command.SetInvertCulling(false);

				command.EndRenderPass();
			}

			// Pass 1
			if (camera.targetTexture != null || asset.Samples > 1)
			{
				// Need to bind the camera's depth buffer 
				var attachmentCount = camera.cameraType == CameraType.SceneView ? 2 : 1;
				var depthIndex = camera.cameraType == CameraType.SceneView ? 1 : -1;

				attachments.Add(new AttachmentDescriptor
				{
					loadAction = RenderBufferLoadAction.DontCare,
					storeAction = RenderBufferStoreAction.Store,
					graphicsFormat = camera.targetTexture == null ? GraphicsFormat.R8G8B8A8_SRGB : camera.targetTexture.graphicsFormat,
					loadStoreTarget = camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture,
					resolveTarget = BuiltinRenderTextureType.None,
					clearColor = new Color(0, 0, 0, 0),
					clearDepth = 1.0f,
					clearStencil = 1u
				});

				if (camera.cameraType == CameraType.SceneView)
				{
					// For scene view we 'resolve' the depth (Just take the first sample) for gizmos, wireframe, etc.
					attachments.Add(new AttachmentDescriptor
					{
						loadAction = RenderBufferLoadAction.DontCare,
						storeAction = RenderBufferStoreAction.Store,
						graphicsFormat = camera.targetTexture.depthStencilFormat,
						loadStoreTarget = camera.targetTexture,
						resolveTarget = BuiltinRenderTextureType.None,
						clearColor = new Color(0, 0, 0, 0),
						clearDepth = 1.0f,
						clearStencil = 1u
					});
				}

				var colorOutputs = new AttachmentIndexArray(1);
				colorOutputs[0] = 0;
				subpasses.Add(new() { colorOutputs = colorOutputs });

				command.SetGlobalTexture("CameraTarget", cameraTarget);
				command.BeginRenderPass(camera.pixelWidth, camera.pixelHeight, 1, attachments.AsArray(), depthIndex, subpasses.AsArray());
				attachments.Clear();
				subpasses.Clear();

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
				command.DrawProcedural(Matrix4x4.identity, blitMaterial, 0, MeshTopology.Triangles, 3);

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

				command.EndRenderPass();
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
