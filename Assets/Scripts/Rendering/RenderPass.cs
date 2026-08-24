using System;
using System.Diagnostics;
using UnityEngine.Rendering;
using Unmath;

[DebuggerDisplay("{Name}")]
public struct RenderPass<T> : IRenderPass
{
	private readonly T data;
	private readonly Action<CommandBuffer, T> render;
	public bool IsNativeRenderPass { get; private set; }
	public Int2 Size { get; private set; }
	public int Samples { get; private set; }

	public RenderPass(bool isNativeRenderPass, Int2 size, int samples, T data, Action<CommandBuffer, T> render)
	{
		IsNativeRenderPass = isNativeRenderPass;
		Size = size;
		Samples = samples;
		this.data = data;
		this.render = render;
	}

	void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}
}
