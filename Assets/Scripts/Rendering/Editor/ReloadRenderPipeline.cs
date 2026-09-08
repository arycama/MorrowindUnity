using UnityEditor;
using UnityEngine.Rendering;

public class ReloadRenderPipeline
{
	[MenuItem("Tools/Reload Render Pipeline")]
	public static void OnReloadRenderPipelineSelected()
	{
		if (GraphicsSettings.currentRenderPipeline is RenderPipelineAsset renderPipelineAsset)
			renderPipelineAsset.ReloadRenderPipeline();
	}
}
