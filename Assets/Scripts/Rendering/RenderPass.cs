using System;
using System.Diagnostics;
using UnityEngine.Rendering;
using Unmath;

[DebuggerDisplay("{Name}")]
public struct RenderPass<T> : IRenderPass
{
	private readonly T data;
	private readonly Action<CommandBuffer, T> render;
	public bool IsNativeRenderPass { get; }

	public RenderPass(bool isNativeRenderPass, T data, Action<CommandBuffer, T> render)
	{
		IsNativeRenderPass = isNativeRenderPass;
		this.data = data;
		this.render = render;
	}

	void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}
}
