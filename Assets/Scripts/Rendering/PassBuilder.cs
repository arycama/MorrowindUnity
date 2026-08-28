using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class PassBuilder : IDisposable
{
	private readonly RenderGraph renderGraph;

	public string Name { get; set; }
	public int Index { get; set; } = -1;
	public ViewHandle ViewHandle { get; set; } = new(-1);
	public IRenderPass RenderPass { get; private set; }
	public List<TextureHandle> Resources { get; } = new();
	public List<TextureHandle> Inputs { get; } = new();
	public List<TextureHandle> Outputs { get; } = new();
	public List<GlobalKeyword> Keywords { get; } = new();
	public TextureHandle DepthStencil { get; set; } = new(-1);

	public PassBuilder(RenderGraph renderGraph)
	{
		this.renderGraph = renderGraph;
	}

	public void AddResource(TextureHandle resource) => Resources.Add(resource);

	public void AddResources(ReadOnlySpan<TextureHandle> resources)
	{
		foreach (var resource in resources)
			AddResource(resource);
	}

	public void AddOutput(TextureHandle output) => Outputs.Add(output);

	public void AddOutputs(ReadOnlySpan<TextureHandle> outputs)
	{
		foreach (var output in outputs)
			AddOutput(output);
	}

	public void AddInput(TextureHandle input) => Inputs.Add(input);

	public void AddInputs(ReadOnlySpan<TextureHandle> inputs)
	{
		foreach (var input in inputs)
			AddInput(input);
	}

	public void AddKeyword(string keyword) => Keywords.Add(GlobalKeyword.Create(keyword));

	public void AddKeywords(ReadOnlySpan<string> keywords)
	{
		foreach (var keyword in keywords)
			AddKeyword(keyword);
	}

	public void SetRenderFunction<T>(T data, Action<CommandBuffer, T> render) => RenderPass = new RenderPass<T>(data, render);

	public void Dispose()
	{
		renderGraph.SetRenderPass(this);
		Name = default;
		ViewHandle = new(-1);
		Index = -1;
		DepthStencil = new(-1);
		RenderPass = null;
		Resources.Clear();
		Outputs.Clear();
		Inputs.Clear();
		Keywords.Clear();
	}
}