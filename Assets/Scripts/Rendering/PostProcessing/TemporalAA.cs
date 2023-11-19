using System;
using UnityEngine;
using UnityEngine.Rendering;

public class TemporalAA
{
    [Serializable]
    public class Settings
    {
        [SerializeField, Range(1, 32)]
        private int sampleCount = 8;

        [SerializeField, Range(0.0f, 1f), Tooltip("The diameter (in texels) inside which jitter samples are spread. Smaller values result in crisper but more aliased output, while larger values result in more stable, but blurrier, output.")]
        private float jitterSpread = 0.75f;

        [SerializeField, Range(0f, 3f), Tooltip("Controls the amount of sharpening applied to the color buffer. High values may introduce dark-border artifacts.")]
        private float sharpness = 0.25f;

        [SerializeField, Range(0f, 0.99f), Tooltip("The blend coefficient for a stationary fragment. Controls the percentage of history sample blended into the final color.")]
        private float stationaryBlending = 0.95f;

        [SerializeField, Range(0f, 0.99f), Tooltip("The blend coefficient for a fragment with significant motion. Controls the percentage of history sample blended into the final color.")]
        private float motionBlending = 0.85f;

        public int SampleCount => sampleCount;
        public float JitterSpread => jitterSpread;
        public float Sharpness => sharpness;
        public float StationaryBlending => stationaryBlending;
        public float MotionBlending => motionBlending;
    }

    private Settings settings;
    private CameraTextureCache textureCache = new();
    private Material material;
    private MaterialPropertyBlock propertyBlock;

    public TemporalAA(Settings settings)
    {
        this.settings = settings;
        material = new Material(Shader.Find("Hidden/Temporal AA")) { hideFlags = HideFlags.HideAndDontSave };
        propertyBlock = new();
    }

    public void Release()
    {
        textureCache.Dispose();
    }

    public void OnPreRender(Camera camera, int frameCount, CommandBuffer command)
    {
        camera.ResetProjectionMatrix();
        camera.nonJitteredProjectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix;

        var sampleIndex = frameCount % settings.SampleCount;

        Vector2 jitter;
        jitter.x = Halton(sampleIndex, 2) - 0.5f;
        jitter.y = Halton(sampleIndex, 3) - 0.5f;
        jitter *= settings.JitterSpread;

        var matrix = camera.projectionMatrix;
        matrix[0, 2] = 2.0f * jitter.x / camera.pixelWidth;
        matrix[1, 2] = 2.0f * jitter.y / camera.pixelHeight;
        camera.projectionMatrix = matrix;

        command.SetGlobalVector("_Jitter", jitter);
    }

    public RenderTargetIdentifier Render(Camera camera, CommandBuffer command, int frameCount, RenderTargetIdentifier input, RenderTargetIdentifier motion)
    {
        using var profilerScope = command.BeginScopedSample("Temporal AA");

        var descriptor = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, RenderTextureFormat.RGB111110Float);
        var wasCreated = textureCache.GetTexture(camera, descriptor, out var current, out var previous, frameCount);

        propertyBlock.SetFloat("_Sharpness", settings.Sharpness);
        propertyBlock.SetFloat("_HasHistory", wasCreated ? 0f : 1f);

        const float kMotionAmplification = 100f * 60f;
        propertyBlock.SetVector("_FinalBlendParameters", new Vector4(settings.StationaryBlending, settings.MotionBlending, kMotionAmplification, 0f));
        propertyBlock.SetTexture("_History", previous);

        command.SetGlobalTexture("_Input", input);
        command.SetGlobalTexture("_Motion", motion);

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
