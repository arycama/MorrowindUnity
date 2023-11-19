using System;
using UnityEngine;
using UnityEngine.Rendering;

public class ConvolutionBloom
{
    [Serializable]
    public class Settings
    {
        [SerializeField] private int resolution = 512;

        [Header("Kernel Generation")]
        [SerializeField] private Texture2D kernelTexture = null;
        [SerializeField, Min(0f)] private float kernelIntensity = 1.0f;
        [SerializeField] private Color kernelTint = Color.white;
        [SerializeField] private Vector2 kernelScale = Vector2.one;
        [SerializeField] private Vector2 kernelOffset = Vector2.zero;

        [SerializeField, Min(0f)] public float Intensity = 1.0f;
        [SerializeField, Min(0f)] public float Threshold = 0.0f;
        [SerializeField] public Vector2 KernelPositionOffset = new Vector2(0, 0);
        [SerializeField] public Vector2 KernelSizeScale = new Vector2(1, 1);
        [SerializeField] public float KernelDistanceExp = 0.0f;
        [SerializeField] public float KernelDistanceExpClampMin = 1.0f;
        [SerializeField] public float KernelDistanceExpScale = 1.0f;
        [SerializeField] public bool KernelImageUseLuminanceAsRGB = false;

        public int Resolution => resolution;
        public Texture2D KernelTexture => kernelTexture;
        public float KernelIntensity => kernelIntensity;
        public Color KernelTint => kernelTint;
        public Vector2 KernelScale => kernelScale;
        public Vector2 KernelOffset => kernelOffset;
    }

    private Settings settings;
    private Material finalBlitMaterial;
    private ComputeShader cs, fftBloom;

    public ConvolutionBloom(Settings settings)
    {
        this.settings = settings;
        finalBlitMaterial = new Material(Shader.Find("ConvolutionBloom/FinalBlit")) { hideFlags = HideFlags.HideAndDontSave };
        cs = Resources.Load<ComputeShader>("FFTCS");
        fftBloom = Resources.Load<ComputeShader>("FFTBloom");
    }

    public void Render(CommandBuffer command, RenderTargetIdentifier input, RenderTargetIdentifier result)
    {
        using var profilerScope = command.BeginScopedSample("Convolution Bloom");

        // Generate Kernel
        var kernelDesc = new RenderTextureDescriptor(settings.Resolution, settings.Resolution, RenderTextureFormat.RGB111110Float) { enableRandomWrite = true };
        var kernelTextureId = Shader.PropertyToID("_KernelTexture");
        command.GetTemporaryRT(kernelTextureId, kernelDesc);

        command.SetComputeTextureParam(fftBloom, 0, "_Input", settings.KernelTexture);
        command.SetComputeTextureParam(fftBloom, 0, "_Result", kernelTextureId);
        command.SetComputeVectorParam(fftBloom, "_Tint", settings.KernelTint.linear);
        command.SetComputeVectorParam(fftBloom, "_Scale", settings.KernelScale);
        command.SetComputeVectorParam(fftBloom, "_Offset", settings.KernelOffset);
        command.SetComputeFloatParam(fftBloom, "_Intensity", settings.KernelIntensity);
        command.SetComputeIntParam(fftBloom, "_Resolution", settings.Resolution);
        command.DispatchNormalized(fftBloom, 0, settings.Resolution, settings.Resolution, 1);

        // Transform kernel (Could probably be combined with above
        var kernelTransformTextureId = Shader.PropertyToID("_KernelTransformTexture");
        command.GetTemporaryRT(kernelTransformTextureId, kernelDesc);
        command.SetComputeTextureParam(fftBloom, 1, "_Input", kernelTextureId);
        command.SetComputeTextureParam(fftBloom, 1, "_Result", kernelTransformTextureId);
        command.SetComputeIntParam(fftBloom, "_Resolution", settings.Resolution);
        command.DispatchNormalized(fftBloom, 1, settings.Resolution, settings.Resolution, 1);

        // Generate Source texture
        var sourceTextureId = Shader.PropertyToID("_SourceTexture");
        command.GetTemporaryRT(sourceTextureId, kernelDesc);
        command.SetComputeTextureParam(fftBloom, 2, "_Input", input);
        command.SetComputeTextureParam(fftBloom, 2, "_Result", sourceTextureId);
        command.SetComputeIntParam(fftBloom, "_Resolution", settings.Resolution);
        command.DispatchNormalized(fftBloom, 2, settings.Resolution, settings.Resolution, 1);

        // um
        command.SetGlobalFloat("FFTBloomIntensity", settings.Intensity);
        command.SetGlobalFloat("FFTBloomThreshold", settings.Threshold);
        Vector4 kernelGenParam = new Vector4(settings.KernelPositionOffset.x, settings.KernelPositionOffset.y, settings.KernelSizeScale.x, settings.KernelSizeScale.y);
        command.SetGlobalVector("FFTBloomKernelGenParam", kernelGenParam);
        Vector4 kernelGenParam1 = new Vector4(settings.KernelDistanceExp, settings.KernelDistanceExpClampMin, settings.KernelDistanceExpScale, settings.KernelImageUseLuminanceAsRGB ? 1.0f : 0.0f);
        command.SetGlobalVector("FFTBloomKernelGenParam1", kernelGenParam1);

        var desc1 = new RenderTextureDescriptor(settings.Resolution * 2, settings.Resolution, RenderTextureFormat.ARGBFloat) { enableRandomWrite = true };
        command.SetGlobalVector("_Resolution", new Vector2(settings.Resolution, settings.Resolution));

        // Transform kernel into frequency domain
        int kTwoForOneFFTForwardHorizontal = cs.FindKernel("TwoForOneFFTForwardHorizontal");
        command.SetComputeTextureParam(cs, kTwoForOneFFTForwardHorizontal, "SourceTexture", kernelTransformTextureId);

        var m_kernelFrequencyTextureID = Shader.PropertyToID("KernelFrequencyTexture");
        command.GetTemporaryRT(m_kernelFrequencyTextureID, desc1);
        command.SetComputeTextureParam(cs, kTwoForOneFFTForwardHorizontal, "FrequencyTexture", m_kernelFrequencyTextureID);
        command.DispatchCompute(cs, kTwoForOneFFTForwardHorizontal, settings.Resolution, 1, 1);
        command.SetComputeFloatParam(cs, "IsForward", 1.0f);

        // Kernel vertical
        int kFFTVertical = cs.FindKernel("FFTVertical");
        command.SetComputeTextureParam(cs, kFFTVertical, "FrequencyTexture", m_kernelFrequencyTextureID);
        command.DispatchCompute(cs, kFFTVertical, settings.Resolution, 1, 1);

        // Transform source into frequency domain
        command.SetComputeTextureParam(cs, kTwoForOneFFTForwardHorizontal, "SourceTexture", sourceTextureId);

        var m_sourceFrequencyTextureID = Shader.PropertyToID("SourceFrequencyTexture");
        command.GetTemporaryRT(m_sourceFrequencyTextureID, desc1);
        command.SetComputeTextureParam(cs, kTwoForOneFFTForwardHorizontal, "FrequencyTexture", m_sourceFrequencyTextureID);
        command.DispatchCompute(cs, kTwoForOneFFTForwardHorizontal, settings.Resolution, 1, 1);

        // Source vertical
        command.SetComputeFloatParam(cs, "IsForward", 1.0f);
        command.SetComputeTextureParam(cs, kFFTVertical, "FrequencyTexture", m_sourceFrequencyTextureID);
        command.DispatchCompute(cs, kFFTVertical, settings.Resolution, 1, 1);

        // Convolve Source with Kernel
        int kConvolution = cs.FindKernel("Convolution");
        command.SetComputeTextureParam(cs, kConvolution, "SourceFrequencyTexture", m_sourceFrequencyTextureID);
        command.SetComputeTextureParam(cs, kConvolution, "KernelFrequencyTexture", m_kernelFrequencyTextureID);
        command.DispatchCompute(cs, kConvolution, settings.Resolution / 8, settings.Resolution / 8, 1);

        // Inverse fft
        command.SetComputeFloatParam(cs, "IsForward", 0.0f);
        command.SetComputeTextureParam(cs, kFFTVertical, "FrequencyTexture", m_sourceFrequencyTextureID);
        command.DispatchCompute(cs, kFFTVertical, settings.Resolution, 1, 1);

        // Final inverse
        int kTwoForOneFFTInverseHorizontal = cs.FindKernel("TwoForOneFFTInverseHorizontal");
        command.SetComputeTextureParam(cs, kTwoForOneFFTInverseHorizontal, "FrequencyTexture", m_sourceFrequencyTextureID);
        command.DispatchCompute(cs, kTwoForOneFFTInverseHorizontal, settings.Resolution, 1, 1);

        // Final combine
        command.SetRenderTarget(result);
        command.SetGlobalTexture("_Input", input);
        command.SetGlobalTexture("_MainTex", m_sourceFrequencyTextureID);
        command.SetGlobalVector("_Resolution", new Vector2(settings.Resolution, settings.Resolution));
        command.DrawProcedural(Matrix4x4.identity, finalBlitMaterial, 0, MeshTopology.Triangles, 3);
    }
}
