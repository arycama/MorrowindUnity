public readonly struct CameraDepth : IRenderResource
{
	public readonly TextureHandle handle;

	public CameraDepth(TextureHandle handle)
	{
		this.handle = handle;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(handle);
	}
}
