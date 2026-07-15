using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Morrowind Render Pipeline Asset")]
public class MorrowindRenderPipelineAsset : CustomRenderPipelineAssetBase
{
	[SerializeField] private float shadowDistance = 4096;
	[SerializeField] private int shadowResolution = 2048;
	[SerializeField] private float shadowBias = 0.0f;
	[SerializeField] private float shadowSlopeBias = 0.0f;
	[SerializeField] private bool useSrpBatching = true;
	[field: SerializeField] public float ShadowFadeDistance { get; private set; } = 8f;

	[field: SerializeField] public LightCulling.Settings LightCulling { get; private set; }

	public float ShadowDistance => shadowDistance;
	public int ShadowResolution => shadowResolution;
	public float ShadowBias => shadowBias;
	public float ShadowSlopeBias => shadowSlopeBias;
	public override bool UseSrpBatching => useSrpBatching;

	public override SupportedRenderingFeatures SupportedRenderingFeatures => new()
	{
		defaultMixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.None,
		editableMaterialRenderQueue = false,
		enlighten = false,
		lightmapBakeTypes = LightmapBakeType.Realtime,
		lightmapsModes = LightmapsMode.NonDirectional,
		lightProbeProxyVolumes = false,
		mixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.None,
		motionVectors = true,
		overridesEnvironmentLighting = false,
		overridesFog = false,
		overridesLODBias = false,
		overridesMaximumLODLevel = false,
		overridesOtherLightingSettings = true,
		overridesRealtimeReflectionProbes = true,
		overridesShadowmask = true,
		particleSystemInstancing = true,
		receiveShadows = true,
		reflectionProbeModes = SupportedRenderingFeatures.ReflectionProbeModes.None,
		reflectionProbes = false,
		rendererPriority = false,
		rendererProbes = false,
		rendersUIOverlay = false,
		ambientProbeBaking = false,
		defaultReflectionProbeBaking = false,
		reflectionProbesBlendDistance = false,
		overridesEnableLODCrossFade = false,
		overridesLightProbeSystem = true,
		overridesLightProbeSystemWarningMessage = default,
		supportsHDR = false,
		skyOcclusion = false,
		supportsClouds = false,
	};

	protected override RenderPipeline CreatePipeline()
	{
		return new MorrowindRenderPipeline(this);
	}
}
