using System;
using UnityEngine.Rendering;
using Unmath;

public interface IRenderPass
{
	bool IsNativeRenderPass { get; }
	Int2 Size { get; }
	int Samples { get; }

	void Execute(CommandBuffer command);
}
