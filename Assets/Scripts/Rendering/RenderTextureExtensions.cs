using UnityEngine;
using UnityEngine.Experimental.Rendering;

public static class RenderTextureExtensions
{
    public static void Clear(this RenderTexture renderTexture, bool clearDepth, bool clearColor, Color backgroundColor, float depth = 1.0f)
    {
        var previous = RenderTexture.active;
        Graphics.SetRenderTarget(renderTexture);
        GL.Clear(clearDepth, clearColor, backgroundColor, depth);
        RenderTexture.active = previous;
    }

    public static void ToTexture2D(this RenderTexture renderTexture, Texture2D texture)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        RenderTexture.active = previous;
    }

    public static Texture2D ToTexture2D(this RenderTexture renderTexture)
    {
        var texture = new Texture2D(renderTexture.width, renderTexture.height, renderTexture.graphicsFormat, renderTexture.useMipMap ? TextureCreationFlags.MipChain : TextureCreationFlags.None);
        ToTexture2D(renderTexture, texture);
        return texture;
    }

    /// <summary>
    /// Convenience function to ensure a RenderTexture is always created. (Eg when using GetTemporary and ComputeShaders)
    /// </summary>
    /// <param name="renderTexture">The RenderTexture to Create</param>
    /// <returns>The created RenderTExture</returns>
    public static RenderTexture Created(this RenderTexture renderTexture)
    {
        if (!renderTexture.IsCreated())
        {
            renderTexture.Create();
        }

        return renderTexture;
    }

    /// <summary>
    /// Compares a RenderTexture to a descriptor, and re-creates it if any parameterse change.
    /// </summary>
    /// <param name="renderTexture"></param>
    /// <param name="descriptor"></param>
    public static void CheckRenderTexture(ref RenderTexture renderTexture, RenderTextureDescriptor descriptor)
    {
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(descriptor);
        }
        else if (!renderTexture.descriptor.Equals(descriptor))
        {
            renderTexture.Release();
            renderTexture.descriptor = descriptor;
            renderTexture.Create();
        }
    }

    public static void Resize(this RenderTexture renderTexture, int width, int height, int depth, out bool hasChanged)
    {
        if (renderTexture.width != width || renderTexture.height != height || renderTexture.volumeDepth != depth)
        {
            renderTexture.Release();
            renderTexture.width = width;
            renderTexture.height = height;
            renderTexture.volumeDepth = depth;
            renderTexture.Create();
            hasChanged = true;
        }
        else
        {
            hasChanged = false;
        }
    }

    public static void Resize(this RenderTexture renderTexture, int width, int height, int depth)
    {
        Resize(renderTexture, width, height, depth, out _);
    }


    public static void Resize(this RenderTexture renderTexture, int width, int height, out bool hasChanged)
    {
        Resize(renderTexture, width, height, 1, out hasChanged);
    }

    public static void Resize(this RenderTexture renderTexture, int width, int height)
    {
        Resize(renderTexture, width, height, out _);
    }
}