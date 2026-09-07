public readonly struct AlbedoNormal : IRenderResource
{
	public readonly RenderTargetHandle handle;

	public AlbedoNormal(RenderTargetHandle handle)
	{
		this.handle = handle;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(handle);
	}
}
