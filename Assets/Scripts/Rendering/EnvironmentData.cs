using UnityEngine;
using UnityEngine.Rendering;
using CustomRenderPipeline;

public readonly struct EnvironmentData : IRenderPassData
{
	private readonly ResourceHandle<GraphicsBuffer> buffer;

	public EnvironmentData(ResourceHandle<GraphicsBuffer> buffer)
	{
		this.buffer = buffer;
	}

	void IRenderPassData.SetInputs(CustomRenderPipeline.RenderPass pass)
	{
		pass.ReadBuffer("EnvironmentData", buffer);
	}

	void IRenderPassData.SetProperties(CustomRenderPipeline.RenderPass pass, CommandBuffer command)
	{
	}
}
