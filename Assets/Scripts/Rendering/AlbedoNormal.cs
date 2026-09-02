public readonly struct AlbedoNormal : IRenderResource
{
	public readonly TextureHandle handle;

	public AlbedoNormal(TextureHandle handle)
	{
		this.handle = handle;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(handle);
	}
}
