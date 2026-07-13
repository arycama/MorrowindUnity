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

	void IRenderPassData.SetInputs(RenderPass pass)
	{
		pass.ReadBuffer("EnvironmentData", buffer);
	}

	void IRenderPassData.SetProperties(RenderPass pass, CommandBuffer command)
	{
	}
}
