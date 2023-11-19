using System;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Custom Render Pipeline Asset")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] private bool enableSrpBatcher;
    [SerializeField] private ShadowSettings shadowSettings;
    [SerializeField] private ClusteredLightCulling.Settings clusteredLightingSettings;
    [SerializeField] private TemporalAA.Settings temporalAASettings;
    [SerializeField] private ConvolutionBloom.Settings convolutionBloomSettings;
    [SerializeField] private Bloom.Settings bloomSettings;

    [Header("Volumetric Lighting")]
    [SerializeField] private int tileSize = 8;
    [SerializeField] private int depthSlices = 128;
    [SerializeField, Range(0.0f, 2.0f)] private float blurSigma = 1.0f;
    [SerializeField] private bool nonLinearDepth = true;

    public Color waterAlbedo = Color.white;
    [ColorUsage(false, true)] public Color waterExtinction = Color.grey;

    public DepthOfField.Settings depthOfFieldSettings;

    public int TileSize => tileSize;
    public int DepthSlices => depthSlices;
    public float BlurSigma => blurSigma;
    public bool NonLinearDepth => nonLinearDepth;

    public bool EnableSrpBatcher => enableSrpBatcher;
    public ShadowSettings ShadowSettings => shadowSettings;
    public ClusteredLightCulling.Settings ClusteredLightingSettings => clusteredLightingSettings;
    public TemporalAA.Settings TemporalAASettings => temporalAASettings;
    public ConvolutionBloom.Settings ConvolutionBloomSettings => convolutionBloomSettings;
    public Bloom.Settings BloomSettings => bloomSettings;

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(this);
    }

    protected override void OnValidate()
    {
        //base.OnValidate();
    }
}
