using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

public partial class RenderGraph
{
	public class PassBuilder : IDisposable
	{
		private readonly RenderGraph renderGraph;

		public string Name { get; set; }
		public int Index { get; set; }
		public ViewHandle ViewInfo { get; set; }

		private IRenderPass renderPass;
		private readonly List<TextureHandle> resources = new(), inputs = new(), outputs = new();

		public PassBuilder(RenderGraph renderGraph)
		{
			this.renderGraph = renderGraph;
		}

		public void AddResource(TextureHandle resource)
		{
			resources.Add(resource);
		}

		public void SetResources(ReadOnlySpan<TextureHandle> resources)
		{
			foreach (var resource in resources)
				AddResource(resource);
		}

		public void AddOutput(TextureHandle output)
		{
			outputs.Add(output);
		}

		public void AddOutputs(ReadOnlySpan<TextureHandle> outputs)
		{
			foreach (var output in outputs)
				AddOutput(output);
		}

		public void AddInput(TextureHandle input)
		{
			inputs.Add(input);
		}

		public void AddInputs(ReadOnlySpan<TextureHandle> inputs)
		{
			foreach (var input in inputs)
				AddInput(input);
		}

		public void SetRenderFunction<T>(T data, Action<CommandBuffer, T> render)
		{
			renderPass = new RenderPass<T>(data, render);
		}

		public void Dispose()
		{
			renderGraph.SetRenderPass(Name, ViewInfo, Index, renderPass, resources, outputs, inputs);
			Name = default;
			ViewInfo = new(-1);
			Index = -1;
			renderPass = null;
			resources.Clear();
			outputs.Clear();
			inputs.Clear();
		}
	}
}