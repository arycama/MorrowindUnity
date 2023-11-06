using System;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Morrowind Render Pipeline Asset")]
public class MorrowindRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] private bool enableSrpBatcher;
    [SerializeField] private ShadowSettings shadowSettings;
    [SerializeField] private ClusteredLightCulling.Settings clusteredLightingSettings;
    [SerializeField] private TemporalAA.Settings temporalAASettings;
    [SerializeField] private ConvolutionBloom.Settings convolutionBloomSettings;

    [Header("Volumetric Lighting")]
    [SerializeField] private int tileSize = 8;
    [SerializeField] private int depthSlices = 128;
    [SerializeField, Range(0.0f, 2.0f)] private float blurSigma = 1.0f;
    [SerializeField] private bool nonLinearDepth = true;

    public int TileSize => tileSize;
    public int DepthSlices => depthSlices;
    public float BlurSigma => blurSigma;
    public bool NonLinearDepth => nonLinearDepth;

    public bool EnableSrpBatcher => enableSrpBatcher;
    public ShadowSettings ShadowSettings => shadowSettings;
    public ClusteredLightCulling.Settings ClusteredLightingSettings => clusteredLightingSettings;
    public TemporalAA.Settings TemporalAASettings => temporalAASettings;
    public ConvolutionBloom.Settings ConvolutionBloomSettings => convolutionBloomSettings;

    protected override RenderPipeline CreatePipeline()
    {
        return new MorrowindRenderPipeline(this);
    }
}
