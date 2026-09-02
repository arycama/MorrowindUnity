public readonly struct BlueNoise1D : IRenderResource
{
	public readonly TextureHandle handle;

	public BlueNoise1D(TextureHandle handle)
	{
		this.handle = handle;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(handle);
	}
}
