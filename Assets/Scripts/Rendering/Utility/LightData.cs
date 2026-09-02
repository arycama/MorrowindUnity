using Unmath;

public readonly struct LightData
{
	public readonly Float3 position;
	public readonly float rcpRangeSq;
	public readonly Float3 forward;
	public readonly float angleScale;
	public readonly Float3 color;
	public readonly float angleOffset;
	public readonly Float4 cullingSphere;
	public readonly uint shadowIndex;
	public readonly float shadowProjectionX;
	public readonly float shadowProjectionY;
	public readonly float padding;

	public LightData(Float3 position, float rcpRangeSq, Float3 forward, float angleScale, Float3 color, float angleOffset, Float4 cullingSphere, uint shadowIndex, float shadowProjectionX, float shadowProjectionY)
	{
		this.position = position;
		this.rcpRangeSq = rcpRangeSq;
		this.color = color;
		this.angleScale = angleScale;
		this.forward = forward;
		this.angleOffset = angleOffset;
		this.cullingSphere = cullingSphere;
		this.shadowIndex = shadowIndex;
		this.shadowProjectionX = shadowProjectionX;
		this.shadowProjectionY = shadowProjectionY;
		this.padding = 0;
	}
}
