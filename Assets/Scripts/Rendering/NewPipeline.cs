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

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		command = new() { name = "Render Frame" };
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
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
			var msaaCameraTarget = Shader.PropertyToID("MsaaCameraTarget");

			// Pass 0
			{
				// Setup view data
				var tanHalfFovY = Tan(0.5f * Radians(camera.fieldOfView));
				var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);
				var worldToView = Float4x4.WorldToLocal(0.0f, camera.transform.WorldRotation());
				var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, camera.nearClipPlane, camera.farClipPlane);
				var worldToClip = viewToClip.Mul(worldToView);

				command.SetGlobalMatrix("WorldToView", worldToView);
				command.SetGlobalMatrix("WorldToClip", worldToClip);
				command.SetGlobalVector("ViewPosition", camera.transform.position);
				command.SetGlobalVector("SunDirection", -RenderSettings.sun.transform.forward);
				command.SetGlobalVector("SunColor", RenderSettings.sun.color.linear);
				command.SetGlobalVector("AmbientLight", RenderSettings.ambientLight.linear);

				var fogEnabled = RenderSettings.fog;
#if UNITY_EDITOR
				if (SceneView.currentDrawingSceneView != null)
					fogEnabled &= SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

				var fogStart = fogEnabled ? RenderSettings.fogStartDistance : 0;
				var fogEnd = fogEnabled ? RenderSettings.fogEndDistance : 0;
				var fogScale = fogEnabled ? 1 / (fogEnd - fogStart) : 0;
				var fogOffset = fogEnabled ? fogStart / (fogStart - fogEnd) : 0;

				command.SetGlobalFloat("FogScale", fogScale);
				command.SetGlobalFloat("FogOffset", fogOffset);
				command.SetGlobalVector("FogColor", RenderSettings.fogColor.linear);

				var attachments = new NativeArray<AttachmentDescriptor>(2, Allocator.Temp);

				if (camera.cameraType == CameraType.SceneView)
				{
					command.GetTemporaryRT(cameraDepth, new RenderTextureDescriptor
					{
						width = camera.pixelWidth,
						height = camera.pixelHeight,
						volumeDepth = 1,
						msaaSamples = asset.Samples,
						graphicsFormat = GraphicsFormat.None,
						depthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt,
						mipCount = 1,
						dimension = TextureDimension.Tex2D,
						shadowSamplingMode = ShadowSamplingMode.None,
						vrUsage = VRTextureUsage.None,
						enableRandomWrite = false,
						stencilFormat = GraphicsFormat.None,
						useMipMap = false,
						bindMS = asset.Samples > 1
					});
				}

				// For scene view we also need depth for wireframe (Note that msaa depth can not be resolved automatically so we need to store and manually resolve later)
				attachments[0] = new AttachmentDescriptor
				{
					loadAction = RenderBufferLoadAction.Clear,
					storeAction = camera.cameraType == CameraType.SceneView ? RenderBufferStoreAction.Store : RenderBufferStoreAction.DontCare,
					graphicsFormat = GraphicsFormat.D32_SFloat_S8_UInt,
					loadStoreTarget = camera.cameraType == CameraType.SceneView ? cameraDepth : BuiltinRenderTextureType.None,
					resolveTarget = BuiltinRenderTextureType.None,
					clearColor = new Color(0, 0, 0, 0),
					clearDepth = 1.0f,
					clearStencil = 1u
				};

				var targetFormat = camera.targetTexture == null ? GraphicsFormat.R8G8B8A8_SRGB : camera.targetTexture.graphicsFormat;
				if (camera.targetTexture != null)
				{
					var targetDesc = camera.targetTexture.descriptor;
					command.GetTemporaryRT(cameraTarget, new RenderTextureDescriptor
					{
						width = targetDesc.width,
						height = targetDesc.height,
						volumeDepth = targetDesc.volumeDepth,
						msaaSamples = asset.Samples,
						graphicsFormat = targetDesc.graphicsFormat,
						depthStencilFormat = targetDesc.depthStencilFormat,
						mipCount = targetDesc.mipCount,
						dimension = targetDesc.dimension,
						shadowSamplingMode = targetDesc.shadowSamplingMode,
						vrUsage = targetDesc.vrUsage,
						enableRandomWrite = targetDesc.enableRandomWrite,
						stencilFormat = targetDesc.stencilFormat,
						useMipMap = targetDesc.useMipMap,
					});
				}
				else if (asset.Samples > 1)
				{
					command.GetTemporaryRT(msaaCameraTarget, new RenderTextureDescriptor
					{
						width = camera.pixelWidth,
						height = camera.pixelHeight,
						volumeDepth = 1,
						msaaSamples = 1,
						graphicsFormat = targetFormat,
						depthStencilFormat = GraphicsFormat.None,
						mipCount = 1,
						dimension = TextureDimension.Tex2D,
						shadowSamplingMode = ShadowSamplingMode.None,
						vrUsage = VRTextureUsage.None,
						enableRandomWrite = false,
						stencilFormat = GraphicsFormat.None,
						useMipMap = false,
					});
				}

				// Color 
				attachments[1] = new AttachmentDescriptor
				{
					loadAction = RenderBufferLoadAction.Clear,
					storeAction = asset.Samples == 1 ? RenderBufferStoreAction.Store : RenderBufferStoreAction.Resolve,
					graphicsFormat = targetFormat,
					loadStoreTarget = asset.Samples == 1 ? (camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : cameraTarget) : BuiltinRenderTextureType.None,
					resolveTarget = asset.Samples == 1 ? BuiltinRenderTextureType.None : (camera.targetTexture == null ? msaaCameraTarget : cameraTarget),
					clearColor = RenderSettings.fogColor.linear,
					clearDepth = 1.0f,
					clearStencil = 1u
				};

				var subPasses = new NativeArray<SubPassDescriptor>(1, Allocator.Temp);
				var colorOutputs = new AttachmentIndexArray(1);
				colorOutputs[0] = 1;
				subPasses[0] = new SubPassDescriptor() { colorOutputs = colorOutputs };

				command.BeginRenderPass(camera.pixelWidth, camera.pixelHeight, asset.Samples, attachments, 0, subPasses);

				var shaderPassName = new ShaderTagId("Forward");
				var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
				var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

				var drawSettings = new DrawingSettings(shaderPassName, sortingSettings) { enableInstancing = true, perObjectData = PerObjectData.None };
				var rendererListParams = new RendererListParams(cullingResults, drawSettings, filteringSettings);
				var opaqueRendererList = context.CreateRendererList(ref rendererListParams);

				// This is basically only required if the camera is rendering to a no-resolved MSAA texture, which is the case for depth in the scene view..
				if (asset.Samples > 1 && camera.targetTexture == null)
					command.SetInvertCulling(true);

				command.DrawRendererList(opaqueRendererList);

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

				var attachments = new NativeArray<AttachmentDescriptor>(attachmentCount, Allocator.Temp);
				attachments[0] = new AttachmentDescriptor
				{
					loadAction = RenderBufferLoadAction.DontCare,
					storeAction = RenderBufferStoreAction.Store,
					graphicsFormat = camera.targetTexture == null ? GraphicsFormat.R8G8B8A8_SRGB : camera.targetTexture.graphicsFormat,
					loadStoreTarget = camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture,
					resolveTarget = BuiltinRenderTextureType.None,
					clearColor = new Color(0, 0, 0, 0),
					clearDepth = 1.0f,
					clearStencil = 1u
				};

				if (camera.cameraType == CameraType.SceneView)
				{
					attachments[1] = new AttachmentDescriptor
					{
						loadAction = RenderBufferLoadAction.DontCare,
						storeAction = RenderBufferStoreAction.Store,
						graphicsFormat = camera.targetTexture.depthStencilFormat,
						loadStoreTarget = camera.targetTexture,
						resolveTarget = BuiltinRenderTextureType.None,
						clearColor = new Color(0, 0, 0, 0),
						clearDepth = 1.0f,
						clearStencil = 1u
					};
				}

				var subPasses = new NativeArray<SubPassDescriptor>(1, Allocator.Temp);
				var colorOutputs = new AttachmentIndexArray(1);
				colorOutputs[0] = 0;
				subPasses[0] = new SubPassDescriptor() { colorOutputs = colorOutputs };

				command.SetGlobalTexture("CameraTarget", camera.targetTexture == null ? msaaCameraTarget : cameraTarget);
				command.BeginRenderPass(camera.pixelWidth, camera.pixelHeight, 1, attachments, depthIndex, subPasses);

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

		context.ExecuteCommandBuffer(command);
		command.Clear();

		if (context.SubmitForRenderPassValidation())
			context.Submit();
	}
}
