using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class LightingSetup
{
    private readonly MorrowindRenderPipelineAsset renderPipelineAsset;
    private readonly ShadowRenderer shadowRenderer;
    private readonly List<PointLightData> pointLightList;
    private ComputeBuffer pointLightBuffer; // Can't be readonly as we resize if needed.

    public LightingSetup(MorrowindRenderPipelineAsset renderPipelineAsset)
    {
        this.renderPipelineAsset = renderPipelineAsset;
        shadowRenderer = new();
        pointLightList = new();
        pointLightBuffer = new ComputeBuffer(1, 32);
    }

    public void Release()
    {
        pointLightBuffer.Release();
    }

    public void Render(CommandBuffer commandBuffer, CullingResults cullingResults, ScriptableRenderContext context)
    {
        pointLightList.Clear();

        commandBuffer.SetGlobalTexture("_DirectionalShadows", Texture2D.blackTexture);

        commandBuffer.SetGlobalVector("_SunDirection", Vector3.up);
        commandBuffer.SetGlobalVector("_SunColor", Color.black);
        commandBuffer.SetGlobalFloat("_SunShadowsOn", 0.0f);

        // Setup lights/shadows
        for (var i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            var visibleLight = cullingResults.visibleLights[i];
            if (visibleLight.lightType == LightType.Directional)
            {
                var light = visibleLight.light;
                commandBuffer.SetGlobalVector("_SunDirection", -light.transform.forward);
                commandBuffer.SetGlobalVector("_SunColor", visibleLight.light.color.linear);
                commandBuffer.SetGlobalFloat("_SunShadowsOn", light.shadows == LightShadows.None ? 0.0f : 1.0f);
                context.ExecuteCommandBuffer(commandBuffer);
                commandBuffer.Clear();

                if (light.shadows != LightShadows.None)
                {
                    shadowRenderer.Render(commandBuffer, ref context, renderPipelineAsset.ShadowResolution, ref cullingResults, i, light, renderPipelineAsset.ShadowBias, renderPipelineAsset.ShadowSlopeBias);
                    context.ExecuteCommandBuffer(commandBuffer);
                    commandBuffer.Clear();
                }
            }
            else if (visibleLight.lightType == LightType.Point)
            {
                pointLightList.Add(new PointLightData(visibleLight.localToWorldMatrix.GetPosition(), visibleLight.range, (Vector4)visibleLight.light.color.linear, uint.MaxValue));
            }
        }

        if (pointLightList.Count >= pointLightBuffer.count)
        {
            pointLightBuffer.Release();
            pointLightBuffer = new ComputeBuffer(pointLightList.Count, 32);
        }

        commandBuffer.SetBufferData(pointLightBuffer, pointLightList);

        // Pre-object render setup
        commandBuffer.SetGlobalBuffer("_PointLights", pointLightBuffer);
        commandBuffer.SetGlobalInt("_PointLightCount", pointLightList.Count);
    }
}
