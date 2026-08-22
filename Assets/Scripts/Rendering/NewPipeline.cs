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

	private readonly List<(RenderTargetDescriptor descriptor, int resourceIndex, int firstWriteIndex, int lastReadIndex)> targets = new();
	private readonly List<RenderTargetIdentifier> resources = new();
	private readonly List<IRenderPass> renderPasses = new();

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		command = new() { name = "Render Frame" };
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
	}

	private TextureHandle GetTexture(RenderTargetDescriptor descriptor)
	{
		targets.Add((descriptor, -1, -1, -1));
		return new(targets.Count - 1);
	}

	private RenderPass<T> AddRenderPass<T>(T data, string name)
	{
		var index = renderPasses.Count;
		var renderPass = new RenderPass<T>(data, index, name);
		renderPasses.Add(renderPass);
		return renderPass;
	}

	public void ReadTexture(IRenderPass renderPass, TextureHandle handle, int propertyId)
	{
		var target = targets[handle.index];
		target.lastReadIndex = target.lastReadIndex == -1 ? renderPass.Index : Max(target.lastReadIndex, renderPass.Index); // TODO: Is there any situation where we wouldn't just directly assign, since the render graph executes in order
		targets[handle.index] = target;

		renderPass.Inputs.Add((handle, propertyId));
	}

	public void WriteTexture(IRenderPass renderPass, TextureHandle handle, bool dontResolve)
	{
		var target = targets[handle.index];
		target.firstWriteIndex = target.firstWriteIndex == -1 ? renderPass.Index : Min(target.firstWriteIndex, renderPass.Index); // TODO: Is there any situation where we wouldn't just directly assign, since the render graph executes in order
		targets[handle.index] = target;

		renderPass.Outputs.Add((handle, dontResolve));
	}

	private void OutputTexture(TextureHandle handle, RenderTargetIdentifier id)
	{
		resources.Add(id);

		var target = targets[handle.index];
		target.resourceIndex = resources.Count - 1;
		targets[handle.index] = target;
	}

	private void AllocateTexture(int id, TextureHandle handle, bool resolve)
	{
		var target = targets[handle.index];

		bool isColor = false, isDepth = false, isStencil = false;
		switch (target.descriptor.format)
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

		command.GetTemporaryRT(id, new RenderTextureDescriptor
		{
			width = target.descriptor.size.x,
			height = target.descriptor.size.y,
			volumeDepth = 1,
			msaaSamples = resolve ? 1 : target.descriptor.samples,
			graphicsFormat = isColor ? target.descriptor.format : GraphicsFormat.None,
			depthStencilFormat = isDepth ? target.descriptor.format : GraphicsFormat.None,
			mipCount = 1,
			dimension = TextureDimension.Tex2D,
			shadowSamplingMode = ShadowSamplingMode.None,
			vrUsage = VRTextureUsage.None,
			enableRandomWrite = false,
			stencilFormat = isStencil ? GraphicsFormat.R8_UInt : GraphicsFormat.None,
			useMipMap = false,
			bindMS = target.descriptor.samples > 1 && !resolve,
		});
	}

	protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
	{
		command.Clear();
		targets.Clear();
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

			var setViewData = AddRenderPass((camera, fogEnabled, asset), "Set View Data");
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
			var renderForward = AddRenderPass((asset, camera, rendererList), "Render Forward");
			{
				// For scene view we need depth for wireframe (Note that msaa depth can not be resolved automatically so we need to store and manually resolve later)
				if (requiresSceneDepth)
				{
					AllocateTexture(Shader.PropertyToID("CameraDepth"), cameraDepth, false);
					OutputTexture(cameraDepth, Shader.PropertyToID("CameraDepth"));
				}

				if (!renderToBackbuffer)
				{
					AllocateTexture(Shader.PropertyToID("CameraTarget"), cameraTarget, true);
					OutputTexture(cameraTarget, Shader.PropertyToID("CameraTarget"));
				}
				else
					OutputTexture(cameraTarget, BuiltinRenderTextureType.CameraTarget);

				WriteTexture(renderForward, cameraDepth, true);
				WriteTexture(renderForward, cameraTarget, false);

				renderForward.SetRenderPassParams(new(camera.pixelWidth, camera.pixelHeight), asset.Samples);
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
				var finalBlitSetup = AddRenderPass((camera, asset, requiresSceneDepth), "Final Blit Setup");
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
				var finalBlit = AddRenderPass((blitMaterial, camera, asset, requiresSceneDepth), "Final Blit");
				{
					var backbufferColor = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), targetFormat));
					OutputTexture(backbufferColor, camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture);
					WriteTexture(finalBlit, backbufferColor, false);

					// Can't really be a subpass since it requires resolving or flipping
					ReadTexture(finalBlit, cameraTarget, Shader.PropertyToID("CameraTarget"));

					// For sceneView, take the first depth sample for for gizmos, wireframe, etc.
					if (requiresSceneDepth)
					{
						var sceneDepth = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), depthFormat));
						OutputTexture(sceneDepth, camera.targetTexture);
						WriteTexture(finalBlit, sceneDepth, false);
						ReadTexture(finalBlit, cameraDepth, Shader.PropertyToID("CameraDepth"));
					}

					finalBlit.SetRenderPassParams(new(camera.pixelWidth, camera.pixelHeight), 1);
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

			var renderGizmos = AddRenderPass((camera, context, worldToClip, worldToClipAbs), "Render Gizmos");
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
		var setUiMatrices = AddRenderPass(0, "Set UI Matrices");
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
			foreach (var output in renderPass.Outputs)
			{
				var target = targets[output.handle.index];
				var hasTarget = target.resourceIndex != -1;
				var requiresResolve = hasTarget && !output.dontResolve && target.descriptor.samples > 1;

				attachments.Add(new AttachmentDescriptor
				{
					loadAction = target.descriptor.clear ? RenderBufferLoadAction.Clear : RenderBufferLoadAction.DontCare, // TODO: Support load, if contents have already been written to previously
					storeAction = !hasTarget ? RenderBufferStoreAction.DontCare : (requiresResolve ? RenderBufferStoreAction.Resolve : RenderBufferStoreAction.Store), // TODO: Only store if result is read later
					graphicsFormat = target.descriptor.format,
					loadStoreTarget = !hasTarget || requiresResolve ? BuiltinRenderTextureType.None : resources[target.resourceIndex], // TODO: Only set if target is read later
					resolveTarget = requiresResolve ? resources[target.resourceIndex] : BuiltinRenderTextureType.None,
					clearColor = target.descriptor.clearColor,
					clearDepth = target.descriptor.clearDepth,
					clearStencil = target.descriptor.clearStencil
				});

				var index = attachments.Length - 1;
				switch (target.descriptor.format)
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

			foreach (var input in renderPass.Inputs)
			{
				var target = targets[input.handle.index];
				var resource = resources[target.resourceIndex];
				command.SetGlobalTexture(input.propertyId, resource);
			}

			if (renderPass.BeginRenderPass)
			{
				subpasses.Add(new() { colorOutputs = new(colorOutputs.AsArray()) });
				colorOutputs.Clear();

				Span<byte> debugNameUtf8 = stackalloc byte[Encoding.UTF8.GetByteCount(renderPass.Name)];
				_ = Encoding.UTF8.GetBytes(renderPass.Name, debugNameUtf8);

				command.BeginRenderPass(renderPass.Size.x, renderPass.Size.y, renderPass.Samples, attachments.AsArray(), depthIndex, subpasses.AsArray(), debugNameUtf8);
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
