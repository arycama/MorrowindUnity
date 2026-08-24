using System;
using System.Diagnostics;
using UnityEngine.Rendering;

[DebuggerDisplay("{Name}")]
public readonly struct RenderPass<T> : IRenderPass
{
	private readonly T data;
	private readonly Action<CommandBuffer, T> render;

	public RenderPass(T data, Action<CommandBuffer, T> render)
	{
		this.data = data;
		this.render = render;
	}

	readonly void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}
}
