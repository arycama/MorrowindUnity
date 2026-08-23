using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine.Rendering;
using Unmath;

[DebuggerDisplay("{Name}")]
public class RenderPass<T> : IRenderPass
{
	public string Name { get; }
	public int Index { get; }
	public bool InvertCulling { get; }
	private readonly T data;
	private readonly Action<CommandBuffer, T> render;
	private readonly RenderGraph renderGraph;

	public List<(TextureHandle, int)> Inputs { get; }
	public List<TextureHandle> Outputs { get; }
	public bool IsNativeRenderPass { get; private set; }
	public Int2 Size { get; private set; }
	public int Samples { get; private set; }

	public RenderPass(string name, int index, bool isNativeRenderPass, Int2 size, int samples, RenderGraph renderGraph, T data, ReadOnlySpan<TextureHandle> outputs, Action<CommandBuffer, T> render)
	{
		Name = name;
		Index = index;
		IsNativeRenderPass = isNativeRenderPass;
		Size = size;
		Samples = samples;
		this.renderGraph = renderGraph;
		this.data = data;
		this.render = render;
		Inputs = new();
		Outputs = new();

		foreach(var output in outputs)
		{
			Outputs.Add(output);
			renderGraph.SetTargetWriteIndex(output, Index);
		}
	}

	void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}

	public void ReadTexture(TextureHandle handle, int propertyId)
	{
		// Update the last read index. Since rendergraph executes serially, this will always be the last-read pass
		Inputs.Add((handle, propertyId));
		renderGraph.SetTargetReadIndex(handle, Index);
	}
}
