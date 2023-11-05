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

    private Vector4 jitter;

    public void OnPreRender(Camera camera, int frameCount, CommandBuffer command)
    {
        camera.nonJitteredProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;

        var sampleIndex = frameCount % settings.SampleCount;
        jitter.x = Halton(sampleIndex, 2) - 0.5f;
        jitter.y = Halton(sampleIndex, 3) - 0.5f;
        jitter *= settings.JitterSpread;

        var matrix = camera.projectionMatrix;
        matrix[0, 2] = 2.0f * jitter.x / camera.pixelWidth;
        matrix[1, 2] = 2.0f * jitter.y / camera.pixelHeight;
        camera.projectionMatrix = matrix;

        command.SetGlobalVector("_Jitter", jitter);
    }

    public RenderTargetIdentifier Render(Camera camera, CommandBuffer command, int frameCount, RenderTargetIdentifier input, RenderTargetIdentifier motion, RenderTargetIdentifier depth)
    {
        var descriptor = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, RenderTextureFormat.RGB111110Float);
        textureCache.GetTexture(camera, descriptor, out var current, out var previous, frameCount);

        propertyBlock.SetFloat("_Sharpness", settings.Sharpness);

        const float kMotionAmplification = 100f * 60f;
        propertyBlock.SetVector("_FinalBlendParameters", new Vector4(settings.StationaryBlending, settings.MotionBlending, kMotionAmplification, 0f));
        propertyBlock.SetTexture("_History", previous);

        command.SetGlobalTexture("_Input", input);
        command.SetGlobalTexture("_Motion", motion);
        command.SetGlobalTexture("_Depth", depth);

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
