using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRenderPipeline
{
    public readonly struct PointLightData : IRenderPassData
    {
        private readonly ResourceHandle<GraphicsBuffer> dataBuffer, lightBuffer, lightDepthBinBuffer, lightDepthMinMaxBuffer;
        public readonly int lightCount;

        public PointLightData(ResourceHandle<GraphicsBuffer> dataBuffer, ResourceHandle<GraphicsBuffer> lightBuffer, int lightCount, ResourceHandle<GraphicsBuffer> lightDepthBinBuffer, ResourceHandle<GraphicsBuffer> lightDepthMinMaxBuffer)
        {
            this.dataBuffer = dataBuffer;
            this.lightBuffer = lightBuffer;
            this.lightCount = lightCount;
            this.lightDepthBinBuffer = lightDepthBinBuffer;
            this.lightDepthMinMaxBuffer = lightDepthMinMaxBuffer;
		}

        void IRenderPassData.SetInputs(RenderPass pass)
        {
            pass.ReadBuffer("PointLightData", dataBuffer);
            pass.ReadBuffer("PointLights", lightBuffer);
            pass.ReadBuffer("LightDepthBins", lightDepthBinBuffer);
            pass.ReadBuffer("LightDepthMinMax", lightDepthMinMaxBuffer);
        }
		

		void IRenderPassData.SetProperties(RenderPass pass, CommandBuffer command)
        {
        }
    }
}