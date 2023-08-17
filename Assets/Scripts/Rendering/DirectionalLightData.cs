using UnityEngine;

public readonly struct DirectionalLightData
{
    public Vector3 Color { get; }
    public int ShadowIndex { get; }
    public Vector3 Direction { get; }
    public int CascadeCount { get; }
    public Matrix3x4 WorldToLight { get; }

    public DirectionalLightData(Vector3 color, int shadowIndex, Vector3 direction, int cascadeCount, Matrix3x4 worldToLight)
    {
        Color = color;
        ShadowIndex = shadowIndex;
        Direction = direction;
        CascadeCount = cascadeCount;
        WorldToLight = worldToLight;
    }
}
