using UnityEngine;

public struct DirectionalLightData
{
    private Vector3 color;
    private int shadowIndex;
    private Vector3 direction;
    private int cascadeCount;

    public DirectionalLightData(Vector3 color, int shadowIndex, Vector3 direction, int cascadeCount)
    {
        this.color = color;
        this.shadowIndex = shadowIndex;
        this.direction = direction;
        this.cascadeCount = cascadeCount;
    }

    public Vector3 Color => color;
    public int ShadowIndex => shadowIndex;
    public Vector3 Direction => direction;
    public int CascadeCount => cascadeCount;
}