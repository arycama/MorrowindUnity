using UnityEngine;
using UnityEngine.Rendering;

public class TemporalAA
{
    private TemporalAASettings settings;
    private CameraTextureCache textureCache = new();
    private Material material;
    private MaterialPropertyBlock propertyBlock;

    public TemporalAA(TemporalAASettings settings)
    {
        this.settings = settings;
        material = new Material(Shader.Find("Hidden/Temporal AA"));
        propertyBlock = new();
    }

    public void Release()
    {
        textureCache.Dispose();
    }

    private Vector2 jitter;

    public void OnPreRender(Camera camera, int frameCount)
    {
        camera.nonJitteredProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;

        var sampleIndex = frameCount % settings.SampleCount;

        // The variance between 0 and the actual halton sequence values reveals noticeable instability
        // in Unity's shadow maps, so we avoid index 0.
        jitter = new Vector2(Halton((sampleIndex & 1023) + 1, 2) - 0.5f, Halton((sampleIndex & 1023) + 1, 3) - 0.5f);
        jitter *= settings.JitterSpread;

        float near = camera.nearClipPlane;
        float far = camera.farClipPlane;

        float vertical = Mathf.Tan(0.5f * Mathf.Deg2Rad * camera.fieldOfView) * near;
        float horizontal = vertical * camera.aspect;

        var offset = jitter;
        offset.x *= horizontal / (0.5f * camera.pixelWidth);
        offset.y *= vertical / (0.5f * camera.pixelHeight);

        var matrix = camera.projectionMatrix;
        matrix[0, 2] += offset.x / horizontal;
        matrix[1, 2] += offset.y / vertical;
        camera.projectionMatrix = matrix;

        jitter = new Vector2(jitter.x / camera.pixelWidth, jitter.y / camera.pixelHeight);
    }

    public RenderTargetIdentifier Render(Camera camera, CommandBuffer command, int frameCount, RenderTargetIdentifier input)
    {
        var descriptor = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, RenderTextureFormat.RGB111110Float);
        textureCache.GetTexture(camera, descriptor, out var current, out var previous, frameCount);

        propertyBlock.SetVector("_Jitter", jitter);
        propertyBlock.SetFloat("_Sharpness", settings.Sharpness);

        const float kMotionAmplification = 100f * 60f;
        propertyBlock.SetVector("_FinalBlendParameters", new Vector4(settings.StationaryBlending, settings.MotionBlending, kMotionAmplification, 0f));
        propertyBlock.SetTexture("_History", previous);

        command.SetGlobalTexture("_Input", input);

        command.SetRenderTarget(current);
        command.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, propertyBlock);
        return current;
    }

    public static float Halton(int index, int radix)
    {
        float result = 0f;
        float fraction = 1f / (float)radix;

        while (index > 0)
        {
            result += (float)(index % radix) * fraction;

            index /= radix;
            fraction /= (float)radix;
        }

        return result;
    }
}
