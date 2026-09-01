using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class TextureSystem
{
	private readonly Dictionary<TextureHandle, RenderTexture> activeTargets = new();
	private readonly List<RenderTargetIdentifier> renderTargets = new();
	private readonly ResizableArray<RenderTargetDescriptor> descriptors = new();

	public RenderTargetIdentifier GetTexture(int index)
	{
		return renderTargets[index];
	}

	public RenderTargetDescriptor GetDescriptor(int index)
	{
		return descriptors[index];
	}

	public int ExportTexture(RenderTargetIdentifier id)
	{
		var index = renderTargets.Count;
		renderTargets.Add(id);
		return index;
	}

	public int AddDescriptor(RenderTargetDescriptor descriptor)
	{
		var descriptorIndex = descriptors.Count;
		descriptors.Add(descriptor);
		return descriptorIndex;
	}

	public int AllocateTexture(TextureHandle handle, int descriptorIndex, ViewInfo viewInfo, int samples, bool isUav)
	{
		var descriptor = descriptors[descriptorIndex];
		var resource = RenderTexture.GetTemporary(descriptor.GetRenderTextureDescriptor(viewInfo, samples, isUav));

		if (!resource.IsCreated())
			_ = resource.Create();

		var wasAdded = activeTargets.TryAdd(handle, resource);
		if (!wasAdded)
			Debug.LogError($"Adding an already active texture {handle} {descriptor}");

		var index = renderTargets.Count;
		renderTargets.Add(resource);
		return index;
	}

	public void ReleaseResource(TextureHandle handle)
	{
		if (!activeTargets.TryGetValue(handle, out var resource))
		{
			Debug.LogError($"Removing a texture {handle} that was not active");
			return;
		}

		_ = activeTargets.Remove(handle);
		RenderTexture.ReleaseTemporary(resource);
	}

	public void FreeUnreleasedResources()
	{
		foreach (var target in activeTargets)
		{
			Debug.LogError($"Texture {target} was not released during frame");
			RenderTexture.ReleaseTemporary(target.Value);
		}

		activeTargets.Clear();
		renderTargets.Clear();
		descriptors.Clear();
	}
}
