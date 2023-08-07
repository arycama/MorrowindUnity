using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class ObjectRenderer
{
    private RenderQueueRange renderQueueRange;
    private SortingCriteria sortingCriteria;

    public ObjectRenderer(RenderQueueRange renderQueueRange, SortingCriteria sortingCriteria)
    {
        this.renderQueueRange = renderQueueRange;
        this.sortingCriteria = sortingCriteria;
    }

    public void Render(ref CullingResults cullingResults, Camera camera, CommandBuffer commandBuffer, ref ScriptableRenderContext context)
    {
        var srpDefaultUnlitShaderPassName = new ShaderTagId("SRPDefaultUnlit");
        var rendererListDesc = new RendererListDesc(srpDefaultUnlitShaderPassName, cullingResults, camera)
        {
            renderQueueRange = renderQueueRange,
            sortingCriteria = sortingCriteria
        };

        var rendererList = context.CreateRendererList(rendererListDesc);
        commandBuffer.DrawRendererList(rendererList);
    }
}
