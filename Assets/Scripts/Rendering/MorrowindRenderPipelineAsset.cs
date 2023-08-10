using System;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Morrowind Render Pipeline Asset")]
public class MorrowindRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] private ShadowSettings shadowSettings;

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

    protected override RenderPipeline CreatePipeline()
    {
        return new MorrowindRenderPipeline(this);
    }
}

[Serializable]
public class ShadowSettings
{
    [SerializeField, Range(1, 4)] private int shadowCascades = 1;
    [SerializeField] private Vector3 shadowCascadeSplits = new Vector3(0.25f, 0.5f, 0.75f);
    [SerializeField] private float shadowDistance = 4096;
    [SerializeField] private int directionalShadowResolution = 2048;
    [SerializeField] private float shadowBias = 0.0f;
    [SerializeField] private float shadowSlopeBias = 0.0f;
    [SerializeField] private int pointShadowResolution = 256;

    public int ShadowCascades => shadowCascades;
    public Vector3 ShadowCascadeSplits => shadowCascadeSplits;
    public float ShadowDistance => shadowDistance;
    public int DirectionalShadowResolution => directionalShadowResolution;
    public float ShadowBias => shadowBias;
    public float ShadowSlopeBias => shadowSlopeBias;
    public int PointShadowResolution => pointShadowResolution;
}
