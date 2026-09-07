using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class ConstantBufferBuilder : IDisposable
{
	public string PropertyName { get; set; }
	private readonly RenderGraph renderGraph;
	private readonly ResizableArray<byte> buffer = new();

	public ConstantBufferBuilder(RenderGraph renderGraph)
	{
		this.renderGraph = renderGraph;
	}

	public void AddData<T>(Span<T> data) where T : unmanaged
	{
		var bytes = MemoryMarshal.AsBytes(data);
		_ = buffer.AddRange(bytes);
	}

	public void AddData<T>(T data) where T : unmanaged
	{
		AddData(stackalloc[] { data });
	}

	void IDisposable.Dispose()
	{
		var handle = renderGraph.GetBuffer(new(1, buffer.Count, GraphicsBuffer.Target.Constant), Shader.PropertyToID(PropertyName));
		var range = renderGraph.AddConstantBufferData(buffer.AsSpan());

		using var pass = renderGraph.AddRenderPass("Set Constant Buffer");
		pass.AddUavOutput(handle);

		pass.SetRenderFunction((handle, renderGraph, range), static (command, data) =>
		{
			var buffer = data.renderGraph.GetBufferResource(data.handle);
			command.SetBufferData(buffer, data.renderGraph.GetConstantBufferData(data.range).AsArray());
		});

		buffer.Clear();
	}
}