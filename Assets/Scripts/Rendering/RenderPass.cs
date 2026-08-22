using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Unmath;

public class RenderPass<T> : IRenderPass
{
	private T data;
	public List<(TextureHandle, int)> inputs { get; }
	public List<(TextureHandle handle, bool dontResolve)> outputs { get; }

	public Action<CommandBuffer, T> render;
	public bool beginRenderPass { get; private set; }
	public Int2 size { get; private set; }
	public int samples { get; private set; }
	public string name { get; private set; }

	public RenderPass(T data)
	{
		this.data = data;
		render = null;
		inputs = new();
		outputs = new();
		beginRenderPass = false;
		size = 1;
		samples = 1;
		name = null;
	}

	public void ReadTexture(TextureHandle handle, int propertyId)
	{
		inputs.Add((handle, propertyId));
	}

	public void WriteTexture(TextureHandle handle, bool dontResolve)
	{
		outputs.Add((handle, dontResolve));
	}

	void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}

	public void SetRenderPassParams(Int2 size, int samples, string name)
	{
		beginRenderPass = true;
		this.size = size;
		this.samples = samples;
		this.name = name;
	}

	public void SetRenderFunction(Action<CommandBuffer, T> render)
	{
		this.render = render;
	}
}
