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

			cullingParameters.cullingOptions = CullingOptions.DisablePerObjectCulling;
			var cullingResults = context.Cull(ref cullingParameters);

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

			var count = camera.targetTexture == null ? 2 : 3;

			var attachments = new NativeArray<AttachmentDescriptor>(count, Allocator.Temp);
			attachments[0] = new AttachmentDescriptor
			{
				loadAction = RenderBufferLoadAction.Clear,
				storeAction = RenderBufferStoreAction.DontCare,
				graphicsFormat = GraphicsFormat.D32_SFloat_S8_UInt,
				loadStoreTarget = BuiltinRenderTextureType.None,
				resolveTarget = BuiltinRenderTextureType.None,
				clearColor = new Color(0, 0, 0, 0),
				clearDepth = 1.0f,
				clearStencil = 1u
			};

			// Color 
			attachments[1] = new AttachmentDescriptor
			{
				loadAction = RenderBufferLoadAction.Clear,
				storeAction = RenderBufferStoreAction.Store,
				graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32,
				loadStoreTarget = camera.targetTexture == null ? BuiltinRenderTextureType.CameraTarget : BuiltinRenderTextureType.None,
				resolveTarget = BuiltinRenderTextureType.None,
				clearColor = RenderSettings.fogColor.linear,
				clearDepth = 1.0f,
				clearStencil = 1u
			};

			if(camera.targetTexture != null)
			{
				attachments[2] = new AttachmentDescriptor
				{
					loadAction = RenderBufferLoadAction.Clear,
					storeAction = RenderBufferStoreAction.Store,
					graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32,
					loadStoreTarget = camera.targetTexture,
					resolveTarget = BuiltinRenderTextureType.None,
					clearColor = default,
					clearDepth = 1.0f,
					clearStencil = 1u
				};
			}

			var subPassCount = camera.targetTexture == null ? 1 : 2;
			var subPasses = new NativeArray<SubPassDescriptor>(subPassCount, Allocator.Temp);

			var colorOutputs0 = new AttachmentIndexArray(1);
			colorOutputs0[0] = 1;
			subPasses[0] = new SubPassDescriptor() { colorOutputs = colorOutputs0 };

			if(camera.targetTexture != null)
			{
				var colorOutputs1 = new AttachmentIndexArray(1);
				colorOutputs1[0] = 2;

				var colorInputs1 = new AttachmentIndexArray(1);
				colorInputs1[0] = 1;

				subPasses[1] = new SubPassDescriptor() { colorOutputs = colorOutputs1, inputs = colorInputs1 };
			}

			command.BeginRenderPass(camera.pixelWidth, camera.pixelHeight, asset.Samples, attachments, 0, subPasses);

			var shaderPassName = new ShaderTagId("Forward");
			var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
			var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

			var drawSettings = new DrawingSettings(shaderPassName, sortingSettings) { enableInstancing = true, perObjectData = PerObjectData.None };
			var rendererListParams = new RendererListParams(cullingResults, drawSettings, filteringSettings);
			var opaqueRendererList = context.CreateRendererList(ref rendererListParams);

			command.DrawRendererList(opaqueRendererList);

			if(camera.targetTexture != null)
			{
				command.NextSubPass();

				// Blit
				command.DrawProcedural(Matrix4x4.identity, blitMaterial, 0, MeshTopology.Triangles, 3);
			}

			command.EndRenderPass();
		}

		context.ExecuteCommandBuffer(command);
		command.Clear();

		if (context.SubmitForRenderPassValidation())
			context.Submit();
	}
}
