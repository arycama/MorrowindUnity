using UnityEngine;
using UnityEngine.Rendering;
using CustomRenderPipeline;

public readonly struct LightingData : IRenderPassData
{
	private readonly ResourceHandle<RenderTexture> sunShadow;
	private readonly ResourceHandle<GraphicsBuffer> lightingDataBuffer;
	private readonly bool sunShadowEnabled;

	public LightingData(ResourceHandle<RenderTexture> sunShadow, ResourceHandle<GraphicsBuffer> lightingDataBuffer, bool sunShadowEnabled)
	{
		this.sunShadow = sunShadow;
		this.lightingDataBuffer = lightingDataBuffer;
		this.sunShadowEnabled = sunShadowEnabled;
	}

	void IRenderPassData.SetInputs(RenderPass pass)
	{
		pass.ReadTexture("SunShadow", sunShadow);
		pass.ReadBuffer("LightingData", lightingDataBuffer);

		if (sunShadowEnabled)
			pass.AddKeyword("SHADOWS_ON");
	}

	void IRenderPassData.SetProperties(RenderPass pass, CommandBuffer command)
	{
	}
}
