using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public class CameraTextureCache : IDisposable
{
    private readonly string name;
    private readonly Dictionary<Camera, (RenderTexture, RenderTexture)> cameraTextureCache = new();
    private bool disposedValue;

    public CameraTextureCache(string name = null)
    {
        this.name = name;
    }

    public bool GetTexture(Camera camera, RenderTextureDescriptor descriptor, out RenderTexture texture0, out RenderTexture texture1, int frameCount)
    {
        bool wasCreated;
        if (!cameraTextureCache.TryGetValue(camera, out var textures))
        {
            var textureA = new RenderTexture(descriptor)
            {
                name = $"{name} {camera.name} 0",
                hideFlags = HideFlags.HideAndDontSave
            }.Created();

            var textureB = new RenderTexture(descriptor)
            {
                name = $"{name} {camera.name} 1",
                hideFlags = HideFlags.HideAndDontSave
            }.Created();

            textures = (textureA, textureB);
            cameraTextureCache.Add(camera, textures);
            wasCreated = true;
        }
        else
        {
            // Resize if needed
            textures.Item1.Resize(descriptor.width, descriptor.height, descriptor.volumeDepth);
            textures.Item2.Resize(descriptor.width, descriptor.height, descriptor.volumeDepth);

            // If already exists, swap textures
            if ((frameCount & 1) == 0)
                textures = (textures.Item2, textures.Item1);

            wasCreated = false;
        }

        // Stupid renderdoc thing where textures can become uncreated..
        if (!textures.Item1.IsCreated())
            textures.Item1.Create();

        if (!textures.Item2.IsCreated())
            textures.Item2.Create();

        texture0 = textures.Item1;
        texture1 = textures.Item2;

        return wasCreated;
    }

    protected virtual void Dispose(bool disposing)
    {
        foreach (var data in cameraTextureCache)
        {
            Object.DestroyImmediate(data.Value.Item1);
            Object.DestroyImmediate(data.Value.Item2);
        }

        if (!disposedValue)
        {
            if (disposing)
            {
                cameraTextureCache.Clear();
            }
            else
            {
                Debug.LogError($"GarbageCollector disposing of {nameof(CameraTextureCache)} [{name}]. Please use .Dispose() to manually release.");
            }

            disposedValue = true;
        }
    }

    ~CameraTextureCache()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
