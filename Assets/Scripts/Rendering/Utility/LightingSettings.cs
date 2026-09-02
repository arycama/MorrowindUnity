using System;
using UnityEngine;

[Serializable]
public class LightingSettings
{
	[field: SerializeField] public bool MicroShadows { get; private set; } = true;
	[field: SerializeField, Range(1e-3f, 180)] public float SunAngularDiameter { get; private set; } = 0.52f;

	[field: Header("Sun Shadows")]
	[field: SerializeField, Min(0.0f), Tooltip("Length of fadeout for directional shadow")] public float DirectionalFadeLength { get; private set; } = 16f;
	[field: SerializeField, Range(0, 5)] public int DirectionalMaxFilterSize { get; private set; } = 1;
	[field: SerializeField, Min(0.0f), Tooltip("Simulated distance between receiver and a blocker, used to calculate penumbra size")] public float DirectionalBlockerDistance { get; private set; } = 1.5f;
	[field: SerializeField] public bool Use32Bit { get; private set; } = false;
	[field: SerializeField, Range(0, 2)] public float CascadeUniformity { get; private set; } = 0.5f;
	[field: SerializeField, Range(1, 8)] public int DirectionalCascadeCount { get; private set; } = 4;
	[field: SerializeField] public float DirectionalShadowDistance { get; private set; } = 128;
	[field: SerializeField] public int DirectionalShadowResolution { get; private set; } = 4096;
	[field: SerializeField] public float DirectionalShadowBias { get; private set; } = 5;
	[field: SerializeField] public float DirectionalShadowSlopeBias { get; private set; } = 1;

	[field: Header("Point Shadows")]
	[field: SerializeField] public int PointShadowResolution { get; private set; } = 256;
	[field: SerializeField] public float PointShadowBias { get; private set; } = 0.0f;
	[field: SerializeField] public float PointShadowSlopeBias { get; private set; } = 0.0f;

	[field: Header("Spot Shadows")]
	[field: SerializeField] public int SpotShadowResolution { get; private set; } = 512;
	[field: SerializeField] public float SpotShadowBias { get; private set; } = 0.0f;
	[field: SerializeField] public float SpotShadowSlopeBias { get; private set; } = 0.0f;
}
