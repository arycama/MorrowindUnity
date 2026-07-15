using System;
using CustomRenderPipeline;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static Unmath.Math;

public class LightCulling : ViewRenderFeature
{
    [Serializable]
    public class Settings
    {
        [field: SerializeField, Pow2(32)] public int TileSize { get; private set; } = 16;
    }

	public const int maxLightsPerTile = 32;

	private readonly Settings settings;
	private readonly ComputeShader computeShader;

	public LightCulling(Settings settings, RenderGraph renderGraph) : base(renderGraph)
	{
		this.settings = settings;
		computeShader = Resources.Load<ComputeShader>("LightCulling");
	}
	
	public override void Render(in ReadOnlySpan<ViewParameter> viewParameters, in ViewPassData viewPassData, in DisplayData displayOutputData, ScriptableRenderContext context)
	{
		var tileCountX = DivRoundUp(viewPassData.viewSize.x, settings.TileSize);
		var tileCountY = DivRoundUp(viewPassData.viewSize.y, settings.TileSize);
		var tileCount = tileCountX * tileCountY;

		var pointLightData = renderGraph.GetResource<PointLightData>();
		var pointLightCount = pointLightData.lightCount;

		var lightIndexCount = DivRoundUp(pointLightCount, 32);
		var visibleLightBits = renderGraph.GetBuffer(Max(1, lightIndexCount * tileCount));

		using (var pass = renderGraph.AddComputeRenderPass("Light Culling", (viewPassData.viewCount, tileCount * maxLightsPerTile, tileCountY)))
		{
			pass.Initialize(computeShader, 0, tileCountX, tileCountY, viewPassData.viewCount, false);
			pass.ReadResource<PointLightData>();

			pass.WriteBuffer("VisibleLightBitsWrite", visibleLightBits);
			pass.ReadResource<ViewData>();
		}

		renderGraph.SetResource(new Result(visibleLightBits));
	}

	public readonly struct Result : IRenderPassData
	{
		private readonly ResourceHandle<GraphicsBuffer> visibleLightBits;

		public Result(ResourceHandle<GraphicsBuffer> visibleLightBits)
		{
			this.visibleLightBits = visibleLightBits;
		}

		public void SetInputs(RenderPass pass)
		{
			pass.ReadBuffer("VisibleLightBits", visibleLightBits);
		}

		public void SetProperties(RenderPass pass, CommandBuffer command)
		{
		}
	}
}
