using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Data/New Pipeline")]
public class NewPipelineAsset : RenderPipelineAsset<NewPipeline>
{
	[field: SerializeField, Pow2(8)] public int Samples { get; private set; } = 1;

	[Header("Volumetric Light")]
	[field: SerializeField] public bool VolumetricsEnabled { get; private set; } = true;
	[field: SerializeField] public int VolumetricTileSize { get; private set; } = 8;
	[field: SerializeField] public int VolumetricSlices { get; private set; } = 64;
	[field: SerializeField] public float VolumetricDistance { get; private set; } = 512.0f;

	[field: SerializeField] public float FocusDistance { get; private set; } = 10.0f;
	[field: SerializeField] public float SensorSize { get; private set; } = 24.0f;
	[field: SerializeField] public float Aperture { get; private set; } = 1.0f / 16.0f;

	[field: SerializeField] public LayerMask RayTracingLayerMask { get; private set; } = ~0;
	[field: SerializeField] public bool RaytracedOcclusion { get; private set; }
	[field: SerializeField] public bool RaytracedShadows { get; private set; }
	[field: SerializeField] public bool RaytracedDiffuse { get; private set; }
	[field: SerializeField] public bool RaytracedSpecular { get; private set; }
	[field: SerializeField] public bool RaytracedDepthOfField { get; private set; }

	[field: SerializeField] public LightingSettings Lighting { get; private set; }
	[field: SerializeField] public LightCulling.Settings LightCulling { get; private set; }
	[field: SerializeField] public Bloom.Settings Bloom { get; private set; }

	[SerializeField] private DefaultPipelineMaterials defaultMaterials = new();
	[SerializeField] private DefaultPipelineShaders defaultShaders = new();

	public sealed override Material defaultMaterial => defaultMaterials.DefaultMaterial ?? base.defaultMaterial;
	public sealed override Material defaultUIMaterial => defaultMaterials.DefaultUIMaterial ?? base.defaultUIMaterial;
	public sealed override Material default2DMaterial => defaultMaterials.Default2DMaterial ?? base.default2DMaterial;
	public sealed override Material defaultLineMaterial => defaultMaterials.DefaultLineMaterial ?? base.defaultLineMaterial;
	public sealed override Material defaultParticleMaterial => defaultMaterials.DefaultParticleMaterial ?? base.defaultParticleMaterial;
	public sealed override Material defaultTerrainMaterial => defaultMaterials.DefaultTerrainMaterial ?? base.defaultTerrainMaterial;
	public sealed override Material defaultUIETC1SupportedMaterial => defaultMaterials.DefaultUIETC1SupportedMaterial ?? base.defaultUIETC1SupportedMaterial;
	public sealed override Material defaultUIOverdrawMaterial => defaultMaterials.DefaultUIOverdrawMaterial ?? base.defaultUIOverdrawMaterial;
	public sealed override Material default2DMaskMaterial => defaultMaterials.Default2DMaskMaterial;

	public sealed override Shader autodeskInteractiveMaskedShader => defaultShaders.AutodeskInteractiveMaskedShader ?? base.autodeskInteractiveMaskedShader;
	public sealed override Shader autodeskInteractiveShader => defaultShaders.AutodeskInteractiveShader ?? base.autodeskInteractiveShader;
	public sealed override Shader autodeskInteractiveTransparentShader => defaultShaders.AutodeskInteractiveTransparentShader ?? base.autodeskInteractiveTransparentShader;
	public sealed override Shader defaultSpeedTree7Shader => defaultShaders.DefaultSpeedTree7Shader ?? base.defaultSpeedTree7Shader;
	public sealed override Shader defaultSpeedTree8Shader => defaultShaders.DefaultSpeedTree8Shader ?? base.defaultSpeedTree8Shader;
	public sealed override Shader defaultSpeedTree9Shader => defaultShaders.DefaultSpeedTree9Shader ?? base.defaultSpeedTree9Shader;
	public sealed override Shader defaultShader => defaultShaders.DefaultShader ?? base.defaultShader;
	public sealed override Shader terrainDetailGrassBillboardShader => defaultShaders.TerrainDetailGrassBillboardShader ?? base.terrainDetailGrassBillboardShader;
	public sealed override Shader terrainDetailGrassShader => defaultShaders.TerrainDetailGrassShader ?? base.terrainDetailGrassShader;
	public sealed override Shader terrainDetailLitShader => defaultShaders.TerrainDetailLitShader ?? base.terrainDetailLitShader;

	public override string renderPipelineShaderTag => "NewPipeline";

	public SupportedRenderingFeatures SupportedRenderingFeatures => new()
	{
		ambientProbeBaking = false,
		defaultReflectionProbeBaking = false,
		defaultMixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.None,
		editableMaterialRenderQueue = false,
		enlighten = false,
		lightmapBakeTypes = LightmapBakeType.Realtime,
		lightmapsModes = LightmapsMode.NonDirectional,
		lightProbeProxyVolumes = false,
		mixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.None,
		motionVectors = false,
		overridesEnableLODCrossFade = true,
		overridesEnvironmentLighting = false,
		overridesFog = false,
		overridesMaximumLODLevel = false,
		overridesLightProbeSystem = true,
		overridesLightProbeSystemWarningMessage = default,
		overridesLODBias = false,
		overridesOtherLightingSettings = true,
		overridesRealtimeReflectionProbes = true,
		overridesShadowmask = true,
		particleSystemInstancing = true,
		receiveShadows = true,
		reflectionProbeModes = SupportedRenderingFeatures.ReflectionProbeModes.None,
		reflectionProbes = false,
		rendererPriority = false,
		rendererProbes = false,
		rendersUIOverlay = true,
		reflectionProbesBlendDistance = false,
		skyOcclusion = false,
		supportsClouds = false,
		supportsHDR = true
	};

	protected override RenderPipeline CreatePipeline()
	{
		return new NewPipeline(this);
	}

	protected override void OnValidate()
	{
	}

	public void ReloadRenderPipeline()
	{
		// This internally calls RenderPipelineManager.RecreateCurrentPipeline(this), which is internal so we can't call it directly..
		base.OnValidate();
	}
}
