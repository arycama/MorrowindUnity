using System;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Morrowind Render Pipeline Asset")]
public class MorrowindRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] private ShadowSettings shadowSettings;
    [SerializeField] private ClusteredLightingSettings clusteredLightingSettings;
    [SerializeField] private TemporalAASettings temporalAASettings;

    [Header("Volumetric Lighting")]
    [SerializeField] private int tileSize = 8;
    [SerializeField] private int depthSlices = 128;
    [SerializeField, Range(0.0f, 2.0f)] private float blurSigma = 1.0f;
    [SerializeField] private bool nonLinearDepth = true;

    public int TileSize => tileSize;
    public int DepthSlices => depthSlices;
    public float BlurSigma => blurSigma;
    public bool NonLinearDepth => nonLinearDepth;

    public ShadowSettings ShadowSettings => shadowSettings;
    public ClusteredLightingSettings ClusteredLightingSettings => clusteredLightingSettings;
    public TemporalAASettings TemporalAASettings => temporalAASettings;

    protected override RenderPipeline CreatePipeline()
    {
        return new MorrowindRenderPipeline(this);
    }
}
