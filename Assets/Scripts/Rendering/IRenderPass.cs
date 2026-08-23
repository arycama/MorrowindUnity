using System.Collections.Generic;
using UnityEngine.Rendering;
using Unmath;

public interface IRenderPass
{
	string Name { get; }
	int Index { get; }
	bool IsNativeRenderPass { get; }
	Int2 Size { get; }
	int Samples { get; }
	List<(TextureHandle handle, int propertyId)> Inputs { get; }
	List<TextureHandle> Outputs { get; }

	void Execute(CommandBuffer command);
}
