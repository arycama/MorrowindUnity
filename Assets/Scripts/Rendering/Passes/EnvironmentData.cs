public readonly struct EnvironmentData : IRenderResource
{
	private readonly BufferHandle environmentData;

	public EnvironmentData(BufferHandle environmentData)
	{
		this.environmentData = environmentData;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(environmentData);
	}
}
