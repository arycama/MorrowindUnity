using UnityEngine;

public static class GraphicsUtilities
{
    public static void SafeExpand(ref ComputeBuffer computeBuffer, int size = 1, int stride = sizeof(int), ComputeBufferType type = ComputeBufferType.Default)
    {
        size = Mathf.Max(size, 1);

        if (computeBuffer == null || computeBuffer.count < size)
        {
            if (computeBuffer != null)
                computeBuffer.Release();

            computeBuffer = new ComputeBuffer(size, stride, type);
        }
    }
}