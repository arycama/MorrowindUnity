using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Morrowind Render Pipeline Asset")]
public class MorrowindRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] private float shadowDistance = 4096;
    [SerializeField] private int shadowResolution = 2048;
    [SerializeField] private float shadowBias = 0.0f;
    [SerializeField] private float shadowSlopeBias = 0.0f;
    [SerializeField] private bool useSrpBatching = true;

    public float ShadowDistance => shadowDistance;
    public int ShadowResolution => shadowResolution;
    public float ShadowBias => shadowBias;
    public float ShadowSlopeBias => shadowSlopeBias;
    public bool UseSrpBatching => useSrpBatching;

    protected override RenderPipeline CreatePipeline()
    {
        return new MorrowindRenderPipeline(this);
    }
}
