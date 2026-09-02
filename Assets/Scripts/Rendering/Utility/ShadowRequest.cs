using UnityEngine.Rendering;
using Unmath;

public readonly struct ShadowRequest
{
	public readonly int LightIndex;
	public readonly Float4x4 ViewMatrix;
	public readonly Float4x4 ProjectionMatrix;
	// TODO: This is only used for culling planes, maybe replace?
	public readonly ShadowSplitData ShadowSplitData;
	public readonly int CubemapFace;
	public readonly Float3 LightPosition;
	public readonly bool HasCasters;
	public readonly float Near;
	public readonly float Far;
	public readonly Float3 ViewPosition;
	public readonly Quaternion ViewRotation;
	public readonly float Width;
	public readonly float Height;
	public readonly Int2 Resolution;

	public ShadowRequest(int lightIndex, Float4x4 viewMatrix, Float4x4 projectionMatrix, ShadowSplitData shadowSplitData, int cubemapFace, Float3 lightPosition, bool hasCasters, float near, float far, Float3 viewPosition, Quaternion viewRotation, float width, float height, Int2 resolution)
	{
		LightIndex = lightIndex;
		ViewMatrix = viewMatrix;
		ProjectionMatrix = projectionMatrix;
		ShadowSplitData = shadowSplitData;
		CubemapFace = cubemapFace;
		LightPosition = lightPosition;
		HasCasters = hasCasters;
		Near = near;
		Far = far;
		ViewPosition = viewPosition;
		ViewRotation = viewRotation;
		Width = width;
		Height = height;
		Resolution = resolution;
	}
}