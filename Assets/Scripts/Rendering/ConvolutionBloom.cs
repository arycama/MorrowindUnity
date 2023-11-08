using System;
using UnityEngine;
using UnityEngine.Rendering;

public class ConvolutionBloom
{
    [Serializable]
    public class Settings
    {
        [SerializeField] public Texture2D KernelTexture = null;
        [SerializeField, Min(0f)] public float Intensity = 1.0f;
        [SerializeField, Min(0f)] public float Threshold = 0.0f;
        [SerializeField] public Vector2 KernelPositionOffset = new Vector2(0, 0);
        [SerializeField] public Vector2 KernelSizeScale = new Vector2(1, 1);
        [SerializeField] public float KernelDistanceExp = 0.0f;
        [SerializeField] public float KernelDistanceExpClampMin = 1.0f;
        [SerializeField] public float KernelDistanceExpScale = 1.0f;
        [SerializeField] public bool KernelImageUseLuminanceAsRGB = false;
    }

    private Settings settings;
    private Material sourceGenerateMaterial, kernelGenerateMaterial, finalBlitMaterial;
    private ComputeShader cs;

    public ConvolutionBloom(Settings settings)
    {
        this.settings = settings;
        kernelGenerateMaterial = new Material(Shader.Find("ConvolutionBloom/KernelGenerate")) { hideFlags = HideFlags.HideAndDontSave };
        sourceGenerateMaterial = new Material(Shader.Find("ConvolutionBloom/SourceGenerate")) { hideFlags = HideFlags.HideAndDontSave };
        finalBlitMaterial = new Material(Shader.Find("ConvolutionBloom/FinalBlit")) { hideFlags = HideFlags.HideAndDontSave };
        cs = Resources.Load<ComputeShader>("FFTCS");
    }

    public void Render(CommandBuffer command, RenderTargetIdentifier input, RenderTargetIdentifier result)
    {
        command.BeginSample("Convolution Bloom");

        command.SetGlobalFloat("FFTBloomIntensity", settings.Intensity);
        command.SetGlobalFloat("FFTBloomThreshold", settings.Threshold);
        Vector4 kernelGenParam = new Vector4(settings.KernelPositionOffset.x, settings.KernelPositionOffset.y, settings.KernelSizeScale.x, settings.KernelSizeScale.y);
        command.SetGlobalVector("FFTBloomKernelGenParam", kernelGenParam);
        Vector4 kernelGenParam1 = new Vector4(settings.KernelDistanceExp, settings.KernelDistanceExpClampMin, settings.KernelDistanceExpScale, settings.KernelImageUseLuminanceAsRGB ? 1.0f : 0.0f);
        command.SetGlobalVector("FFTBloomKernelGenParam1", kernelGenParam1);

        var desc = new RenderTextureDescriptor(512, 512, RenderTextureFormat.ARGBFloat) { enableRandomWrite = true };
        var desc1 = new RenderTextureDescriptor(512 * 2, 512 * 2, RenderTextureFormat.ARGBFloat) { enableRandomWrite = true };

        //command.Blit(settings.KernelTexture, m_kernelTexture, kernelGenerateMaterial);
        var m_kernelTexture = Shader.PropertyToID("m_kernelTexture");
        command.GetTemporaryRT(m_kernelTexture, desc);
        command.SetRenderTarget(m_kernelTexture);
        command.SetGlobalTexture("_MainTex", settings.KernelTexture);
        command.SetGlobalVector("_Resolution", new Vector2(512, 512));
        command.DrawProcedural(Matrix4x4.identity, kernelGenerateMaterial, 0, MeshTopology.Triangles, 3);

        int kKernelTransform = cs.FindKernel("KernelTransform");
        command.SetComputeTextureParam(cs, kKernelTransform, "SourceTexture", m_kernelTexture);
        command.DispatchCompute(cs, kKernelTransform, 512 / 8, 256 / 8, 1);

        //cmd.Blit(renderingData.cameraData.renderer.cameraColorTarget, m_sourceTexture, settings.SourceGenerateMaterial);
        var m_sourceTexture = Shader.PropertyToID("m_sourceTexture");
        command.GetTemporaryRT(m_sourceTexture, desc);
        command.SetRenderTarget(m_sourceTexture);
        command.SetGlobalTexture("_MainTex", input);
        command.SetGlobalVector("_Resolution", new Vector2(512, 512));
        command.DrawProcedural(Matrix4x4.identity, sourceGenerateMaterial, 0, MeshTopology.Triangles, 3);

        int kTwoForOneFFTForwardHorizontal = cs.FindKernel("TwoForOneFFTForwardHorizontal");
        command.SetComputeTextureParam(cs, kTwoForOneFFTForwardHorizontal, "SourceTexture", m_kernelTexture);

        var m_kernelFrequencyTextureID = Shader.PropertyToID("KernelFrequencyTexture");
        command.GetTemporaryRT(m_kernelFrequencyTextureID, desc1);
        command.SetComputeTextureParam(cs, kTwoForOneFFTForwardHorizontal, "FrequencyTexture", m_kernelFrequencyTextureID);
        command.DispatchCompute(cs, kTwoForOneFFTForwardHorizontal, 512, 1, 1);
        command.SetComputeFloatParam(cs, "IsForward", 1.0f);

        int kFFTVertical = cs.FindKernel("FFTVertical");
        command.SetComputeTextureParam(cs, kFFTVertical, "FrequencyTexture", m_kernelFrequencyTextureID);
        command.DispatchCompute(cs, kFFTVertical, 512, 1, 1);

        command.SetComputeTextureParam(cs, kTwoForOneFFTForwardHorizontal, "SourceTexture", m_sourceTexture);

        var m_sourceFrequencyTextureID = Shader.PropertyToID("SourceFrequencyTexture");
        command.GetTemporaryRT(m_sourceFrequencyTextureID, desc1);
        command.SetComputeTextureParam(cs, kTwoForOneFFTForwardHorizontal, "FrequencyTexture", m_sourceFrequencyTextureID);
        command.DispatchCompute(cs, kTwoForOneFFTForwardHorizontal, 512, 1, 1);
        command.SetComputeFloatParam(cs, "IsForward", 1.0f);
        command.SetComputeTextureParam(cs, kFFTVertical, "FrequencyTexture", m_sourceFrequencyTextureID);
        command.DispatchCompute(cs, kFFTVertical, 512, 1, 1);

        int kConvolution = cs.FindKernel("Convolution");
        command.SetComputeTextureParam(cs, kConvolution, "SourceFrequencyTexture", m_sourceFrequencyTextureID);
        command.SetComputeTextureParam(cs, kConvolution, "KernelFrequencyTexture", m_kernelFrequencyTextureID);
        command.DispatchCompute(cs, kConvolution, 512 / 8, 512 / 8, 1);

        command.SetComputeFloatParam(cs, "IsForward", 0.0f);
        command.SetComputeTextureParam(cs, kFFTVertical, "FrequencyTexture", m_sourceFrequencyTextureID);
        command.DispatchCompute(cs, kFFTVertical, 512, 1, 1);
        int kTwoForOneFFTInverseHorizontal = cs.FindKernel("TwoForOneFFTInverseHorizontal");
        command.SetComputeTextureParam(cs, kTwoForOneFFTInverseHorizontal, "FrequencyTexture", m_sourceFrequencyTextureID);
        command.DispatchCompute(cs, kTwoForOneFFTInverseHorizontal, 512, 1, 1);

        // final blit
        //command.Blit(m_sourceFrequencyTextureID, renderingData.cameraData.renderer.cameraColorTarget, settings.FinalBlitMaterial);
        command.SetRenderTarget(result);
        command.SetGlobalTexture("_Input", input);
        command.SetGlobalTexture("_MainTex", m_sourceFrequencyTextureID);
        command.SetGlobalVector("_Resolution", new Vector2(512, 512));
        command.DrawProcedural(Matrix4x4.identity, finalBlitMaterial, 0, MeshTopology.Triangles, 3);

        command.EndSample("Convolution Bloom");
    }
}
