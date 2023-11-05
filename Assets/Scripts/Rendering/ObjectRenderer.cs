using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class ObjectRenderer
{
    private RenderQueueRange renderQueueRange;
    private SortingCriteria sortingCriteria;
    private bool excludeObjectMotionVectors;
    private PerObjectData perObjectData;
    private string passName;

    public ObjectRenderer(RenderQueueRange renderQueueRange, SortingCriteria sortingCriteria, bool excludeObjectMotionVectors, PerObjectData perObjectData, string passName)
    {
        this.renderQueueRange = renderQueueRange;
        this.sortingCriteria = sortingCriteria;
        this.excludeObjectMotionVectors = excludeObjectMotionVectors;
        this.passName = passName;
        this.perObjectData = perObjectData;
    }

    public void Render(ref CullingResults cullingResults, Camera camera, CommandBuffer commandBuffer, ref ScriptableRenderContext context)
    {
        var srpDefaultUnlitShaderPassName = new ShaderTagId(passName);
        var rendererListDesc = new RendererListDesc(srpDefaultUnlitShaderPassName, cullingResults, camera)
        {
            renderQueueRange = renderQueueRange,
            sortingCriteria = sortingCriteria,
            excludeObjectMotionVectors = excludeObjectMotionVectors,
            rendererConfiguration = perObjectData
        };

        var rendererList = context.CreateRendererList(rendererListDesc);
        commandBuffer.DrawRendererList(rendererList);
    }
}
