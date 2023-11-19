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

    public static Vector4 ThreadIdScaleOffset(int width, int height)
    {
        return new Vector4((float)(1.0 / width), (float)(1.0 / height), (float)(0.5 / width), (float)(0.5 / height));
    }
}