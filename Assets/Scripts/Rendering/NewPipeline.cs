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
	private readonly List<IRenderPass> renderPasses = new();

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		command = new() { name = "Render Frame" };
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
	}

	private TextureHandle GetTexture(RenderTargetDescriptor descriptor, bool dontResolve)
	{
		targets.Add(new(descriptor, -1, -1, -1, dontResolve));
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
		// Update the last read index. Since rendergraph executes serially, this will always be the last-read pass
		var target = targets[handle.index];
		target.lastReadIndex = renderPass.Index;
		targets[handle.index] = target;

		renderPass.Inputs.Add((handle, propertyId));
	}

	public void WriteTexture(IRenderPass renderPass, TextureHandle handle, bool dontResolve)
	{
		var target = targets[handle.index];

		// If this pass hasn't been written yet, mark this as the first pass. It's possible a target might be written in multiple passes so we want the first pass
		// We also mark it as 'read' since it might be used as a a transient depth buffer for example, which would otherwise be culled if its read is -1, but instead we only
		// cull if all pass inputs are never read
		// TODO: We could just do this in the pass where the texture is created
		if (target.firstWriteIndex == -1)
		{
			target.firstWriteIndex = renderPass.Index;
			target.lastReadIndex = renderPass.Index;
			targets[handle.index] = target;
		}

		renderPass.Outputs.Add((handle, dontResolve));
	}

	private void ExportTexture(TextureHandle handle, RenderTargetIdentifier id)
	{
		resources.Add(id);
		var target = targets[handle.index];
		target.resourceIndex = resources.Count - 1;
		targets[handle.index] = target;
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
					command.SetGlobalVector("ViewSize", new(data.camera.pixelWidth, data.camera.pixelHeight));

					// This is basically only required if the camera is rendering to a no-resolved MSAA texture, which is the case for depth in the scene view..
					if (data.asset.Samples > 1 && data.camera.targetTexture == null)
						command.SetInvertCulling(true);
				});
			}

			var depthFormat = camera.targetTexture == null ? GraphicsFormat.D32_SFloat_S8_UInt : camera.targetTexture.depthStencilFormat;
			var cameraDepth = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), depthFormat, asset.Samples, true), true);

			// TODO: This should also account for HDR
			var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
			var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;
			var cameraColor = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), targetFormat, asset.Samples, true, RenderSettings.fogColor.linear), false);

			// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
			// TODO: Check for hardware msaa backbuffer resolve support
			var renderToBackbuffer = asset.Samples == 1 && camera.targetTexture == null;

			var rendererList = context.CreateRendererList(new(new ShaderTagId("Forward"), cullingResults, camera) { renderQueueRange = RenderQueueRange.opaque });
			var renderForward = AddRenderPass((asset, camera, rendererList), "Render Forward");
			{
				WriteTexture(renderForward, cameraDepth, true);
				WriteTexture(renderForward, cameraColor, false);

				renderForward.SetRenderPassParams(new(camera.pixelWidth, camera.pixelHeight), asset.Samples);
				renderForward.SetRenderFunction(static (command, data) =>
				{
					command.DrawRendererList(data.rendererList);

					command.EndRenderPass();

					if (data.asset.Samples > 1 && data.camera.targetTexture == null)
						command.SetInvertCulling(false);
				});
			}

			if (renderToBackbuffer)
			{
				ExportTexture(cameraColor, BuiltinRenderTextureType.CameraTarget);
			}
			else
			{
				var requiresSceneDepth = camera.cameraType == CameraType.SceneView;
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

					command.SetWireframe(false);
				});

				// Pass 2: Final blit/resolve if needed
				var finalBlitPass = AddRenderPass((blitMaterial, camera, asset, requiresSceneDepth), "Final Blit");
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
					finalBlitPass.SetRenderFunction(static (command, data) =>
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

			// Render UI. For now this is done automatically so it only sets matrices for UI rendering
			var setUiMatrices = AddRenderPass(0, "Set UI Matrices");
			setUiMatrices.SetRenderFunction(static (command, data) =>
			{
				var overlayMatrix = Float4x4.OrthoReverseZ(-Screen.width / 2f, Screen.width / 2f, -Screen.height / 2f, Screen.height / 2f, 0, 1);
				command.SetGlobalMatrix("UiOverlayMatrix", overlayMatrix);
			});

			// Render gizmos
			if (Handles.ShouldRenderGizmos())
			{
				var preImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PreImageEffects);
				var postImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PostImageEffects);

				var renderGizmos = AddRenderPass((preImageEffectsRenderList, postImageEffectsRenderList), "Render Gizmos");
				renderGizmos.SetRenderFunction(static (command, data) =>
				{
					// Note that gizmos use their own matrix logic which we can't override
					command.DrawRendererList(data.preImageEffectsRenderList);
					command.DrawRendererList(data.postImageEffectsRenderList);
				});
			}

			// Render wireframe
			var wireframeRendererList = context.CreateWireOverlayRendererList(camera);
			var renderWireframe = AddRenderPass((camera, wireframeRendererList), "Render Gizmos");
			renderWireframe.SetRenderFunction(static (command, data) =>
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
				var hasTarget = target.resourceIndex != -1;
				if (i == target.lastReadIndex && !hasTarget)
				{
					attachmentDescriptor.storeAction = RenderBufferStoreAction.DontCare;
				}
				else
				{
					RenderTargetIdentifier resource;
					if (hasTarget)
					{
						resource = resources[target.resourceIndex];
					}
					else
					{
						var descriptor = new RenderTextureDescriptor
						{
							width = target.descriptor.size.x,
							height = target.descriptor.size.y,
							volumeDepth = 1,
							msaaSamples = target.dontResolve ? target.descriptor.samples : 1,
							mipCount = 1,
							dimension = TextureDimension.Tex2D,
							shadowSamplingMode = ShadowSamplingMode.None,
						};

						// This output gets read later so we need to allocate a texture for it
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
						
						if (isColor)
							descriptor.graphicsFormat = target.descriptor.format;

						if (isDepth)
							descriptor.depthStencilFormat = target.descriptor.format;

						if (isStencil)
							descriptor.stencilFormat = GraphicsFormat.R8_UInt;

						if (target.dontResolve && target.descriptor.samples > 1)
							descriptor.bindMS = true;

						target.resourceIndex = resources.Count;
						command.GetTemporaryRT(target.resourceIndex, descriptor);

						resource = target.resourceIndex;
						resources.Add(resource);
						targets[output.handle.index] = target;
					}

					var requiresResolve = !output.dontResolve && target.descriptor.samples > 1;
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
