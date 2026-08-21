using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

public readonly struct RenderPass<T> : IRenderPass
{
	private readonly T data;
	private readonly Action<CommandBuffer, T> render;
	private readonly List<int> inputs;
	private readonly List<int> outputs;

	public RenderPass(T data, Action<CommandBuffer, T> render)
	{
		this.data = data;
		this.render = render;
		inputs = new();
		outputs = new();
	}

	public void ReadTexture()
	{

	}

	readonly void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}
}
