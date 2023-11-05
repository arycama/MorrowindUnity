using System;
using UnityEngine;

[Serializable]
public class TemporalAASettings
{
    [SerializeField, Range(1, 32)]
    private int sampleCount = 8;

    [SerializeField, Range(0.0f, 1f), Tooltip("The diameter (in texels) inside which jitter samples are spread. Smaller values result in crisper but more aliased output, while larger values result in more stable, but blurrier, output.")]
    private float jitterSpread = 0.75f;

    [SerializeField, Range(0f, 3f), Tooltip("Controls the amount of sharpening applied to the color buffer. High values may introduce dark-border artifacts.")]
    private float sharpness = 0.25f;

    [SerializeField, Range(0f, 0.99f), Tooltip("The blend coefficient for a stationary fragment. Controls the percentage of history sample blended into the final color.")]
    private float stationaryBlending = 0.95f;

    [SerializeField, Range(0f, 0.99f), Tooltip("The blend coefficient for a fragment with significant motion. Controls the percentage of history sample blended into the final color.")]
    private float motionBlending = 0.85f;

    public int SampleCount => sampleCount;
    public float JitterSpread => jitterSpread;
    public float Sharpness => sharpness;
    public float StationaryBlending => stationaryBlending;
    public float MotionBlending => motionBlending;
}