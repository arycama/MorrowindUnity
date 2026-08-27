using UnityEngine.Rendering;

public interface IRenderPass
{

	void Execute(CommandBuffer command);
}
