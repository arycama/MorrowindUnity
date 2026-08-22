using System.Collections.Generic;
using UnityEngine.Rendering;
using Unmath;

public interface IRenderPass
{
	int Index { get; }
	bool BeginRenderPass { get; }
	Int2 Size { get; }
	int Samples { get; }
	string Name { get; }
	List<(TextureHandle handle, int propertyId)> Inputs { get; }
	List<(TextureHandle handle, bool dontResolve)> Outputs { get; }

	void Execute(CommandBuffer command);
}
