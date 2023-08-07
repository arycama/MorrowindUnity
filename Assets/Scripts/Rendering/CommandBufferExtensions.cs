using UnityEngine;
using UnityEngine.Rendering;

public static class CommandBufferExtensions
{
    public static void DispatchNormalized(this CommandBuffer commandBuffer, ComputeShader computeShader, int kernelIndex, int threadsX, int threadsY, int threadsZ)
    {
        computeShader.GetKernelThreadGroupSizes(kernelIndex, out var x, out var y, out var z);

        var threadGroupsX = (int)((threadsX - 1) / x) + 1;
        var threadGroupsY = (int)((threadsY - 1) / y) + 1;
        var threadGroupsZ = (int)((threadsZ - 1) / z) + 1;

        commandBuffer.DispatchCompute(computeShader, kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
    }
}
