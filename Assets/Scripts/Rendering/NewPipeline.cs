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

	private readonly List<RenderTargetDescriptor> targetDescriptors = new();
	private readonly List<int> resourceIndices = new();
	private readonly List<RenderTargetIdentifier> resources = new();

	private readonly List<IRenderPass> renderPasses = new();

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		command = new() { name = "Render Frame" };
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
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

	private RenderPass<T> AddRenderPass<T>(T data)
	{
		var renderPass = new RenderPass<T>(data);
		renderPasses.Add(renderPass);
		return renderPass;
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

	protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
	{
		command.Clear();
		targetDescriptors.Clear();
		resourceIndices.Clear();
		resources.Clear();
		renderPasses.Clear();

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
			var fogEnabled = RenderSettings.fog;
#if UNITY_EDITOR
			if (SceneView.currentDrawingSceneView != null)
				fogEnabled &= SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

			var setViewData = AddRenderPass((camera, fogEnabled, asset));
			{
				setViewData.SetRenderFunction(static (command, data) =>
				{
					var tanHalfFovY = Tan(0.5f * Radians(data.camera.fieldOfView));
					var tanHalfFov = new Float2(tanHalfFovY * data.camera.aspect, tanHalfFovY);
					var worldToView = Float4x4.WorldToLocal(0.0f, data.camera.transform.WorldRotation());
					var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, data.camera.nearClipPlane, data.camera.farClipPlane);
					var worldToClip = viewToClip.Mul(worldToView);

					command.SetGlobalMatrix("WorldToView", worldToView);
					command.SetGlobalMatrix("WorldToClip", worldToClip);
					command.SetGlobalVector("ViewPosition", data.camera.transform.position);
					command.SetGlobalVector("SunDirection", data.camera.transform.WorldRotation().InverseRotate(-RenderSettings.sun.transform.forward));
					command.SetGlobalVector("SunColor", RenderSettings.sun.color.linear);
					command.SetGlobalVector("AmbientLight", RenderSettings.ambientLight.linear);
					command.SetGlobalVector("FogColor", RenderSettings.fogColor.linear);
					command.SetGlobalFloat("FogScale", data.fogEnabled ? 1 / (RenderSettings.fogEndDistance - RenderSettings.fogStartDistance) : 0);
					command.SetGlobalFloat("FogOffset", data.fogEnabled ? RenderSettings.fogStartDistance / (RenderSettings.fogStartDistance - RenderSettings.fogEndDistance) : 0);

					// This is basically only required if the camera is rendering to a no-resolved MSAA texture, which is the case for depth in the scene view..
					if (data.asset.Samples > 1 && data.camera.targetTexture == null)
						command.SetInvertCulling(true);
				});
			}

			var requiresSceneDepth = camera.cameraType == CameraType.SceneView;
			var depthFormat = camera.targetTexture == null ? GraphicsFormat.D32_SFloat_S8_UInt : camera.targetTexture.depthStencilFormat;
			var cameraDepth = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), depthFormat, asset.Samples, true));

			// TODO: This should also account for HDR
			var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
			var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;
			var cameraTarget = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), targetFormat, asset.Samples, true, RenderSettings.fogColor.linear));

			// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
			// TODO: Check for hardware msaa backbuffer resolve support
			var renderToBackbuffer = asset.Samples == 1 && camera.targetTexture == null;

			// Pass 1: Render forward
			var rendererList = context.CreateRendererList(new(new ShaderTagId("Forward"), cullingResults, camera) { renderQueueRange = RenderQueueRange.opaque });
			var renderForward = AddRenderPass((asset, camera, rendererList));
			{
				// For scene view we need depth for wireframe (Note that msaa depth can not be resolved automatically so we need to store and manually resolve later)
				if (requiresSceneDepth)
					OutputTexture(Shader.PropertyToID("CameraDepth"), cameraDepth, false);

				if (!renderToBackbuffer)
					OutputTexture(Shader.PropertyToID("CameraTarget"), cameraTarget, true);
				else
					OutputTexture(cameraTarget, BuiltinRenderTextureType.CameraTarget);

				renderForward.WriteTexture(cameraDepth, true);
				renderForward.WriteTexture(cameraTarget, false);

				renderForward.SetRenderPassParams(new(camera.pixelWidth, camera.pixelHeight), asset.Samples, "Base Pass");
				renderForward.SetRenderFunction(static (command, data) =>
				{
					command.DrawRendererList(data.rendererList);

					command.EndRenderPass();

					if (data.asset.Samples > 1 && data.camera.targetTexture == null)
						command.SetInvertCulling(false);
				});
			}

			if (!renderToBackbuffer)
			{
				var finalBlitSetup = AddRenderPass((camera, asset, requiresSceneDepth));
				finalBlitSetup.SetRenderFunction(static (command, data) =>
				{
					if (data.camera.targetTexture == null)
						command.EnableShaderKeyword("FLIP");

					if (data.requiresSceneDepth)
					{
						switch (data.asset.Samples)
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

					command.SetGlobalVector("ViewSize", new(data.camera.pixelWidth, data.camera.pixelHeight));
					command.SetWireframe(false);
				});

				// Pass 2: Final blit/resolve if needed
				var finalBlit = AddRenderPass((blitMaterial, camera, asset, requiresSceneDepth));
				{
					var backbufferColor = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), targetFormat));
					OutputTexture(backbufferColor, camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture);
					finalBlit.WriteTexture(backbufferColor, false);

					// Can't really be a subpass since it requires resolving or flipping
					finalBlit.ReadTexture(cameraTarget, Shader.PropertyToID("CameraTarget"));

					// For sceneView, take the first depth sample for for gizmos, wireframe, etc.
					if (requiresSceneDepth)
					{
						var sceneDepth = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), depthFormat));
						OutputTexture(sceneDepth, camera.targetTexture);
						finalBlit.WriteTexture(sceneDepth, false);
						finalBlit.ReadTexture(cameraDepth, Shader.PropertyToID("CameraDepth"));
					}

					finalBlit.SetRenderPassParams(new(camera.pixelWidth, camera.pixelHeight), 1, "Blit Pass");
					finalBlit.SetRenderFunction(static (command, data) =>
					{
						command.DrawProcedural(Matrix4x4.identity, data.blitMaterial, 0, MeshTopology.Triangles, 3);
						command.EndRenderPass();

						if (data.camera.targetTexture == null)
							command.DisableShaderKeyword("FLIP");

						if (data.requiresSceneDepth)
						{
							switch (data.asset.Samples)
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
					});
				}
			}

			// Pass 3, render gizmos wireframe (editor-only
			// Editor-only, to make selection-wireframe render properly, we need to setup the same camera properties again but with a flipped matrix
			var tanHalfFovY = Tan(0.5f * Radians(camera.fieldOfView));
			var tanHalfFov = new Float2(tanHalfFovY * camera.aspect, tanHalfFovY);
			var worldToView = Float4x4.WorldToLocal(0.0f, camera.transform.WorldRotation());
			var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, camera.nearClipPlane, camera.farClipPlane, 0, true);
			var worldToClip = viewToClip.Mul(worldToView);

			// For selection outline to work, we need to also set this builtin matrix
			var worldToViewAbs = Float4x4.WorldToLocal(camera.transform.WorldPosition(), camera.transform.WorldRotation());
			var worldToClipAbs = viewToClip.Mul(worldToViewAbs);

			var renderGizmos = AddRenderPass((camera, context, worldToClip, worldToClipAbs));
			renderGizmos.SetRenderFunction(static (command, data) =>
			{
				command.SetGlobalMatrix("WorldToClip", data.worldToClip);
				command.SetGlobalMatrix("unity_MatrixVP", data.worldToClipAbs);

				if (Handles.ShouldRenderGizmos())
				{
					// Note that gizmos use their own matrix logic which we can't override
					command.DrawRendererList(data.context.CreateGizmoRendererList(data.camera, GizmoSubset.PreImageEffects));
					command.DrawRendererList(data.context.CreateGizmoRendererList(data.camera, GizmoSubset.PostImageEffects));
				}

				// Note this uses whatever matrices are previously set, so we need to set the flipped version first
				command.DrawRendererList(data.context.CreateWireOverlayRendererList(data.camera));
			});
		}

		// Final pass: Set matrices for UI rendering
		var setUiMatrices = AddRenderPass(0);
		setUiMatrices.SetRenderFunction(static (command, data) =>
		{
			var overlayMatrix = Float4x4.OrthoReverseZ(-Screen.width / 2f, Screen.width / 2f, -Screen.height / 2f, Screen.height / 2f, 0, 1);
			command.SetGlobalMatrix("UiOverlayMatrix", overlayMatrix);
		});

		var attachments = new NativeList<AttachmentDescriptor>(8, Allocator.Temp);
		var subpasses = new NativeList<SubPassDescriptor>(8, Allocator.Temp);
		var colorOutputs = new NativeList<int>(8, Allocator.Temp);
		var depthIndex = -1;

		foreach (var renderPass in renderPasses)
		{
			foreach (var output in renderPass.outputs)
			{
				var descriptor = targetDescriptors[output.handle.index];
				var resourceIndex = resourceIndices[output.handle.index];
				var hasTarget = resourceIndex != -1;
				var requiresResolve = hasTarget && !output.dontResolve && descriptor.samples > 1;

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
				switch (descriptor.format)
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

			foreach (var input in renderPass.inputs)
			{
				var resourceIndex = resourceIndices[input.handle.index];
				var resource = resources[resourceIndex];
				command.SetGlobalTexture(input.propertyId, resource);
			}

			if (renderPass.beginRenderPass)
			{
				subpasses.Add(new() { colorOutputs = new(colorOutputs.AsArray()) });
				colorOutputs.Clear();

				Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(renderPass.name)];
				_ = Encoding.UTF8.GetBytes(renderPass.name, debugNameUtf8);

				command.BeginRenderPass(renderPass.size.x, renderPass.size.y, renderPass.samples, attachments.AsArray(), depthIndex, subpasses.AsArray(), debugNameUtf8);
				subpasses.Clear();
				attachments.Clear();
				depthIndex = -1;
			}

			renderPass.Execute(command);
		}

		context.ExecuteCommandBuffer(command);

		if (context.SubmitForRenderPassValidation())
			context.Submit();
	}
}
