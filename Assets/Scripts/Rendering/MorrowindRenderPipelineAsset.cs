using UnityEngine;
using UnityEngine.Rendering;
using CustomRenderPipeline;
using System;

[CreateAssetMenu(menuName = "Morrowind Render Pipeline Asset")]
public class MorrowindRenderPipelineAsset : CustomRenderPipelineAssetBase
{
	[SerializeField] private bool useSrpBatching = true;
    [field: SerializeField] public RaytracingSystem.Settings RayTracingSettings { get; private set; }
	[field: SerializeField] public float ShadowFadeDistance { get; private set; } = 8f;
	[field: SerializeField, Range(0, 1)] public float FogStartDensity { get; private set; } = 0.95f;
	[field: SerializeField, Range(0, 1)] public float FogAtDensity { get; private set; } = 0.0f;

	[field: SerializeField] public LightingSettings LightingSettings { get; private set; }
	[field: SerializeField] public LightCulling.Settings LightCulling { get; private set; }
	[field: SerializeField] public VolumetricLightingOld.Settings VolumetricLighting { get; private set; }

	public override bool UseSrpBatching => useSrpBatching;

	public override string renderPipelineShaderTag => "MorrowindRenderPipeline";
	public override Type pipelineType => typeof(MorrowindRenderPipeline);

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
