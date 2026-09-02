public readonly struct CameraColor : IRenderResource
{
	public readonly RenderTargetHandle handle;

	public CameraColor(RenderTargetHandle handle)
	{
		this.handle = handle;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(handle);
	}
}
