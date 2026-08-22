using System.Collections.Generic;
using UnityEngine.Rendering;
using Unmath;

public interface IRenderPass
{
	bool beginRenderPass { get; }
	Int2 size { get; }
	int samples { get; }
	string name { get; }
	List<(TextureHandle handle, int propertyId)> inputs { get; }
	List<(TextureHandle handle, bool dontResolve)> outputs { get; }

	void Execute(CommandBuffer command);
}
