using UnityEngine.Rendering;

public interface IRenderPass
{
	bool IsNativeRenderPass { get; }
	void Execute(CommandBuffer command);
}
