using UnityEngine;

public readonly struct PointLightData
{
    public Vector3 Position { get; }
    public float Range { get; }
    public Vector3 Color { get; }
    public uint ShadowIndex { get; }

    public PointLightData(Vector3 position, float range, Vector3 color, uint shadowIndex)
    {
        Position = position;
        Range = range;
        Color = color;
        ShadowIndex = shadowIndex;
    }
}
