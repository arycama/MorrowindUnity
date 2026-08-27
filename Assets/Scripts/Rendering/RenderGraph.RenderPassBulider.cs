using System;
using UnityEngine.Rendering;

public partial class RenderGraph
{
	public ref struct RenderPassBulider
	{
		private readonly RenderGraph renderGraph;
		private readonly string name;
		private ViewHandle viewInfo;
		private readonly int index;

		public RenderPassBulider(RenderGraph renderGraph, string name, int index)
		{
			this.renderGraph = renderGraph;
			this.name = name;
			this.index = index;
			viewInfo = default;
		}

		public readonly void SetResources(ReadOnlySpan<TextureHandle> resources)
		{
			renderGraph.SetResources(resources, index);
		}

		public void SetViewInfo(ViewHandle viewInfo)
		{
			this.viewInfo = viewInfo;
		}

		public readonly void SetOutputs(ReadOnlySpan<TextureHandle> outputs)
		{
			renderGraph.AddOutputs(outputs, index);
		}

		public readonly void SetInputs(ReadOnlySpan<TextureHandle> inputs)
		{
			renderGraph.AddInputs(inputs, index);
		}

		public void SetRenderFunction<T>(T data, Action<CommandBuffer, T> render)
		{
			renderGraph.AddRenderPass(new RenderPass<T>(data, render));

		}

		public void Dispose()
		{
			renderGraph.SetRenderPass<int>(name, viewInfo, index);
		}
	}
}