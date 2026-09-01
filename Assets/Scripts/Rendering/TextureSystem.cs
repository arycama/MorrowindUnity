using System.Collections.Generic;
using UnityEngine;

public class TextureSystem
{
	private readonly RenderGraph renderGraph;
	private readonly Dictionary<TextureHandle, RenderTexture> activeTargets = new();

	public TextureSystem(RenderGraph renderGraph)
	{
		this.renderGraph = renderGraph;
	}

	public RenderTexture GetResource(TextureHandle handle, RenderTargetDescriptor descriptor, ViewHandle viewHandle, int samples = 1, bool isUav = false)
	{
		var viewInfo = renderGraph.GetViewInfo(viewHandle);
		var resource = RenderTexture.GetTemporary(descriptor.GetRenderTextureDescriptor(viewInfo, samples, isUav));
		_ = resource.Create();

		var wasAdded = activeTargets.TryAdd(handle, resource);
		if (!wasAdded)
			Debug.LogError($"Adding an already active texture {handle} {descriptor}");

		return resource;
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
	}
}
