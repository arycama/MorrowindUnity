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

		var lightCounts = renderGraph.GetTexture(new(tileCountX, tileCountY), GraphicsFormat.R8_UInt, viewPassData.viewCount, TextureDimension.Tex2DArray);
		var lightList = renderGraph.GetBuffer(tileCount * maxLightsPerTile * viewPassData.viewCount, UnsafeUtility.SizeOf<LightData>());

		using (var pass = renderGraph.AddComputeRenderPass("Light Culling", (viewPassData.viewCount, tileCount * maxLightsPerTile, lightCounts, tileCountY)))
		{
			pass.Initialize(computeShader, 0, tileCountX, tileCountY, viewPassData.viewCount, false);
			pass.ReadResource<PointLightData>();

			pass.WriteBuffer("LightListWrite", lightList);
			pass.WriteTexture("LightCountWrite", lightCounts);
			pass.ReadResource<ViewData>();

			pass.SetRenderFunction(static (command, pass, data) =>
			{
				command.SetRenderTarget(pass.GetRenderTexture(data.lightCounts));
				command.ClearRenderTarget(true, true, default);

				pass.SetInt("ViewCount", data.viewCount);
			});
		}

		renderGraph.SetResource(new Result(lightCounts, lightList));
	}

	public readonly struct Result : IRenderPassData
	{
		private readonly ResourceHandle<RenderTexture> lightCounts;
		private readonly ResourceHandle<GraphicsBuffer> lightList;

		public Result(ResourceHandle<RenderTexture> lightCounts, ResourceHandle<GraphicsBuffer> lightList)
		{
			this.lightCounts = lightCounts;
			this.lightList = lightList;
		}

		public void SetInputs(RenderPass pass)
		{
			pass.ReadTexture("LightCounts", lightCounts);
			pass.ReadBuffer("LightList", lightList);
		}

		public void SetProperties(RenderPass pass, CommandBuffer command)
		{
		}
	}
}
