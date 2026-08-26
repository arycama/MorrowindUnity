using System;
using UnityEngine.Rendering;

public readonly struct RenderPass<T> : IRenderPass
{
	private readonly T data;
	private readonly Action<CommandBuffer, T> render;

	public RenderPass(string name, ViewHandle viewHandle, Range resourceRange, int nativePassIndex, bool isNewSubPass, T data, Action<CommandBuffer, T> render)
	{
		this.data = data;
		this.render = render ?? throw new ArgumentNullException(nameof(render));
		Name = name ?? throw new ArgumentNullException(nameof(name));
		ViewHandle = viewHandle;
		ResourceRange = resourceRange;
		NativePassIndex = nativePassIndex;
		IsNewSubPass = isNewSubPass;
	}

	public string Name { get; }
	public ViewHandle ViewHandle { get; }
	public Range ResourceRange { get; }
	public int NativePassIndex { get; }
	public bool IsNewSubPass { get; }

	readonly void IRenderPass.Execute(CommandBuffer command)
	{
		render(command, data);
	}
}
