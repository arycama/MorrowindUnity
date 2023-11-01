using System;
using UnityEngine;

[Serializable]
public class ShadowSettings
{
    [SerializeField] private bool customShadowRendering = false;
    [SerializeField] private bool includeCameraPlanes = false;
    [SerializeField, Range(0.0f, 0.5f)] private float maxSpaceWasteTolerance = 0.25f;
    [SerializeField, Range(0.0f, 1.0f)] private float resolutionTolerance = 0.5f;

    [SerializeField] private bool closeFit = true;
    [SerializeField] private bool overlapFix = true;
    [SerializeField, Range(1, 4)] private int shadowCascades = 1;
    [SerializeField] private Vector3 shadowCascadeSplits = new Vector3(0.25f, 0.5f, 0.75f);
    [SerializeField] private float shadowDistance = 4096;
    [SerializeField] private int directionalShadowResolution = 2048;
    [SerializeField] private float shadowBias = 0.0f;
    [SerializeField] private float shadowSlopeBias = 0.0f;
    [SerializeField] private int pointShadowResolution = 256;
    [SerializeField, Range(1, 32)] private int pcfSamples = 4;
    [SerializeField, Min(0f)] private float pcfRadius = 1f;
    [SerializeField, Range(1, 32)] private int blockerSamples = 4;
    [SerializeField, Min(0f)] private float blockerRadius = 1f;
    [SerializeField, Min(0f)] private float pcssSoftness = 1f;

    public bool CustomShadowRendering => customShadowRendering;
    public float MaxSpaceWasteTolerance => maxSpaceWasteTolerance;
    public float ResolutionTolerance => resolutionTolerance;
    public bool CloseFit => closeFit;
    public bool OverlapFix => overlapFix;
    public int ShadowCascades => shadowCascades;
    public Vector3 ShadowCascadeSplits => shadowCascadeSplits;
    public float ShadowDistance => shadowDistance;
    public int DirectionalShadowResolution => directionalShadowResolution;
    public float ShadowBias => shadowBias;
    public float ShadowSlopeBias => shadowSlopeBias;
    public int PointShadowResolution => pointShadowResolution;
    public int PcfSamples => pcfSamples;
    public float PcfRadius => pcfRadius;
    public int BlockerSamples => blockerSamples;
    public float BlockerRadius => blockerRadius;
    public float PcssSoftness => pcssSoftness;

    public bool IncludeCameraPlanes => includeCameraPlanes;
}
