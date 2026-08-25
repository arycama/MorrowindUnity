using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public abstract class RenderPipelineBase : RenderPipeline
{
	private readonly CommandBuffer command;
	protected readonly RenderGraph renderGraph = new();

	public RenderPipelineBase()
	{
		command = new() { name = "Render Frame" };
	}

	protected override void Dispose(bool disposing)
	{
		renderGraph.Dispose();
	}

	protected abstract void RenderCamera(Camera camera, ScriptableCullingParameters cullingParameters, ScriptableRenderContext context);

	protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
	{
		command.Clear();
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
		}
		//EndContextRendering(context, cameras);

		renderGraph.Execute(command);
		context.ExecuteCommandBuffer(command);

		//if (context.SubmitForRenderPassValidation())
		context.Submit();
	}
}
