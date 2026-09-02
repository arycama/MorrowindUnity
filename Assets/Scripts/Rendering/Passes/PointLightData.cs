public readonly struct PointLightData : IRenderResource
{
	public readonly BufferHandle dataBuffer, lightBuffer, lightDepthMinMaxBuffer;
	public readonly TextureHandle visibleLightBits, pointShadows;
	public readonly int pointLightCount, intersectingLightCount;

	public PointLightData(BufferHandle dataBuffer, BufferHandle lightBuffer, BufferHandle lightDepthMinMaxBuffer, TextureHandle visibleLightBits, TextureHandle pointShadows, int pointLightCount, int intersectingLightCount)
	{
		this.dataBuffer = dataBuffer;
		this.lightBuffer = lightBuffer;
		this.lightDepthMinMaxBuffer = lightDepthMinMaxBuffer;
		this.visibleLightBits = visibleLightBits;
		this.pointShadows = pointShadows;
		this.pointLightCount = pointLightCount;
		this.intersectingLightCount = intersectingLightCount;
	}

	public void SetData(PassBuilder builder)
	{
		if (builder.RenderGraph.IsResourceWritten(visibleLightBits))
		{
			builder.AddKeyword("POINT_LIGHTS_ON");
			builder.AddResources(stackalloc ResourceHandle[] { dataBuffer, lightBuffer, lightDepthMinMaxBuffer, visibleLightBits, pointShadows });
		}
	}
}
