public readonly struct BlueNoise2D : IRenderResource
{
	public readonly TextureHandle handle;

	public BlueNoise2D(TextureHandle handle)
	{
		this.handle = handle;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(handle);
	}
}
