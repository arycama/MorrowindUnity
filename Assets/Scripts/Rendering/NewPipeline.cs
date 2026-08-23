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

	private readonly List<RenderTargetInfo> targets = new();
	private readonly List<RenderTargetIdentifier> resources = new();
	private readonly List<RenderTargetIdentifier> importedResources = new();
	private readonly List<IRenderPass> renderPasses = new();

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		command = new() { name = "Render Frame" };
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
	}

	private TextureHandle GetTexture(RenderTargetDescriptor descriptor, bool dontResolve)
	{
		targets.Add(new(descriptor, -1, -1, -1, dontResolve, false));
		return new(targets.Count - 1);
	}

	private RenderPass<T> AddRenderPass<T>(string name, bool invertCulling, T data, Action<CommandBuffer, T> render)
	{
		var index = renderPasses.Count;
		var renderPass = new RenderPass<T>(name, index, invertCulling, data, render);
		renderPasses.Add(renderPass);
		return renderPass;
	}

	public void ReadTexture(IRenderPass renderPass, TextureHandle handle, int propertyId)
	{
		// Update the last read index. Since rendergraph executes serially, this will always be the last-read pass
		var target = targets[handle.index];
		target.lastReadIndex = renderPass.Index;
		targets[handle.index] = target;

		renderPass.Inputs.Add((handle, propertyId));
	}

	public void WriteTexture(IRenderPass renderPass, TextureHandle handle, bool dontResolve)
	{
		var target = targets[handle.index];

		// Track the first pass this target is written to so we know when to clear. This also allows allocation to be skipped for textures that are never written to
		if (target.firstWriteIndex == -1)
			target.firstWriteIndex = renderPass.Index;

		// Writes are also treataed as reads for the purposes of resource tracking, this stops a texture from being discarded as a future write (Eg a 2nd pass to the same RT) would not be treated as a read otherwise, and would cause the texture to be discarded after the first pass
		target.lastReadIndex = renderPass.Index;
		targets[handle.index] = target;

		renderPass.Outputs.Add((handle, dontResolve));
	}

	private void ExportTexture(TextureHandle handle, RenderTargetIdentifier id)
	{
		var resourceIndex = importedResources.Count;
		importedResources.Add(id);

		var target = targets[handle.index];
		target.resourceIndex = resourceIndex;
		target.isImported = true;
		targets[handle.index] = target;
	}

	protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
	{
		command.Clear();
		targets.Clear();
		resources.Clear();
		importedResources.Clear();
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

			var fogEnabled = RenderSettings.fog;
#if UNITY_EDITOR
			if (SceneView.currentDrawingSceneView != null)
				fogEnabled &= SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif


			var setViewData = AddRenderPass("Set View Data", false, (camera, fogEnabled), static (command, data) =>
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
				command.SetGlobalVector("ViewSize", new(data.camera.pixelWidth, data.camera.pixelHeight));
			});

			var depthFormat = camera.targetTexture == null ? GraphicsFormat.D32_SFloat_S8_UInt : camera.targetTexture.depthStencilFormat;
			var cameraDepth = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), depthFormat, asset.Samples, true), true);

			// TODO: This should also account for HDR
			var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
			var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;
			var cameraColor = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), targetFormat, asset.Samples, true, RenderSettings.fogColor.linear), false);

			// This is only required if the camera is rendering to a no-resolved MSAA texture, which is the case for depth in the scene view..
			var invertCulling = asset.Samples > 1 && camera.targetTexture == null;

			var opaqueRendererList = context.CreateRendererList(new(new ShaderTagId("Forward"), cullingResults, camera) { renderQueueRange = RenderQueueRange.opaque, sortingCriteria = SortingCriteria.CommonOpaque });
			var renderForwardOpaque = AddRenderPass("Render Forward Opaque", invertCulling, opaqueRendererList, static (command, opaqueRendererList) =>
			{
				command.DrawRendererList(opaqueRendererList);
			});
			{
				renderForwardOpaque.SetRenderPassParams(new(camera.pixelWidth, camera.pixelHeight), asset.Samples);
				WriteTexture(renderForwardOpaque, cameraDepth, true);
				WriteTexture(renderForwardOpaque, cameraColor, false);
			}

			var transparentRendererList = context.CreateRendererList(new(new ShaderTagId("Forward"), cullingResults, camera) { renderQueueRange = RenderQueueRange.transparent, sortingCriteria = SortingCriteria.CommonTransparent });
			var renderForwardTransparent = AddRenderPass("Render Forward Transparent", invertCulling, (transparentRendererList, invertCulling), static (command, data) =>
			{
				command.DrawRendererList(data.transparentRendererList);
			});
			{
				renderForwardTransparent.SetRenderPassParams(new(camera.pixelWidth, camera.pixelHeight), asset.Samples);
				WriteTexture(renderForwardTransparent, cameraDepth, true);
				WriteTexture(renderForwardTransparent, cameraColor, false);
			}

			// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
			// TODO: Check for hardware msaa backbuffer resolve support
			var renderToBackbuffer = asset.Samples == 1 && camera.targetTexture == null;
			if (renderToBackbuffer)
			{
				ExportTexture(cameraColor, BuiltinRenderTextureType.CameraTarget);
			}
			else
			{
				var requiresSceneDepth = camera.cameraType == CameraType.SceneView;
				var finalBlitSetup = AddRenderPass("Final Blit Setup", false, (camera, asset, requiresSceneDepth), static (command, data) =>
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

					command.SetWireframe(false);
				});

				// Pass 2: Final blit/resolve if needed
				var finalBlitPass = AddRenderPass("Final Blit", false, (blitMaterial, camera, asset, requiresSceneDepth), static (command, data) =>
				{
					command.DrawProcedural(Matrix4x4.identity, data.blitMaterial, 0, MeshTopology.Triangles, 3);

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
				{
					ReadTexture(finalBlitPass, cameraColor, Shader.PropertyToID("CameraColor"));
					var backbufferColor = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), targetFormat), false);
					WriteTexture(finalBlitPass, backbufferColor, false);
					ExportTexture(backbufferColor, camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : camera.targetTexture);

					// For sceneView, take the first depth sample for for gizmos, wireframe, etc.
					if (requiresSceneDepth)
					{
						ReadTexture(finalBlitPass, cameraDepth, Shader.PropertyToID("CameraDepth"));

						var sceneDepth = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), depthFormat), false);
						WriteTexture(finalBlitPass, sceneDepth, false);
						ExportTexture(sceneDepth, camera.targetTexture);
					}

					finalBlitPass.SetRenderPassParams(new(camera.pixelWidth, camera.pixelHeight), 1);
				}
			}

			// Render UI. For now this is done automatically so it only sets matrices for UI rendering
			var setUiMatrices = AddRenderPass("Set UI Matrices", false, 0, static (command, data) =>
			{
				var overlayMatrix = Float4x4.OrthoReverseZ(-Screen.width / 2f, Screen.width / 2f, -Screen.height / 2f, Screen.height / 2f, 0, 1);
				command.SetGlobalMatrix("UiOverlayMatrix", overlayMatrix);
			});

			// Render gizmos
			if (Handles.ShouldRenderGizmos())
			{
				var preImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PreImageEffects);
				var postImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PostImageEffects);

				var renderGizmos = AddRenderPass("Render Gizmos", false, (preImageEffectsRenderList, postImageEffectsRenderList), static (command, data) =>
				{
					// Note that gizmos use their own matrix logic which we can't override
					command.DrawRendererList(data.preImageEffectsRenderList);
					command.DrawRendererList(data.postImageEffectsRenderList);
				});
			}

			// Render wireframe
			var wireframeRendererList = context.CreateWireOverlayRendererList(camera);
			var renderWireframe = AddRenderPass("Render Gizmos", false, (camera, wireframeRendererList), static (command, data) =>
			{
				// Editor-only, to make selection-wireframe render properly, we need to setup the same camera properties again but with a flipped matrix
				var tanHalfFovY = Tan(0.5f * Radians(data.camera.fieldOfView));
				var tanHalfFov = new Float2(tanHalfFovY * data.camera.aspect, tanHalfFovY);
				var worldToView = Float4x4.WorldToLocal(0.0f, data.camera.transform.WorldRotation());
				var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, data.camera.nearClipPlane, data.camera.farClipPlane, 0, true);
				var worldToClip = viewToClip.Mul(worldToView);

				// For selection outline to work, we need to also set this builtin matrix
				var worldToViewAbs = Float4x4.WorldToLocal(data.camera.transform.WorldPosition(), data.camera.transform.WorldRotation());
				var worldToClipAbs = viewToClip.Mul(worldToViewAbs);

				command.SetGlobalMatrix("WorldToClip", worldToClip);
				command.SetGlobalMatrix("unity_MatrixVP", worldToClipAbs);

				// Note this uses whatever matrices are previously set, so we need to set the flipped version first. It also needs to be rendered before gizmos as they may override matrices
				command.DrawRendererList(data.wireframeRendererList);
			});
		}

		// Process the rendergraph
		// TODO: Can we use spans
		var attachments = new NativeList<AttachmentDescriptor>(8, Allocator.Temp);
		var subpasses = new NativeList<SubPassDescriptor>(8, Allocator.Temp);
		var colorOutputs = new NativeList<int>(8, Allocator.Temp);
		var depthIndex = -1;

		for (var i = 0; i < renderPasses.Count; i++)
		{
			var renderPass = renderPasses[i];
			foreach (var output in renderPass.Outputs)
			{
				var target = targets[output.handle.index];
				var attachmentDescriptor = new AttachmentDescriptor
				{
					graphicsFormat = target.descriptor.format
				};

				// Clear the target on the first write if needed, or just leave contents uninitialized. If this is not the first write, then it will default to a load action.
				if (i == target.firstWriteIndex)
				{
					if (target.descriptor.clear)
					{
						attachmentDescriptor.loadAction = RenderBufferLoadAction.Clear;
						attachmentDescriptor.clearColor = target.descriptor.clearColor;
						attachmentDescriptor.clearDepth = target.descriptor.clearDepth;
						attachmentDescriptor.clearStencil = target.descriptor.clearStencil;
					}
					else
						attachmentDescriptor.loadAction = RenderBufferLoadAction.DontCare;
				}

				// If this is the last time this target is read, it does not need to be stored. Otherwise it needs to be stored or resolved depending on sample count
				var requiresResolve = !output.dontResolve && target.descriptor.samples > 1;

				// TODO: Can this be combined with the 2nd branch at all
				if (target.isImported)
				{
					// Imported targets are always resolved or stored
					var resource = importedResources[target.resourceIndex];
					if (requiresResolve)
					{
						attachmentDescriptor.resolveTarget = resource;
						attachmentDescriptor.storeAction = RenderBufferStoreAction.Resolve;
					}
					else
					{
						attachmentDescriptor.loadStoreTarget = resource;
					}
				}
				else
				{
					RenderTargetIdentifier resource;
					var requiresLoad = i > target.firstWriteIndex;
					var requiresStore = i < target.lastReadIndex;

					if (requiresLoad)
					{
						// If this target has already been written to, use it's current resource
						resource = resources[target.resourceIndex];

						if(requiresStore)
						{
							if (requiresResolve)
							{
								attachmentDescriptor.resolveTarget = resource;
								attachmentDescriptor.storeAction = RenderBufferStoreAction.Resolve;
							}
							else
							{
								attachmentDescriptor.loadStoreTarget = resource;
							}
						}
						else
						{
							attachmentDescriptor.loadStoreTarget = resource;
							attachmentDescriptor.storeAction = RenderBufferStoreAction.DontCare;
						}
					}
					else if (requiresStore)
					{
						// Dynamic targets only need to be stored if they are read in a later renderpass
						target.resourceIndex = resources.Count;
						command.GetTemporaryRT(target.resourceIndex, target.descriptor.GetDescriptor(target.dontResolve));
						resource = target.resourceIndex;
						resources.Add(resource);
						targets[output.handle.index] = target;

						if (requiresResolve)
						{
							attachmentDescriptor.resolveTarget = resource;
							attachmentDescriptor.storeAction = RenderBufferStoreAction.Resolve;
						}
						else
						{
							attachmentDescriptor.loadStoreTarget = resource;
						}
					}
					else
					{
						attachmentDescriptor.storeAction = RenderBufferStoreAction.DontCare;
					}
				}

				var index = attachments.Length;
				attachments.Add(attachmentDescriptor);

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

			if (renderPass.IsNativeRenderPass)
			{
				if (renderPass.InvertCulling)
					command.SetInvertCulling(true);

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

			if (renderPass.IsNativeRenderPass)
			{
				command.EndRenderPass();
				if (renderPass.InvertCulling)
					command.SetInvertCulling(false);
			}
		}

		context.ExecuteCommandBuffer(command);

		if (context.SubmitForRenderPassValidation())
			context.Submit();
	}
}
