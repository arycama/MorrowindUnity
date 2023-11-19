using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
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

    public static void ExpandAndSetComputeBufferData<T>(this CommandBuffer command, ref ComputeBuffer computeBuffer, List<T> data, ComputeBufferType type = ComputeBufferType.Default) where T : struct
    {
        var size = Mathf.Max(data.Count, 1);

        if (computeBuffer == null || computeBuffer.count < size)
        {
            if (computeBuffer != null)
                computeBuffer.Release();

            var stride = UnsafeUtility.SizeOf<T>();
            computeBuffer = new ComputeBuffer(size, stride, type);
        }

        command.SetBufferData(computeBuffer, data);
    }

    public static CommandBufferProfilerScope BeginScopedSample(this CommandBuffer command, string name)
    {
        return new CommandBufferProfilerScope(command, name);
    }
}
