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

	public LightData(Float3 position, float rcpRangeSq, Float3 forward, float angleScale, Float3 color, float angleOffset, Float4 cullingSphere)
	{
		this.position = position;
		this.rcpRangeSq = rcpRangeSq;
		this.color = color;
		this.angleScale = angleScale;
		this.forward = forward;
		this.angleOffset = angleOffset;
		this.cullingSphere = cullingSphere;
	}
}
