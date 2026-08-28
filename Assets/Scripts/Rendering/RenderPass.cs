using System;
using System.Collections.Generic;
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

	public string Name { get; set; }
	public Range ResourceRange { get; set; }
	public ViewHandle ViewHandle { get; set; }
	public int NativePassIndex { get; set; }
	public bool IsNewSubPass { get; set; }
	public List<GlobalKeyword> Keywords { get; set; } = new();

	void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}
}
