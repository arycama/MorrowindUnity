using System;
using System.Diagnostics;
using UnityEngine.Rendering;
using Unmath;

[DebuggerDisplay("{Name}")]
public class RenderPass<T> : IRenderPass
{
	public string Name { get; }
	public int Index { get; }
	private readonly T data;
	private readonly Action<CommandBuffer, T> render;
	public Range Inputs { get; }
	public Range Outputs { get; }

	public bool IsNativeRenderPass { get; private set; }
	public Int2 Size { get; private set; }
	public int Samples { get; private set; }

	public RenderPass(string name, int index, bool isNativeRenderPass, Int2 size, int samples, RenderGraph renderGraph, T data, ReadOnlySpan<TextureHandle> outputs, ReadOnlySpan<TextureHandle> inputs, Action<CommandBuffer, T> render)
	{
		Name = name;
		Index = index;
		IsNativeRenderPass = isNativeRenderPass;
		Size = size;
		Samples = samples;
		this.data = data;
		this.render = render;
		Inputs = renderGraph.SetInputs(inputs, index);
		Outputs = renderGraph.SetOutputs(outputs, index);
	}

	void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}
}
