public readonly struct ViewDataFlipped : IRenderResource
{
	public readonly BufferHandle handle;

	public ViewDataFlipped(BufferHandle handle)
	{
		this.handle = handle;
	}

	public void SetData(PassBuilder builder)
	{
		builder.AddResource(handle);
	}
}
