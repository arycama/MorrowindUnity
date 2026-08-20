using System;
using System.Collections.Generic;
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
	private readonly List<RenderTargetIdentifier?> targets = new();
	private readonly List<bool> targetsRead = new();
	private readonly List<IRenderPass> renderPasses = new();

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		command = new() { name = "Render Frame" };
		blitMaterial = new Material(Shader.Find("Hidden/Blit Material")) { hideFlags = HideFlags.HideAndDontSave };
	}

	private static RenderTextureDescriptor GetRenderTextureDescriptor(RenderTargetDescriptor a, bool resolve)
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

	private int GetTexture(RenderTargetDescriptor desc)
	{
		targetDescriptors.Add(desc);
		targets.Add(default);
		targetsRead.Add(false);
		return targetDescriptors.Count - 1;
	}

	private static void ReadTexture(string propertyName, int index, CommandBuffer command, List<bool> targetsRead, List<RenderTargetIdentifier?> targets)
	{
		targetsRead[index] = true;
		command.SetGlobalTexture(propertyName, targets[index].Value);
	}

	private int AddRenderPass<T>(T data, Action<CommandBuffer, T> render)
	{
		renderPasses.Add(new RenderPass<T>(data, render));
		return renderPasses.Count;
	}

	private static NativeRenderPass<T> AddNativeRenderPass<T>(Int2 size, int samples, CommandBuffer command, string name, T data, Action<CommandBuffer, T> render)
	{
		return new NativeRenderPass<T>(size, samples, command, name, data, render);
	}

	protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
	{
		// Cleanup from last frame. We do this incase there was an error which would cause any code at the end of the last frame to not be called
		command.Clear();
		targetDescriptors.Clear();
		targets.Clear();
		targetsRead.Clear();
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
			var cameraTargetId = Shader.PropertyToID("CameraTarget");
			var cameraDepthId = Shader.PropertyToID("CameraDepth");

			// Setup depth target
			var depthFormat = camera.targetTexture == null ? GraphicsFormat.D32_SFloat_S8_UInt : camera.targetTexture.depthStencilFormat;
			var cameraDepthIndex = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), depthFormat, asset.Samples, true));

			// For scene view we need depth for wireframe (Note that msaa depth can not be resolved automatically so we need to store and manually resolve later)
			var requiresDepthResolve = camera.cameraType == CameraType.SceneView;

			// TODO: This should also account for HDR
			var backbufferFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
			var targetFormat = camera.targetTexture == null ? backbufferFormat : camera.targetTexture.graphicsFormat;
			var cameraColorIndex = GetTexture(new(new(camera.pixelWidth, camera.pixelHeight), targetFormat, asset.Samples, true, RenderSettings.fogColor.linear));

			// Can only render directly to backbuffer if there is no msaa samples and there is no target texture
			// TODO: Check for hardware msaa backbuffer resolve support
			var directToBackbuffer = asset.Samples == 1 && camera.targetTexture == null;

			// Pass 0
			AddRenderPass((camera, asset, requiresDepthResolve), static (command, data) =>
			{
				// Setup view data
				var tanHalfFovY = Tan(0.5f * Radians(data.camera.fieldOfView));
				var tanHalfFov = new Float2(tanHalfFovY * data.camera.aspect, tanHalfFovY);
				var worldToView = Float4x4.WorldToLocal(0.0f, data.camera.transform.WorldRotation());
				var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, data.camera.nearClipPlane, data.camera.farClipPlane);
				var worldToClip = viewToClip.Mul(worldToView);

				var fogEnabled = RenderSettings.fog;
#if UNITY_EDITOR
				if (SceneView.currentDrawingSceneView != null)
					fogEnabled &= SceneView.currentDrawingSceneView.sceneViewState.fogEnabled;
#endif

				command.SetGlobalMatrix("WorldToView", worldToView);
				command.SetGlobalMatrix("WorldToClip", worldToClip);
				command.SetGlobalVector("ViewPosition", data.camera.transform.position);
				command.SetGlobalVector("SunDirection", data.camera.transform.WorldRotation().InverseRotate(-RenderSettings.sun.transform.forward));
				command.SetGlobalVector("SunColor", RenderSettings.sun.color.linear);
				command.SetGlobalVector("AmbientLight", RenderSettings.ambientLight.linear);
				command.SetGlobalVector("FogColor", RenderSettings.fogColor.linear);
				command.SetGlobalFloat("FogScale", fogEnabled ? 1 / (RenderSettings.fogEndDistance - RenderSettings.fogStartDistance) : 0);
				command.SetGlobalFloat("FogOffset", fogEnabled ? RenderSettings.fogStartDistance / (RenderSettings.fogStartDistance - RenderSettings.fogEndDistance) : 0);

				// This is basically only required if the camera is rendering to a no-resolved MSAA texture, which is the case for depth in the scene view..
				// TODO: Explain this betetr (I think its because we don't flip, but msaa causes culling to invert anyway? um
				if (data.asset.Samples > 1 && data.camera.targetTexture == null)
					command.SetInvertCulling(true);
			});

			AddRenderPass((camera, asset, requiresDepthResolve, context, cullingResults, cameraDepthId, targetDescriptors, cameraDepthIndex, targets, directToBackbuffer, cameraColorIndex, cameraTargetId), static (command, data) =>
			{
				using (var colorPass = AddNativeRenderPass(new(data.camera.pixelWidth, data.camera.pixelHeight), data.asset.Samples, command, "Base Pass", (data.context, data.cullingResults, data.camera), static (command, data) =>
				{
					var context = data.context;
					var cullingResults = data.cullingResults;
					var camera = data.camera;
					command.DrawRendererList(context.CreateRendererList(new(new ShaderTagId("Forward"), cullingResults, camera) { renderQueueRange = RenderQueueRange.opaque }));
				}))
				{
					// TODO: This logic should be deferred 
					if (data.requiresDepthResolve)
					{
						command.GetTemporaryRT(data.cameraDepthId, GetRenderTextureDescriptor(data.targetDescriptors[data.cameraDepthIndex], false));
						data.targets[data.cameraDepthIndex] = data.cameraDepthId;
					}

					// TODO: This should just be a list of things passed to the struct. (Span?)
					colorPass.WriteAttachment(data.targetDescriptors[data.cameraDepthIndex], data.targets[data.cameraDepthIndex], false);

					if (data.directToBackbuffer)
					{
						data.targets[data.cameraColorIndex] = BuiltinRenderTextureType.CameraTarget;
					}
					else
					{
						command.GetTemporaryRT(data.cameraTargetId, GetRenderTextureDescriptor(data.targetDescriptors[data.cameraColorIndex], true));
						data.targets[data.cameraColorIndex] = data.cameraTargetId;
					}

					colorPass.WriteAttachment(data.targetDescriptors[data.cameraColorIndex], data.targets[data.cameraColorIndex], true);
				}

				if (data.asset.Samples > 1 && data.camera.targetTexture == null)
					command.SetInvertCulling(false);
			});

			// Pass 1
			if (!directToBackbuffer)
			{
				AddRenderPass((camera, cameraColorIndex, requiresDepthResolve, cameraDepthIndex, targetsRead, targets, asset, blitMaterial, targetDescriptors), static (command, data) =>
				{
					command.SetWireframe(false);
					command.SetGlobalVector("ViewSize", new(data.camera.pixelWidth, data.camera.pixelHeight));

					// Can't really be a subpass since it requires resolving or flipping
					ReadTexture("CameraTarget", data.cameraColorIndex, command, data.targetsRead, data.targets);

					if (data.requiresDepthResolve)
					{
						ReadTexture("CameraDepth", data.cameraDepthIndex, command, data.targetsRead, data.targets);

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

					// When rendering to the backbuffer we need to avoid flipping (This is kind of inverted keyword)
					if (data.camera.targetTexture == null)
						command.EnableShaderKeyword("FLIP");
				
					using (var blitPass = AddNativeRenderPass(new(data.camera.pixelWidth, data.camera.pixelHeight), 1, command, "Blit", data.blitMaterial, static (command, blitMaterial) =>
					{
						command.DrawProcedural(Matrix4x4.identity, blitMaterial, 0, MeshTopology.Triangles, 3);
					}))
					{
						if (data.requiresDepthResolve)
							blitPass.WriteAttachment(data.targetDescriptors[data.cameraDepthIndex], data.camera.targetTexture, false);

						blitPass.WriteAttachment(data.targetDescriptors[data.cameraColorIndex], data.camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : data.camera.targetTexture, false);
					}

					if (data.camera.targetTexture == null)
						command.DisableShaderKeyword("FLIP");

					if (data.requiresDepthResolve)
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

			AddRenderPass((camera, context), static (command, data) =>
			{
				// Editor-only, to make selection-wireframe render properly, we need to setup the same camera properties again but with a flipped matrix
				var tanHalfFovY = Tan(0.5f * Radians(data.camera.fieldOfView));
				var tanHalfFov = new Float2(tanHalfFovY * data.camera.aspect, tanHalfFovY);
				var worldToView = Float4x4.WorldToLocal(0.0f, data.camera.transform.WorldRotation());
				var viewToClip = Float4x4.PerspectiveReverseZ(tanHalfFov, data.camera.nearClipPlane, data.camera.farClipPlane, 0, true);
				var worldToClip = viewToClip.Mul(worldToView);
				command.SetGlobalMatrix("WorldToClip", worldToClip);

				// For selection outline to work, we need to also set this builtin matrix
				var worldToViewAbs = Float4x4.WorldToLocal(data.camera.transform.WorldPosition(), data.camera.transform.WorldRotation());
				var worldToClipAbs = viewToClip.Mul(worldToViewAbs);
				command.SetGlobalMatrix("unity_MatrixVP", worldToClipAbs);

				command.DrawRendererList(((ScriptableRenderContext)data.context).CreateWireOverlayRendererList(data.camera));

				if (Handles.ShouldRenderGizmos())
				{
					command.DrawRendererList(((ScriptableRenderContext)data.context).CreateGizmoRendererList(data.camera, GizmoSubset.PreImageEffects));
					command.DrawRendererList(((ScriptableRenderContext)data.context).CreateGizmoRendererList(data.camera, GizmoSubset.PostImageEffects));
				}
			});
		}

		AddRenderPass(0, static (command, data) =>
		{
			// Set matrices for UI rendering
			var overlayMatrix = Float4x4.OrthoReverseZ(-Screen.width / 2f, Screen.width / 2f, -Screen.height / 2f, Screen.height / 2f, 0, 1);
			command.SetGlobalMatrix("UiOverlayMatrix", overlayMatrix);
		});

		foreach (var pass in renderPasses)
			pass.Execute(command);

		context.ExecuteCommandBuffer(command);

		if (context.SubmitForRenderPassValidation())
			context.Submit();
	}
}
