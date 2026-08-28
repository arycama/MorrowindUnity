using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class TextureHandleSystem
{
	public HashSet<int> activeTargets = new();

	public void GetTemporaryRT(CommandBuffer command, int nameID, RenderTargetDescriptor descriptor, ViewInfo viewInfo, int samples = 1)
	{
		var wasAdded = activeTargets.Add(nameID);
		if (!wasAdded)
			Debug.LogError($"Adding an already active texture {nameID} {descriptor}");

		command.GetTemporaryRT(nameID, descriptor.GetRenderTextureDescriptor(samples, viewInfo));
	}

	public void ReleaseTemporaryRT(CommandBuffer command, int nameID)
	{
		var wasRemoved = activeTargets.Remove(nameID);
		if (!wasRemoved)
			Debug.LogError($"Removing a texture {nameID} that was not active");

		command.ReleaseTemporaryRT(nameID);
	}

	public void ReleaseRemainingTargets(CommandBuffer command)
	{
		foreach (var target in activeTargets)
		{
			Debug.LogError($"Texture {target} was not released during frame");
			command.ReleaseTemporaryRT(target);
		}

		activeTargets.Clear();
	}
}