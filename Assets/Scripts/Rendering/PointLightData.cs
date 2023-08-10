using UnityEngine;

public readonly struct PointLightData
{
    public Vector3 Position { get; }
    public float Range { get; }
    public Vector3 Color { get; }
    public int ShadowIndex { get; }
    public int VisibleFaces { get; }
    public float Near { get; }
    public float Far { get; }
    public float Padding { get; }

    public PointLightData(Vector3 position, float range, Vector3 color, int shadowIndex, int visibleFaces, float near, float far)
    {
        Position = position;
        Range = range;
        Color = color;
        ShadowIndex = shadowIndex;
        VisibleFaces = visibleFaces;
        Near = 1 + far / (near - far);
        Far = -(near * far) / (near - far);
        Padding = 0f;
    }
}
