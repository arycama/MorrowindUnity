using System;
using UnityEngine.Rendering;

public class RenderPass<T> : IRenderPass
{
	private readonly T data;
	private readonly Action<CommandBuffer, T> render;

	public RenderPass(T data, Action<CommandBuffer, T> render)
	{
		this.data = data;
		this.render = render;
	}

	void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}
}
