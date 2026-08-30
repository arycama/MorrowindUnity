using System.Collections.Generic;
using UnityEngine;

public class TextureHandleSystem
{
	private readonly RenderGraph renderGraph;
	private readonly Dictionary<int, RenderTexture> targets = new();

	public TextureHandleSystem(RenderGraph renderGraph)
	{
		this.renderGraph = renderGraph;
	}

	public RenderTexture GetTemporaryRT(int nameID, RenderTargetDescriptor descriptor, ViewHandle viewHandle, int samples = 1, bool isUav = false)
	{
		var viewInfo = renderGraph.GetViewInfo(viewHandle);
		var resource = RenderTexture.GetTemporary(descriptor.GetRenderTextureDescriptor(viewInfo, samples, isUav));
		_ = resource.Create();

		var wasAdded = targets.TryAdd(nameID, resource);
		if (!wasAdded)
			Debug.LogError($"Adding an already active texture {nameID} {descriptor}");

		return resource;
	}

	public void ReleaseTemporaryRT(int nameID)
	{
		if (!targets.TryGetValue(nameID, out var resource))
		{
			Debug.LogError($"Removing a texture {nameID} that was not active");
			return;
		}

		RenderTexture.ReleaseTemporary(resource);
		_ = targets.Remove(nameID);
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