using System.Collections.Generic;
using UnityEngine;

public class TextureHandleSystem
{
	private readonly RenderGraph renderGraph;
	private readonly Dictionary<TextureHandle, RenderTexture> targets = new();

	public TextureHandleSystem(RenderGraph renderGraph)
	{
		this.renderGraph = renderGraph;
	}

	public RenderTexture GetTemporaryRT(TextureHandle handle, RenderTargetDescriptor descriptor, ViewHandle viewHandle, int samples = 1, bool isUav = false)
	{
		var viewInfo = renderGraph.GetViewInfo(viewHandle);
		var resource = RenderTexture.GetTemporary(descriptor.GetRenderTextureDescriptor(viewInfo, samples, isUav));
		_ = resource.Create();

		var wasAdded = targets.TryAdd(handle, resource);
		if (!wasAdded)
			Debug.LogError($"Adding an already active texture {handle} {descriptor}");

		return resource;
	}

	public void ReleaseTemporaryRT(TextureHandle handle)
	{
		if (!targets.TryGetValue(handle, out var resource))
		{
			Debug.LogError($"Removing a texture {handle} that was not active");
			return;
		}

		RenderTexture.ReleaseTemporary(resource);
		_ = targets.Remove(handle);
	}

	public void ReleaseRemainingTargets()
	{
		foreach (var target in targets)
		{
			Debug.LogError($"Texture {target} was not released during frame");
			RenderTexture.ReleaseTemporary(target.Value);
		}

		targets.Clear();
	}
}