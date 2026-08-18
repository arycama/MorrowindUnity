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

	public NewPipeline(NewPipelineAsset asset)
	{
		this.asset = asset;
		command = new() { name = "Render Frame" };
	}

	protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
	{
		foreach (var camera in cameras)
		{
			if (!camera.TryGetCullingParameters(out var cullingParameters))
				continue;

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

			var attachments = new NativeArray<AttachmentDescriptor>(2, Allocator.Temp);
			attachments[0] = new AttachmentDescriptor(GraphicsFormat.D32_SFloat_S8_UInt);
			attachments[0].ConfigureClear(default);
			attachments[1] = new AttachmentDescriptor(camera.targetTexture == null ? GraphicsFormat.B10G11R11_UFloatPack32 : camera.targetTexture.graphicsFormat);
			attachments[1].ConfigureClear(camera.backgroundColor.linear);
			attachments[1].ConfigureTarget(BuiltinRenderTextureType.CameraTarget, false, true);

			var subPasses = new NativeArray<SubPassDescriptor>(1, Allocator.Temp);

			var outputIndices = new NativeArray<int>(1, Allocator.Temp);
			outputIndices[0] = 1;
			subPasses[0] = new SubPassDescriptor() { colorOutputs = new AttachmentIndexArray(outputIndices) };


			var shaderPassName = new ShaderTagId("GBuffer");
			var sortingSettings = new SortingSettings(camera);
			var filteringSettings = new FilteringSettings(RenderQueueRange.all);

			var drawSettings = new DrawingSettings(shaderPassName, sortingSettings);
			var rendererListParams = new RendererListParams(cullingResults, drawSettings, filteringSettings);

			var rendererList = context.CreateRendererList(ref rendererListParams);
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

			command.BeginRenderPass(camera.pixelWidth, camera.pixelHeight, asset.Samples, attachments, 0, subPasses);
			command.DrawRendererList(rendererList);
			command.EndRenderPass();
		}

		context.ExecuteCommandBuffer(command);
		command.Clear();

		context.Submit();
	}
}
