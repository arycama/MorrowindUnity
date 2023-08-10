using UnityEngine;
using UnityEngine.Rendering;

public struct ShadowRequest
{
    public bool IsValid { get; }
    public int VisibleLightIndex { get; }
    public Matrix4x4 ViewMatrix { get; }
    public Matrix4x4 ProjectionMatrix { get; }
    public ShadowSplitData ShadowSplitData { get; }
    public int CubemapFace { get; }

    public ShadowRequest(bool isValid, int visibleLightIndex, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, ShadowSplitData shadowSplitData, int cubemapFace)
    {
        IsValid = isValid;
        VisibleLightIndex = visibleLightIndex;
        ViewMatrix = viewMatrix;
        ProjectionMatrix = projectionMatrix;
        ShadowSplitData = shadowSplitData;
        CubemapFace = cubemapFace;
    }
}
