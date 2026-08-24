using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unmath;
using static Unmath.Math;

#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class RenderPipelineBase : RenderPipeline
{
	private readonly CommandBuffer command;
	protected readonly RenderGraph renderGraph = new();

	public RenderPipelineBase()
	{
		command = new() { name = "Render Frame" };
	}

	protected abstract void RenderCamera(Camera camera, ScriptableCullingParameters cullingParameters, ScriptableRenderContext context);

	protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
	{
		renderGraph.Clear();

		//BeginContextRendering(context, cameras);
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

			BeginCameraRendering(context, camera);
			RenderCamera(camera, cullingParameters, context);
			//EndCameraRendering(context, camera);

#if UNITY_EDITOR
			// Render gizmos
			var viewSize = new Int2(camera.pixelWidth, camera.pixelHeight);
			var viewInfo = renderGraph.AddViewInfo(new(camera.pixelWidth, camera.pixelHeight));
			if (Handles.ShouldRenderGizmos())
			{
				var preImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PreImageEffects);
				var postImageEffectsRenderList = context.CreateGizmoRendererList(camera, GizmoSubset.PostImageEffects);

				renderGraph.AddRenderPass("Render Gizmos", viewInfo, (preImageEffectsRenderList, postImageEffectsRenderList), render: static (command, data) =>
				{
					// Note that gizmos use their own matrix logic which we can't override
					command.DrawRendererList(data.preImageEffectsRenderList);
					command.DrawRendererList(data.postImageEffectsRenderList);
				});
			}

			// Render wireframe
			if (camera.cameraType == CameraType.SceneView)
			{
				var wireframeRendererList = context.CreateWireOverlayRendererList(camera);
				renderGraph.AddRenderPass("Render Wireframe", viewInfo, (camera, wireframeRendererList, context), render: static (command, data) =>
				{
					data.context.SetupCameraProperties(data.camera);

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
					command.DrawRendererList(data.wireframeRendererList);
				});
			}
#endif
		}
		//EndContextRendering(context, cameras);

		renderGraph.Execute(command);
		context.ExecuteCommandBuffer(command);
		command.Clear();

		if (context.SubmitForRenderPassValidation())
			context.Submit();
	}
}
