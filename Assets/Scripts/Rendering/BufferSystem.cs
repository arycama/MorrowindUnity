using System;
using System.Collections.Generic;
using UnityEngine;

public class BufferSystem : IDisposable
{
	private readonly Dictionary<BufferHandle, int> activeBuffers = new();
	private readonly List<int> availableBuffers = new();
	private readonly List<GraphicsBuffer> buffers = new();
	private readonly ResizableArray<BufferDescriptor> descriptors = new();

	public GraphicsBuffer GetBuffer(int index)
	{
		return buffers[index];
	}

	public int AddDescriptor(BufferDescriptor descriptor)
	{
		var descriptorIndex = descriptors.Count;
		descriptors.Add(descriptor);
		return descriptorIndex;
	}

	public int AllocateBuffer(BufferHandle handle, int descriptorIndex)
	{
		var descriptor = descriptors[descriptorIndex];
		var resourceIndex = -1;
		GraphicsBuffer resource = null;
		for (var i = 0; i < availableBuffers.Count; i++)
		{
			var bufferIndex = availableBuffers[i];
			var buffer = buffers[bufferIndex];
			if (buffer.stride != descriptor.stride)
				continue;

			if (buffer.count != descriptor.count)
				continue;

			if (buffer.target != descriptor.target)
				continue;

			if (buffer.usageFlags != descriptor.usageFlags)
				continue;

			resource = buffer;
			resourceIndex = bufferIndex;
			availableBuffers.RemoveAt(i);
			break;
		}

		if (resource == null)
		{
			resource = new GraphicsBuffer(descriptor.target, descriptor.usageFlags, descriptor.count, descriptor.stride);
			resourceIndex = buffers.Count;
			buffers.Add(resource);
		}

		var wasAdded = activeBuffers.TryAdd(handle, resourceIndex);
		if (!wasAdded)
			Debug.LogError($"Adding an already active Buffer {handle} {descriptor}");

		return resourceIndex;
	}

	public void ReleaseResource(BufferHandle handle)
	{
		if (!activeBuffers.TryGetValue(handle, out var resource))
		{
			Debug.LogError($"Removing a Buffer {handle} that was not active");
			return;
		}

		_ = activeBuffers.Remove(handle);
		availableBuffers.Add(resource);
	}

	public void FreeUnreleasedResources()
	{
		foreach (var buffer in activeBuffers)
		{
			Debug.LogError($"Buffer {buffer.Value} was not released during frame");
			availableBuffers.Add(buffer.Value);
		}

		activeBuffers.Clear();
		descriptors.Clear();
	}

	public void Dispose()
	{
		foreach (var buffer in buffers)
			buffer.Release();
	}
}