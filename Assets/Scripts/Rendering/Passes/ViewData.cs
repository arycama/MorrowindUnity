public readonly struct ViewData : IRenderResource
{
	public readonly BufferHandle handle;

	public ViewData(BufferHandle handle)
	{
		this.handle = handle;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(handle);
	}
}
