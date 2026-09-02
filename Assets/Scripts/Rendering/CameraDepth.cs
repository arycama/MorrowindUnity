public readonly struct CameraDepth : IRenderResource
{
	public readonly RenderTargetHandle handle;

	public CameraDepth(RenderTargetHandle handle)
	{
		this.handle = handle;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(handle);
	}
}
