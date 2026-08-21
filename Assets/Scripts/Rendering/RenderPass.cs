using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

public struct RenderPass<T> : IRenderPass
{
	private readonly T data;
	private readonly List<(TextureHandle, int)> inputs;
	private readonly List<TextureHandle> outputs;

	private Action<CommandBuffer, T> render;

	public RenderPass(T data)
	{
		this.data = data;
		render = null;
		inputs = new();
		outputs = new();
	}

	public readonly void ReadTexture(TextureHandle handle, int propertyId)
	{
		inputs.Add((handle, propertyId));
	}

	public readonly void WriteTexture(TextureHandle handle)
	{
		outputs.Add(handle);
	}

	readonly void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}

	public void SetRenderFunction(Action<CommandBuffer, T> render)
	{
		this.render = render;
	}

	public void Render(CommandBuffer command)
	{
		render(command, data);
	}
}
