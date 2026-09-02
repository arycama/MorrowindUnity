using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class PassBuilder : IDisposable
{
	public RenderGraph RenderGraph { get; }

	public string Name { get; set; }
	public int Index { get; set; } = -1;
	public ViewHandle ViewHandle { get; set; } = new(-1);
	public IRenderPass RenderPass { get; private set; }
	public List<ResourceHandle> Resources { get; } = new();
	public List<TextureHandle> Inputs { get; } = new();
	public List<TextureHandle> Outputs { get; } = new();
	public List<ResourceHandle> UavOutputs { get; } = new();
	public List<GlobalKeyword> Keywords { get; } = new();
	public TextureHandle DepthStencil { get; set; } = new(-1);
	public int DepthSlice { get; set; } = -1;
	public int VolumeDepth { get; set; } = 1;

	public PassBuilder(RenderGraph renderGraph)
	{
		RenderGraph = renderGraph;
	}

	public void AddResources(ReadOnlySpan<Type> types)
	{
		foreach (var type in types)
			if (RenderGraph.TryGetResource(type, out var resource))
				resource.SetData(this);
	}

	public void AddResource<T>() where T : IRenderResource
	{
		if (RenderGraph.TryGetResource<T>(out var resource))
			resource.SetData(this);
	}

	public void AddResources<T0, T1>() where T0 : IRenderResource where T1 : IRenderResource
	{
		AddResource<T0>();
		AddResource<T1>();
	}

	public void AddResources<T0, T1, T2>() where T0 : IRenderResource where T1 : IRenderResource where T2 : IRenderResource
	{
		AddResource<T0>();
		AddResources<T1, T2>();
	}

	public void AddResources<T0, T1, T2, T3>() where T0 : IRenderResource where T1 : IRenderResource where T2 : IRenderResource where T3 : IRenderResource
	{
		AddResource<T0>();
		AddResources<T1, T2, T3>();
	}

	public void AddResources<T0, T1, T2, T3, T4>() where T0 : IRenderResource where T1 : IRenderResource where T2 : IRenderResource where T3 : IRenderResource where T4 : IRenderResource
	{
		AddResource<T0>();
		AddResources<T1, T2, T3, T4>();
	}

	public void AddResource(ResourceHandle resource) => Resources.Add(resource);

	public void AddResources(Span<ResourceHandle> resources)
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

	public void AddUavOutput(ResourceHandle output) => UavOutputs.Add(output);

	public void AddUavOutputs(ReadOnlySpan<ResourceHandle> outputs)
	{
		foreach (var output in outputs)
			AddUavOutput(output);
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
		RenderGraph.SetRenderPass(this);
		Name = default;
		ViewHandle = new(-1);
		Index = -1;
		DepthSlice = -1;
		VolumeDepth = 1;
		DepthStencil = new(-1);
		RenderPass = null;
		Resources.Clear();
		Outputs.Clear();
		UavOutputs.Clear();
		Inputs.Clear();
		Keywords.Clear();
	}
}