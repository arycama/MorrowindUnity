using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

public readonly struct RenderPass<T> : IRenderPass
{
	private readonly T data;
	private readonly Action<CommandBuffer, T> render;
	private readonly List<(TextureHandle, int)> inputs;
	private readonly List<TextureHandle> outputs;

	public RenderPass(T data)
	{
		this.data = data;
		render = null;
		inputs = new();
		outputs = new();
	}

	public void ReadTexture(TextureHandle handle, int propertyId)
	{
		inputs.Add((handle, propertyId));
	}

	public void WriteTexture(TextureHandle handle)
	{
		outputs.Add(handle);
	}

	readonly void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}
}
