using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Morrowind Render Pipeline Asset")]
public class MorrowindRenderPipelineAsset : RenderPipelineAsset
{
    [Header("Shadows")]
    [SerializeField] private float shadowDistance = 4096;
    [SerializeField] private int shadowResolution = 2048;
    [SerializeField] private float shadowBias = 0.0f;
    [SerializeField] private float shadowSlopeBias = 0.0f;

    [Header("Volumetric Lighting")]
    [SerializeField] private int tileSize = 8;
    [SerializeField] private int depthSlices = 128;
    [SerializeField, Range(0.0f, 2.0f)] private float blurSigma = 1.0f;
    [SerializeField] private bool nonLinearDepth = true;

    public float ShadowDistance => shadowDistance;
    public int ShadowResolution => shadowResolution;
    public float ShadowBias => shadowBias;
    public float ShadowSlopeBias => shadowSlopeBias;
    public int TileSize => tileSize;
    public int DepthSlices => depthSlices;
    public float BlurSigma => blurSigma;
    public bool NonLinearDepth => nonLinearDepth;

    protected override RenderPipeline CreatePipeline()
    {
        return new MorrowindRenderPipeline(this);
    }
}
