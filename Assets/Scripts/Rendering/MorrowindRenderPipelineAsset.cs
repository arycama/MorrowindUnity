using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Custom Render Pipeline Asset")]
public class MorrowindRenderPipelineAsset : CustomRenderPipelineAsset
{
    [SerializeField] private ShadowSettings shadowSettings;
    [SerializeField] private ClusteredLightCulling.Settings clusteredLightingSettings;
    [SerializeField] private TemporalAA.Settings temporalAASettings;
    [SerializeField] private Bloom.Settings bloomSettings;
    [SerializeField] private AmbientOcclusion.Settings ambientOcclusionSettings;
    [SerializeField] private VolumetricLighting.Settings volumetricLightingSettings;
    [SerializeField] private DepthOfField.Settings depthOfFieldSettings;
    [SerializeField] private DynamicResolution.Settings dynamicResolutionSettings;
    [SerializeField] private AutoExposure.Settings autoExposureSettings;
    [SerializeField] private Tonemapping.Settings tonemappingSettings;
    [SerializeField] private LensSettings lensSettings;

    public ShadowSettings ShadowSettings => shadowSettings;
    public ClusteredLightCulling.Settings ClusteredLightingSettings => clusteredLightingSettings;
    public TemporalAA.Settings TemporalAASettings => temporalAASettings;
    public Bloom.Settings BloomSettings => bloomSettings;
    public AmbientOcclusion.Settings AmbientOcclusionSettings => ambientOcclusionSettings;
    public VolumetricLighting.Settings VolumetricLightingSettings => volumetricLightingSettings;
    public DepthOfField.Settings DepthOfFieldSettings => depthOfFieldSettings;
    public DynamicResolution.Settings DynamicResolutionSettings => dynamicResolutionSettings;
    public AutoExposure.Settings AutoExposureSettings => autoExposureSettings;
    public Tonemapping.Settings TonemappingSettings => tonemappingSettings;
    public LensSettings LensSettings => lensSettings;

    [Header("Water Settings")]
    public Color waterAlbedo = Color.white;
    [ColorUsage(false, true)] public Color waterExtinction = Color.grey;

    protected override RenderPipeline CreatePipeline()
    {
        return new MorrowindRenderPipeline(this);
    }
}
