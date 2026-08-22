using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Unmath;

public class RenderPass<T> : IRenderPass
{
	private readonly T data;
	public int Index { get; }
	public string Name { get; }

	public List<(TextureHandle, int)> Inputs { get; }
	public List<(TextureHandle handle, bool dontResolve)> Outputs { get; }

	public Action<CommandBuffer, T> render;
	public bool BeginRenderPass { get; private set; }
	public Int2 Size { get; private set; }
	public int Samples { get; private set; }

	public RenderPass(T data, int index, string name)
	{
		this.data = data;
		Index = index;
		render = null;
		Inputs = new();
		Outputs = new();
		BeginRenderPass = false;
		Size = 1;
		Samples = 1;
		Name = name;
	}

	void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}

	public void SetRenderPassParams(Int2 size, int samples)
	{
		BeginRenderPass = true;
		Size = size;
		Samples = samples;
	}

	public void SetRenderFunction(Action<CommandBuffer, T> render)
	{
		this.render = render;
	}

	public override string ToString() => Name;
}
