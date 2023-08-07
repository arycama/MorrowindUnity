using UnityEngine;
using UnityEngine.Rendering;

public class ShadowRenderer
{
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m)
    {
        if (SystemInfo.usesReversedZBuffer)
            m.SetRow(2, -m.GetRow(2));

        m.SetRow(0, 0.5f * (m.GetRow(0) + m.GetRow(3)));
        m.SetRow(1, 0.5f * (m.GetRow(1) + m.GetRow(3)));
        m.SetRow(2, 0.5f * (m.GetRow(2) + m.GetRow(3)));
        return m;
    }

    public void Render(CommandBuffer commandBuffer, ref ScriptableRenderContext context, int resolution, ref CullingResults cullingResults, int index, Light light, float shadowBias, float shadowSlopeBias)
    {
        if (!cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(index, 0, 1, Vector3.zero, resolution, light.shadowNearPlane, out var viewMatrix, out var projectionMatrix, out var shadowSplitData))
            return;

        commandBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        commandBuffer.SetGlobalFloat("_ZClip", 0);
        commandBuffer.SetGlobalDepthBias(shadowBias, shadowSlopeBias);

        var directionalShadowsId = Shader.PropertyToID("_DirectionalShadows");
        commandBuffer.GetTemporaryRT(directionalShadowsId, resolution, resolution, 16, FilterMode.Point, RenderTextureFormat.Shadowmap);
        commandBuffer.SetRenderTarget(directionalShadowsId);
        commandBuffer.ClearRenderTarget(true, false, Color.clear);
        context.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();

        var shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, index) { splitData = shadowSplitData };
        context.DrawShadows(ref shadowDrawingSettings);

        commandBuffer.SetGlobalTexture("_DirectionalShadows", directionalShadowsId);

        var worldToShadow = ConvertToAtlasMatrix(projectionMatrix * viewMatrix);
        commandBuffer.SetGlobalMatrix("_WorldToShadow", worldToShadow);
        commandBuffer.SetGlobalFloat("_ZClip", 1);
        commandBuffer.SetGlobalDepthBias(0f, 0f);
    }
}
