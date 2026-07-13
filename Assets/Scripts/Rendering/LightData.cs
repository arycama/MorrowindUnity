using Unmath;

public readonly struct LightData
{
	public readonly Float3 position;
	public readonly float rcpRange;
	public readonly Float3 forward;
	public readonly float angleScale;
	public readonly Float3 color;
	public readonly float angleOffset;

	public LightData(Float3 position, float rcpRange, Float3 forward, float angleScale, Float3 color, float angleOffset)
	{
		this.position = position;
		this.rcpRange = rcpRange;
		this.color = color;
		this.angleScale = angleScale;
		this.forward = forward;
		this.angleOffset = angleOffset;
	}
}
