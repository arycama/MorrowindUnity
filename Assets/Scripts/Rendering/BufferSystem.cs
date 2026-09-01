using System;
using System.Collections.Generic;
using UnityEngine;

public class BufferSystem : IDisposable
{
	private readonly Dictionary<BufferHandle, GraphicsBuffer> activeBuffers = new();
	private readonly List<GraphicsBuffer> buffers = new();

	public GraphicsBuffer GetBuffer(BufferHandle handle, BufferDescriptor descriptor)
	{
		GraphicsBuffer resource = null;
		for (var i = 0; i < buffers.Count; i++)
		{
			var buffer = buffers[i];
			if (buffer.stride != descriptor.stride)
				continue;

			if (buffer.count != descriptor.count)
				continue;

			if (buffer.target != descriptor.target)
				continue;

			if (buffer.usageFlags != descriptor.usageFlags)
				continue;

			resource = buffer;
			buffers.RemoveAt(i);
			break;
		}

		if (resource == null)
			resource = new GraphicsBuffer(descriptor.target, descriptor.usageFlags, descriptor.count, descriptor.stride);

		var wasAdded = activeBuffers.TryAdd(handle, resource);
		if (!wasAdded)
			Debug.LogError($"Adding an already active Buffer {handle} {descriptor}");

		return resource;
	}

	public void ReleaseResource(BufferHandle handle)
	{
		if (!activeBuffers.TryGetValue(handle, out var resource))
		{
			Debug.LogError($"Removing a Buffer {handle} that was not active");
			return;
		}

		_ = activeBuffers.Remove(handle);
		buffers.Add(resource);
	}

	public void FreeUnreleasedResources()
	{
		foreach (var buffer in activeBuffers)
		{
			Debug.LogError($"Buffer {buffer.Value} was not released during frame");
			buffers.Add(buffer.Value);
		}

		activeBuffers.Clear();
	}

	public void Dispose()
	{
		foreach (var buffer in buffers)
			buffer.Release();
	}
}