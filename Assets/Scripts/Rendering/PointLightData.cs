using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRenderPipeline
{
    public readonly struct PointLightData : IRenderPassData
    {
        private readonly ResourceHandle<GraphicsBuffer> dataBuffer, lightBuffer;
        public readonly int lightCount;

        public PointLightData(ResourceHandle<GraphicsBuffer> dataBuffer, ResourceHandle<GraphicsBuffer> lightBuffer, int lightCount)
        {
            this.dataBuffer = dataBuffer;
            this.lightBuffer = lightBuffer;
            this.lightCount = lightCount;
        }

        void IRenderPassData.SetInputs(RenderPass pass)
        {
            pass.ReadBuffer("PointLightData", dataBuffer);
            pass.ReadBuffer("PointLights", lightBuffer);
        }

        void IRenderPassData.SetProperties(RenderPass pass, CommandBuffer command)
        {
        }
    }
}