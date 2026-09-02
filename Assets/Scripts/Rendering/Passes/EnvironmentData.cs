public readonly struct EnvironmentData : IRenderResource
{
	private readonly BufferHandle environmentData;
	private readonly RenderTargetHandle sunShadow;

	public EnvironmentData(BufferHandle environmentData, RenderTargetHandle sunShadow)
	{
		this.environmentData = environmentData;
		this.sunShadow = sunShadow;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(environmentData);

		if (builder.RenderGraph.IsResourceWritten(sunShadow))
		{
			builder.AddResource(sunShadow);
			builder.AddKeyword("SHADOWS_ON");
		}
	}
}
